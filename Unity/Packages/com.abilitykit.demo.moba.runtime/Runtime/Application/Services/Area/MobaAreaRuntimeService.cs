using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Area
{
    [WorldService(typeof(MobaAreaRuntimeService))]
    public sealed class MobaAreaRuntimeService : IService
    {
        [WorldInject(required: false)] private IProjectileService _projectiles = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IMobaTemporaryEntityLifecycleService _lifecycle = null;
        [WorldInject(required: false)] private MobaTraceRegistry _trace = null;
        [WorldInject(required: false)] private MobaSkillCastRuntimeService _skillRuntimes = null;

        private readonly Dictionary<int, MobaAreaRuntimeInfo> _areas = new Dictionary<int, MobaAreaRuntimeInfo>();
        private readonly Dictionary<int, MobaSkillRuntimeRetainHandle> _skillRuntimeRetainsByAreaId = new Dictionary<int, MobaSkillRuntimeRetainHandle>();
        private readonly Dictionary<int, List<int>> _areasByOwner = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> _areasByTemplate = new Dictionary<int, List<int>>();
        private readonly HashSet<int> _delayTriggeredAreas = new HashSet<int>();
        private readonly List<int> _queryBuffer = new List<int>(32);
        private readonly MobaSnapshotBuffer<MobaAreaEventSnapshotEntry> _presentationEvents = new MobaSnapshotBuffer<MobaAreaEventSnapshotEntry>(32, 512);

        public int ActiveCount => _areas.Count;

        public void RegisterSpawn(
            AreaId areaId,
            int templateId,
            int ownerActorId,
            in Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int frame,
            int delayFrames,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            if (areaId.Value <= 0) return;

            if (ownerActorId <= 0 || sourceContextId == 0L)
            {
                throw new InvalidOperationException($"Area spawn requires source context. areaId={areaId.Value} templateId={templateId} ownerActorId={ownerActorId} sourceContextId={sourceContextId}");
            }

            var info = new MobaAreaRuntimeInfo(
                areaId.Value,
                templateId,
                ownerActorId,
                center,
                radius,
                collisionLayerMask,
                maxTargets,
                frame,
                delayFrames > 0 ? frame + delayFrames : frame,
                sourceContextId,
                rootContextId != 0L ? rootContextId : sourceContextId,
                ownerContextId != 0L ? ownerContextId : sourceContextId,
                skillRuntimeHandle);

            if (_areas.TryGetValue(areaId.Value, out var oldInfo))
            {
                Unindex(oldInfo);
                EndAreaTrace(in oldInfo, TraceLifecycleReason.Replaced);
                ReleaseSkillRuntime(areaId.Value);
            }

            _areas[areaId.Value] = info;
            RetainSkillRuntime(in info);
            _delayTriggeredAreas.Remove(areaId.Value);
            Index(_areasByOwner, ownerActorId, areaId.Value);
            Index(_areasByTemplate, templateId, areaId.Value);
            _presentationEvents.Add(new MobaAreaEventSnapshotEntry((int)AreaEventKind.Spawn, areaId.Value, ownerActorId, templateId, center.X, center.Y, center.Z, radius));
            _lifecycle?.RecordSpawn(MobaTemporaryEntityKind.Area, ActiveCount, frame);
        }

        public bool Unregister(AreaId areaId)
        {
            if (areaId.Value <= 0) return false;
            if (!_areas.TryGetValue(areaId.Value, out var info)) return false;

            _areas.Remove(areaId.Value);
            _delayTriggeredAreas.Remove(areaId.Value);
            Unindex(info);
            _presentationEvents.Add(new MobaAreaEventSnapshotEntry((int)AreaEventKind.Expire, info.AreaId, info.OwnerActorId, info.TemplateId, info.Center.X, info.Center.Y, info.Center.Z, info.Radius));
            EndAreaTrace(in info, TraceLifecycleReason.Completed);
            ReleaseSkillRuntime(areaId.Value);
            _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Area, ActiveCount, CurrentFrame);
            return true;
        }

        public bool RollbackSpawn(
            AreaId areaId,
            long expectedSourceContextId)
        {
            if (areaId.Value <= 0) return false;
            if (!_areas.TryGetValue(areaId.Value, out var info)) return false;
            if (expectedSourceContextId != 0L
                && info.SourceContextId != expectedSourceContextId)
            {
                return false;
            }

            _areas.Remove(areaId.Value);
            _delayTriggeredAreas.Remove(areaId.Value);
            Unindex(info);
            ReleaseSkillRuntime(areaId.Value);

            try
            {
                _lifecycle?.RecordDespawn(
                    MobaTemporaryEntityKind.Area,
                    ActiveCount,
                    CurrentFrame);
            }
            catch (Exception ex)
            {
                AbilityKit.Core.Logging.Log.Exception(
                    ex,
                    $"[MobaAreaRuntimeService] Record area despawn failed during spawn rollback (areaId={areaId.Value})");
            }

            return true;
        }

        public int DrainPresentationEvents(IList<MobaAreaEventSnapshotEntry> results)
        {
            if (results == null) return 0;
            return _presentationEvents.DrainTo(results);
        }

        public bool TryGetArea(int areaId, out MobaAreaRuntimeInfo info)
        {
            if (areaId <= 0)
            {
                info = default;
                return false;
            }

            return _areas.TryGetValue(areaId, out info);
        }

        public int CollectDueDelayAreas(int frame, List<MobaAreaRuntimeInfo> results)
        {
            if (results == null) return 0;
            results.Clear();

            foreach (var kv in _areas)
            {
                var areaId = kv.Key;
                var info = kv.Value;
                if (_delayTriggeredAreas.Contains(areaId)) continue;
                if (info.DelayTriggerFrame > frame) continue;

                _delayTriggeredAreas.Add(areaId);
                results.Add(info);
            }

            return results.Count;
        }

        public bool TryGetAreas(List<MobaAreaRuntimeInfo> results, int ownerActorId = 0, int templateId = 0)
        {
            if (results == null) return false;
            results.Clear();

            _queryBuffer.Clear();
            CollectAreaIds(_queryBuffer, ownerActorId, templateId);
            for (var i = 0; i < _queryBuffer.Count; i++)
            {
                if (_areas.TryGetValue(_queryBuffer[i], out var info))
                {
                    results.Add(info);
                }
            }

            _queryBuffer.Clear();
            return results.Count > 0;
        }

        public bool DespawnArea(int areaId)
        {
            if (areaId <= 0) return false;
            if (!_areas.ContainsKey(areaId)) return false;
            if (_projectiles == null) return false;

            return _projectiles.DespawnArea(new AreaId(areaId), CurrentFrame);
        }

        public int DespawnAreas(int ownerActorId, int templateId, bool removeAll)
        {
            _queryBuffer.Clear();
            CollectAreaIds(_queryBuffer, ownerActorId, templateId);

            var removed = 0;
            for (var i = 0; i < _queryBuffer.Count; i++)
            {
                if (DespawnArea(_queryBuffer[i])) removed++;
                if (removed > 0 && !removeAll) break;
            }

            _queryBuffer.Clear();
            return removed;
        }

        public void Dispose()
        {
            foreach (var pair in _areas)
            {
                var info = pair.Value;
                EndAreaTrace(in info, TraceLifecycleReason.Cancelled);
            }

            ReleaseAllSkillRuntimes();
            _areas.Clear();
            _areasByOwner.Clear();
            _areasByTemplate.Clear();
            _queryBuffer.Clear();
            _delayTriggeredAreas.Clear();
            _presentationEvents.ClearAndTrim();
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Area, 0, CurrentFrame);
        }

        private int CurrentFrame
        {
            get
            {
                if (_frameTime != null) return _frameTime.Frame.Value;
                throw new InvalidOperationException("MobaAreaRuntimeService requires IFrameTime for current frame.");
            }
        }

        private void CollectAreaIds(List<int> results, int ownerActorId, int templateId)
        {
            if (results == null) return;

            if (ownerActorId > 0 && templateId > 0)
            {
                if (!_areasByOwner.TryGetValue(ownerActorId, out var ownerList) || ownerList == null) return;
                for (var i = 0; i < ownerList.Count; i++)
                {
                    var areaId = ownerList[i];
                    if (_areas.TryGetValue(areaId, out var info) && info.TemplateId == templateId)
                    {
                        results.Add(areaId);
                    }
                }

                return;
            }

            if (ownerActorId > 0)
            {
                CopyIndexed(_areasByOwner, ownerActorId, results);
                return;
            }

            if (templateId > 0)
            {
                CopyIndexed(_areasByTemplate, templateId, results);
                return;
            }

            foreach (var kv in _areas)
            {
                results.Add(kv.Key);
            }
        }

        private void Unindex(MobaAreaRuntimeInfo info)
        {
            RemoveIndexed(_areasByOwner, info.OwnerActorId, info.AreaId);
            RemoveIndexed(_areasByTemplate, info.TemplateId, info.AreaId);
        }

        private void EndAreaTrace(in MobaAreaRuntimeInfo info, TraceLifecycleReason reason)
        {
            if (_trace == null) return;
            if (info.SourceContextId == 0L) return;
            _trace.EndContext(info.SourceContextId, reason);
        }

        private bool RetainSkillRuntime(in MobaAreaRuntimeInfo info)
        {
            if (_skillRuntimes == null) return false;
            if (!info.SkillRuntimeHandle.IsValid) return false;
            if (_skillRuntimeRetainsByAreaId.ContainsKey(info.AreaId)) return true;

            var child = new MobaSkillRuntimeChildRef(
                MobaSkillRuntimeChildKind.Area,
                info.AreaId,
                info.SourceContextId,
                info.TemplateId);
            var runtimeHandle = info.SkillRuntimeHandle;
            if (!_skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle)) return false;

            _skillRuntimeRetainsByAreaId[info.AreaId] = retainHandle;
            return true;
        }

        private void ReleaseSkillRuntime(int areaId)
        {
            if (!_skillRuntimeRetainsByAreaId.TryGetValue(areaId, out var retainHandle)) return;
            _skillRuntimeRetainsByAreaId.Remove(areaId);

            try
            {
                _skillRuntimes?.ReleaseChild(in retainHandle);
            }
            catch (Exception ex)
            {
                AbilityKit.Core.Logging.Log.Exception(ex, $"[MobaAreaRuntimeService] Release skill runtime retain failed (areaId={areaId})");
            }
        }

        private void ReleaseAllSkillRuntimes()
        {
            if (_skillRuntimeRetainsByAreaId.Count == 0) return;

            foreach (var pair in _skillRuntimeRetainsByAreaId)
            {
                var areaId = pair.Key;
                var retainHandle = pair.Value;
                try
                {
                    _skillRuntimes?.ReleaseChild(in retainHandle);
                }
                catch (Exception ex)
                {
                    AbilityKit.Core.Logging.Log.Exception(ex, $"[MobaAreaRuntimeService] Release skill runtime retain failed during dispose (areaId={areaId})");
                }
            }

            _skillRuntimeRetainsByAreaId.Clear();
        }

        private static void Index(Dictionary<int, List<int>> index, int key, int areaId)
        {
            if (key <= 0 || areaId <= 0) return;
            if (!index.TryGetValue(key, out var list) || list == null)
            {
                list = new List<int>(8);
                index[key] = list;
            }

            if (!list.Contains(areaId)) list.Add(areaId);
        }

        private static void CopyIndexed(Dictionary<int, List<int>> index, int key, List<int> results)
        {
            if (key <= 0) return;
            if (!index.TryGetValue(key, out var list) || list == null) return;
            for (var i = 0; i < list.Count; i++)
            {
                results.Add(list[i]);
            }
        }

        private static void RemoveIndexed(Dictionary<int, List<int>> index, int key, int areaId)
        {
            if (key <= 0 || areaId <= 0) return;
            if (!index.TryGetValue(key, out var list) || list == null) return;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == areaId)
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0)
            {
                index.Remove(key);
            }
        }
    }

    public readonly struct MobaAreaRuntimeInfo
    {
        public readonly int AreaId;
        public readonly int TemplateId;
        public readonly int OwnerActorId;
        public readonly Vec3 Center;
        public readonly float Radius;
        public readonly int CollisionLayerMask;
        public readonly int MaxTargets;
        public readonly int SpawnFrame;
        public readonly int DelayTriggerFrame;
        public readonly long SourceContextId;
        public readonly long RootContextId;
        public readonly long OwnerContextId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;

        public MobaAreaRuntimeInfo(
            int areaId,
            int templateId,
            int ownerActorId,
            in Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int spawnFrame,
            int delayTriggerFrame,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            AreaId = areaId;
            TemplateId = templateId;
            OwnerActorId = ownerActorId;
            Center = center;
            Radius = radius;
            CollisionLayerMask = collisionLayerMask;
            MaxTargets = maxTargets;
            SpawnFrame = spawnFrame;
            DelayTriggerFrame = delayTriggerFrame;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
        }
    }
}
