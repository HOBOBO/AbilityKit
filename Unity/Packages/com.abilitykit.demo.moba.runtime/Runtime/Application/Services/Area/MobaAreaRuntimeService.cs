using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Rollback;

namespace AbilityKit.Demo.Moba.Services.Area
{
    [WorldService(typeof(MobaAreaRuntimeService))]
    public sealed class MobaAreaRuntimeService :
        IMobaRuntimeObjectBootstrapContributor,
        IService
    {
        [WorldInject(required: false)] private IProjectileService _projectiles = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IMobaTemporaryEntityLifecycleService _lifecycle = null;
        [WorldInject] private MobaExecutionContextRegistry _executionContexts = null;
        [WorldInject(required: false)] private MobaSkillCastRuntimeService _skillRuntimes = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectLifecycleHook _objectLifecycle = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectBootstrapRegistry _objectBootstrap = null;
        [WorldInject(required: false)] private MobaAreaPresentationCueReporter _presentationCues = null;

        private bool _objectBootstrapRegistered;

        private readonly Dictionary<int, MobaAreaRuntimeInfo> _areas = new Dictionary<int, MobaAreaRuntimeInfo>();
        private readonly Dictionary<int, MobaSkillRuntimeRetainHandle> _skillRuntimeRetainsByAreaId = new Dictionary<int, MobaSkillRuntimeRetainHandle>();
        private readonly Dictionary<int, List<int>> _areasByOwner = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> _areasByTemplate = new Dictionary<int, List<int>>();
        private readonly HashSet<int> _delayTriggeredAreas = new HashSet<int>();
        private readonly List<int> _queryBuffer = new List<int>(32);
        private readonly MobaSnapshotBuffer<MobaAreaEventSnapshotEntry> _presentationEvents = new MobaSnapshotBuffer<MobaAreaEventSnapshotEntry>(32, 512);

        public int ActiveCount => _areas.Count;

        internal MobaAreaRuntimeRollbackCheckpoint CaptureRollbackCheckpoint()
        {
            var infos = new List<MobaAreaRuntimeInfo>(_areas.Values);
            infos.Sort((left, right) => left.AreaId.CompareTo(right.AreaId));
            var entries = new MobaAreaRuntimeRollbackEntry[infos.Count];
            for (var i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                entries[i] = MobaAreaRuntimeRollbackEntry.From(in info);
            }
            var delayed = new List<int>(_delayTriggeredAreas);
            delayed.Sort();
            return new MobaAreaRuntimeRollbackCheckpoint
            {
                Version = 1,
                Areas = entries,
                DelayedAreaIds = delayed.ToArray(),
            };
        }

        internal MobaAreaRuntimeLocalRollbackState CaptureLocalRollbackState()
        {
            var events = new List<MobaAreaEventSnapshotEntry>(_presentationEvents.Count);
            _presentationEvents.CopyTo(events);
            return new MobaAreaRuntimeLocalRollbackState(CaptureRollbackCheckpoint(), events.ToArray());
        }

        internal void RestoreRollbackCheckpoint(MobaAreaRuntimeRollbackCheckpoint checkpoint)
        {
            ApplyRollbackCheckpoint(checkpoint, clearPendingEvents: true);
        }

        internal void RestoreLocalRollbackState(MobaAreaRuntimeLocalRollbackState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            ApplyRollbackCheckpoint(state.Checkpoint, clearPendingEvents: true);
            _presentationEvents.AddRange(new List<MobaAreaEventSnapshotEntry>(state.PendingEvents));
        }

        private void ApplyRollbackCheckpoint(MobaAreaRuntimeRollbackCheckpoint checkpoint, bool clearPendingEvents)
        {
            if (checkpoint == null || checkpoint.Version != 1 || checkpoint.Areas == null || checkpoint.DelayedAreaIds == null)
                throw new InvalidOperationException("Invalid Area runtime rollback checkpoint.");

            var areas = new Dictionary<int, MobaAreaRuntimeInfo>(checkpoint.Areas.Length);
            var retains = new Dictionary<int, MobaSkillRuntimeRetainHandle>(checkpoint.Areas.Length);
            var byOwner = new Dictionary<int, List<int>>();
            var byTemplate = new Dictionary<int, List<int>>();
            for (var i = 0; i < checkpoint.Areas.Length; i++)
            {
                var info = checkpoint.Areas[i].ToInfo();
                if (!areas.TryAdd(info.AreaId, info))
                    throw new InvalidOperationException($"Duplicate Area id in rollback checkpoint: {info.AreaId}.");
                if (info.SkillRuntimeHandle.IsValid)
                {
                    if (_skillRuntimes == null)
                        throw new InvalidOperationException($"SkillRuntime service is unavailable for Area retain restore: {info.AreaId}.");
                    var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area,
                        info.AreaId, info.SourceContextId, info.TemplateId);
                    var runtime = info.SkillRuntimeHandle;
                    if (!_skillRuntimes.TryFindRetain(runtime, child, out var retain))
                        throw new InvalidOperationException($"Target SkillRuntime Area retain is missing: areaId={info.AreaId}.");
                    retains.Add(info.AreaId, retain);
                }
                Index(byOwner, info.OwnerActorId, info.AreaId);
                Index(byTemplate, info.TemplateId, info.AreaId);
            }

            var delayed = new HashSet<int>();
            for (var i = 0; i < checkpoint.DelayedAreaIds.Length; i++)
                if (!areas.ContainsKey(checkpoint.DelayedAreaIds[i]) || !delayed.Add(checkpoint.DelayedAreaIds[i]))
                    throw new InvalidOperationException("Area delay-trigger checkpoint references a missing or duplicate Area.");

            var desiredCueInfos = new List<MobaAreaRuntimeInfo>(areas.Values);
            desiredCueInfos.Sort((left, right) => left.AreaId.CompareTo(right.AreaId));
            _areas.Clear();
            foreach (var pair in areas) _areas.Add(pair.Key, pair.Value);
            _skillRuntimeRetainsByAreaId.Clear();
            foreach (var pair in retains) _skillRuntimeRetainsByAreaId.Add(pair.Key, pair.Value);
            _areasByOwner.Clear();
            foreach (var pair in byOwner) _areasByOwner.Add(pair.Key, pair.Value);
            _areasByTemplate.Clear();
            foreach (var pair in byTemplate) _areasByTemplate.Add(pair.Key, pair.Value);
            _delayTriggeredAreas.Clear();
            foreach (var areaId in delayed) _delayTriggeredAreas.Add(areaId);
            if (clearPendingEvents) _presentationEvents.Clear();
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Area, ActiveCount, CurrentFrame);
            _presentationCues?.ReconcileActiveAreas(desiredCueInfos);
        }

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
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            int groupKey = 0,
            bool claimTargetsWithinGroup = false,
            int claimUnitSubTypeMask = 0)
        {
            RegisterSpawn(
                areaId,
                templateId,
                ownerActorId,
                in center,
                radius,
                collisionLayerMask,
                maxTargets,
                frame,
                delayFrames,
                sourceContextId,
                rootContextId,
                ownerContextId,
                MobaAreaShapeDefaults.Circle,
                Vec3.Forward,
                0f,
                radius * 2f,
                radius * 2f,
                skillRuntimeHandle,
                groupKey,
                claimTargetsWithinGroup,
                claimUnitSubTypeMask: claimUnitSubTypeMask);
        }

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
            int shapeKind,
            in Vec3 forward,
            float halfAngleDeg,
            float width,
            float length,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            int groupKey = 0,
            bool claimTargetsWithinGroup = false,
            float innerRadius = 0f,
            int claimUnitSubTypeMask = 0)
        {
            if (areaId.Value <= 0) return;
            EnsureObjectBootstrapRegistered();

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
                shapeKind,
                in forward,
                halfAngleDeg,
                width,
                length,
                skillRuntimeHandle,
                groupKey,
                claimTargetsWithinGroup,
                innerRadius,
                claimUnitSubTypeMask);

            if (_areas.TryGetValue(areaId.Value, out var oldInfo))
            {
                _presentationCues?.Ended(in oldInfo, MobaExecutionEndReason.Replaced);
                PublishAreaLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    in oldInfo,
                    frame,
                    (int)MobaExecutionEndReason.Replaced);
                Unindex(oldInfo);
                EndAreaContext(in oldInfo, MobaExecutionEndReason.Replaced, frame);
                ReleaseSkillRuntime(areaId.Value);
            }

            _areas[areaId.Value] = info;
            RetainSkillRuntime(in info);
            _delayTriggeredAreas.Remove(areaId.Value);
            Index(_areasByOwner, ownerActorId, areaId.Value);
            Index(_areasByTemplate, templateId, areaId.Value);
            _presentationEvents.Add(new MobaAreaEventSnapshotEntry((int)AreaEventKind.Spawn, areaId.Value, ownerActorId, templateId, center.X, center.Y, center.Z, radius));
            _presentationCues?.Started(in info);
            _lifecycle?.RecordSpawn(MobaTemporaryEntityKind.Area, ActiveCount, frame);
            PublishAreaLifecycle(MobaRuntimeObjectLifecycleStage.Created, in info, frame);
        }

        public bool Unregister(AreaId areaId)
        {
            if (areaId.Value <= 0) return false;
            if (!_areas.TryGetValue(areaId.Value, out var info)) return false;

            _areas.Remove(areaId.Value);
            _delayTriggeredAreas.Remove(areaId.Value);
            Unindex(info);
            _presentationEvents.Add(new MobaAreaEventSnapshotEntry((int)AreaEventKind.Expire, info.AreaId, info.OwnerActorId, info.TemplateId, info.Center.X, info.Center.Y, info.Center.Z, info.Radius));
            _presentationCues?.Ended(in info, MobaExecutionEndReason.Completed);
            EndAreaContext(in info, MobaExecutionEndReason.Completed, CurrentFrame);
            ReleaseSkillRuntime(areaId.Value);
            _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Area, ActiveCount, CurrentFrame);
            PublishAreaLifecycle(
                MobaRuntimeObjectLifecycleStage.Destroyed,
                in info,
                CurrentFrame,
                (int)MobaExecutionEndReason.Completed);
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

            var transaction = new MobaTemporaryEntitySpawnTransaction();
            transaction.Enlist("area-lifecycle-diagnostic", () =>
                _lifecycle?.RecordDespawn(
                    MobaTemporaryEntityKind.Area,
                    ActiveCount,
                    CurrentFrame));
            transaction.Enlist("area-skill-retain", () => ReleaseSkillRuntime(areaId.Value));
            transaction.Enlist("area-indexes", () => Unindex(info));
            transaction.Enlist("area-delay-index", () => _delayTriggeredAreas.Remove(areaId.Value));
            transaction.Enlist("area-runtime", () => _areas.Remove(areaId.Value));
            transaction.Enlist("area-object-catalog", () =>
                PublishAreaLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    in info,
                    CurrentFrame,
                    (int)MobaExecutionEndReason.Failed));
            transaction.Enlist("area-presentation-cue", () =>
                _presentationCues?.Ended(in info, MobaExecutionEndReason.Failed));
            transaction.Enlist("area-context", () =>
                EndAreaContext(in info, MobaExecutionEndReason.Failed, CurrentFrame));
            transaction.Rollback();

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

        /// <summary>
        /// 查询本帧是否还有到期但未消费的区域延迟，不修改触发标记。
        /// </summary>
        public bool HasDueDelayAreas(int frame)
        {
            foreach (var pair in _areas)
                if (!_delayTriggeredAreas.Contains(pair.Key) && pair.Value.DelayTriggerFrame <= frame) return true;
            return false;
        }

        /// <summary>
        /// 冻结本轮开始时的候选身份，保持领域枚举顺序，不提前标记延迟已触发。
        /// </summary>
        public void CollectDelayCandidates(List<int> candidates)
        {
            candidates.Clear();
            foreach (var pair in _areas) candidates.Add(pair.Key);
        }

        /// <summary>
        /// Spawn 消费后采集并标记到期延迟；指定候选时排除本轮回调新建的对象。
        /// </summary>
        public int CollectDueDelayAreas(int frame, List<MobaAreaRuntimeInfo> results, IReadOnlyList<int> candidates = null)
        {
            if (results == null) return 0;
            results.Clear();

            if (candidates != null)
            {
                for (var i = 0; i < candidates.Count; i++)
                    if (_areas.TryGetValue(candidates[i], out var candidate)) Collect(candidate);
            }
            else foreach (var kv in _areas) Collect(kv.Value);

            return results.Count;

            void Collect(MobaAreaRuntimeInfo info)
            {
                var areaId = info.AreaId;
                if (_delayTriggeredAreas.Contains(areaId)) return;
                if (info.DelayTriggerFrame > frame) return;

                _delayTriggeredAreas.Add(areaId);
                results.Add(info);
            }

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
            if (_objectBootstrapRegistered)
            {
                _objectBootstrap?.Unregister(this);
                _objectBootstrapRegistered = false;
            }
            var diagnosticFrame = _frameTime != null ? _frameTime.Frame.Value : -1;
            foreach (var pair in _areas)
            {
                var info = pair.Value;
                PublishAreaLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    in info,
                    diagnosticFrame,
                    (int)MobaExecutionEndReason.Cancelled);
                _presentationCues?.Ended(in info, MobaExecutionEndReason.Cancelled);
                EndAreaContext(in info, MobaExecutionEndReason.Cancelled, diagnosticFrame);
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

        private void PublishAreaLifecycle(
            MobaRuntimeObjectLifecycleStage stage,
            in MobaAreaRuntimeInfo info,
            int frame,
            int endReason = 0)
        {
            EnsureObjectBootstrapRegistered();
            var hook = _objectLifecycle;
            if (hook == null || !hook.IsEnabled || info.AreaId <= 0) return;
            PublishAreaLifecycleTo(hook, stage, in info, frame, endReason);
        }

        void IMobaRuntimeObjectBootstrapContributor.CaptureActiveRuntimeObjects(
            IMobaRuntimeObjectLifecycleHook hook,
            int frame)
        {
            foreach (var info in _areas.Values)
            {
                PublishAreaLifecycleTo(
                    hook,
                    MobaRuntimeObjectLifecycleStage.Created,
                    in info,
                    frame);
            }
        }

        private static void PublishAreaLifecycleTo(
            IMobaRuntimeObjectLifecycleHook hook,
            MobaRuntimeObjectLifecycleStage stage,
            in MobaAreaRuntimeInfo info,
            int frame,
            int endReason = 0)
        {
            if (hook == null || !hook.IsEnabled || info.AreaId <= 0) return;
            var observation = new MobaRuntimeObjectLifecycleObservation(
                stage,
                MobaRuntimeObjectKind.Area,
                info.AreaId,
                frame,
                MobaRuntimeObjectDefinitionKind.Area,
                info.TemplateId,
                ownerActorId: info.OwnerActorId,
                sourceActorId: info.OwnerActorId,
                rootContextId: info.RootContextId,
                contextId: info.SourceContextId,
                endReason: endReason);
            hook.TryObserve(in observation);
        }

        private void EnsureObjectBootstrapRegistered()
        {
            if (_objectBootstrapRegistered) return;
            var registry = _objectBootstrap;
            if (registry != null && registry.Register(this))
                _objectBootstrapRegistered = true;
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

        private void EndAreaContext(in MobaAreaRuntimeInfo info, MobaExecutionEndReason reason, int frame)
        {
            if (_executionContexts == null) return;
            if (info.SourceContextId == 0L) return;
            _executionContexts.End(info.SourceContextId, (int)reason, frame);
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

    /// <summary>
    /// 拥有持久 AOE 特效提示。区域快照继续驱动模型和范围视图。
    /// </summary>
    [WorldService(typeof(MobaAreaPresentationCueReporter))]
    public sealed class MobaAreaPresentationCueReporter : MobaCueReporterBase
    {
        private readonly MobaConfigDatabase _configs;
        private MobaAreaRuntimeInfo _info;
        private MobaExecutionEndReason _reason;
        private AoeMO _aoe;

        public MobaAreaPresentationCueReporter(
            MobaConfigDatabase configs,
            MobaPresentationCueSnapshotService snapshots)
            : base(snapshots)
        {
            _configs = configs;
        }

        protected override PresentationCueDomain Domain => PresentationCueDomain.Aoe;

        public void Started(in MobaAreaRuntimeInfo info)
        {
            Report(MobaPresentationCueStage.Started, in info, MobaExecutionEndReason.None);
        }

        public void Ended(in MobaAreaRuntimeInfo info, MobaExecutionEndReason reason)
        {
            Report(MobaPresentationCueStage.Completed, in info, reason);
        }

        internal void ReconcileActiveAreas(IReadOnlyCollection<MobaAreaRuntimeInfo> areas)
        {
            var entries = new List<MobaPresentationCueSnapshotEntry>();
            if (areas != null)
                foreach (var info in areas)
                    if (TryBuildActiveEntry(in info, out var entry)) entries.Add(entry);
            ReconcileDesiredCueDomain(PresentationCueDomain.Aoe, entries);
        }

        private bool TryBuildActiveEntry(in MobaAreaRuntimeInfo info, out MobaPresentationCueSnapshotEntry entry)
        {
            entry = default;
            if (_configs == null || info.TemplateId <= 0 ||
                !_configs.TryGetAoe(info.TemplateId, out var aoe) || aoe == null || aoe.VfxId <= 0)
                return false;

            _info = info;
            _reason = MobaExecutionEndReason.None;
            _aoe = aoe;
            var draft = CueDraft.Create(PresentationCueDomain.Aoe, MobaPresentationCueStage.Started);
            draft.Class = PresentationCueClass.Lifecycle | PresentationCueClass.Placement | PresentationCueClass.Visual;
            return TryBuildEntry(ref draft, out entry);
        }

        private void Report(MobaPresentationCueStage stage, in MobaAreaRuntimeInfo info, MobaExecutionEndReason reason)
        {
            if (_configs == null || info.TemplateId <= 0) return;
            if (!_configs.TryGetAoe(info.TemplateId, out var aoe) || aoe == null || aoe.VfxId <= 0) return;

            _info = info;
            _reason = reason;
            _aoe = aoe;
            var draft = CueDraft.Create(PresentationCueDomain.Aoe, stage);
            draft.Class = PresentationCueClass.Lifecycle | PresentationCueClass.Placement | PresentationCueClass.Visual;
            Emit(ref draft);
        }

        protected override void Populate(ref CueDraft draft)
        {
            draft.TemplateId = _aoe.Id;
            draft.VfxId = _aoe.VfxId;
            draft.SourceActorId = _info.OwnerActorId;
            draft.TargetActorId = _info.OwnerActorId;
            draft.Targets = _info.OwnerActorId > 0 ? new[] { _info.OwnerActorId } : null;
            draft.Positions = new[] { _info.Center.X, _info.Center.Y, _info.Center.Z };
            draft.DurationMsOverride = _aoe.DurationMs;
            CueNumericParams.SetRadius(ref draft, _info.Radius > 0f ? _info.Radius : _aoe.Radius);
            draft.Parameters = new[]
            {
                CueParameterBag.Vector3(
                    PresentationCueParameterKeys.AreaForward,
                    _info.Forward.X,
                    _info.Forward.Y,
                    _info.Forward.Z),
                CueParameterBag.Integer(PresentationCueParameterKeys.AreaShapeKind, _info.ShapeKind),
                CueParameterBag.Float(PresentationCueParameterKeys.AreaHalfAngleDeg, _info.HalfAngleDeg),
                CueParameterBag.Float(PresentationCueParameterKeys.AreaWidth, _info.Width),
                CueParameterBag.Float(PresentationCueParameterKeys.AreaLength, _info.Length),
            };
            draft.InstanceId = _info.AreaId;
            draft.ContextKind = (int)EffectContextKind.Area;
            draft.OriginKind = (int)MobaExecutionKind.AreaSpawn;
            draft.SourceContextId = _info.SourceContextId;
            draft.RootContextId = _info.RootContextId;
            draft.OwnerContextId = _info.OwnerContextId;
            draft.SourceConfigId = _info.TemplateId;
            draft.ContextEventId = $"area:{_info.AreaId}";
            draft.LifecycleReason = (int)_reason;
        }

        protected override string ResolveInstanceKey(in CueDraft draft) => $"area:{_info.AreaId}";
    }

    public readonly struct MobaAreaRuntimeInfo
    {
        public readonly int AreaId;
        public readonly int TemplateId;
        public readonly int OwnerActorId;
        public readonly Vec3 Center;
        public readonly float Radius;
        public readonly float InnerRadius;
        public readonly int ShapeKind;
        public readonly Vec3 Forward;
        public readonly float HalfAngleDeg;
        public readonly float Width;
        public readonly float Length;
        public readonly int CollisionLayerMask;
        public readonly int MaxTargets;
        public readonly int SpawnFrame;
        public readonly int DelayTriggerFrame;
        public readonly long SourceContextId;
        public readonly long RootContextId;
        public readonly long OwnerContextId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;
        public readonly int GroupKey;
        public readonly bool ClaimTargetsWithinGroup;
        public readonly int ClaimUnitSubTypeMask;

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
            : this(
                areaId,
                templateId,
                ownerActorId,
                in center,
                radius,
                collisionLayerMask,
                maxTargets,
                spawnFrame,
                delayTriggerFrame,
                sourceContextId,
                rootContextId,
                ownerContextId,
                MobaAreaShapeDefaults.Circle,
                Vec3.Forward,
                0f,
                radius * 2f,
                radius * 2f,
                skillRuntimeHandle)
        {
        }

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
            int shapeKind,
            in Vec3 forward,
            float halfAngleDeg,
            float width,
            float length,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            int groupKey = 0,
            bool claimTargetsWithinGroup = false,
            float innerRadius = 0f,
            int claimUnitSubTypeMask = 0)
        {
            if (!MobaAreaClaimQualification.IsValidMask(claimUnitSubTypeMask))
                throw new ArgumentOutOfRangeException(nameof(claimUnitSubTypeMask));
            AreaId = areaId;
            TemplateId = templateId;
            OwnerActorId = ownerActorId;
            Center = center;
            Radius = radius;
            ShapeKind = MobaAreaShapeDefaults.Normalize(shapeKind);
            Forward = MobaAreaShapeDefaults.NormalizeForward(in forward);
            HalfAngleDeg = halfAngleDeg > 0f ? halfAngleDeg : 0f;
            Width = width > 0f ? width : radius * 2f;
            Length = length > 0f ? length : radius * 2f;
            InnerRadius = innerRadius > 0f ? Math.Min(innerRadius, radius) : 0f;
            CollisionLayerMask = collisionLayerMask;
            MaxTargets = maxTargets;
            SpawnFrame = spawnFrame;
            DelayTriggerFrame = delayTriggerFrame;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            GroupKey = groupKey;
            ClaimTargetsWithinGroup = claimTargetsWithinGroup;
            ClaimUnitSubTypeMask = claimUnitSubTypeMask;
        }
    }

    public static class MobaAreaClaimQualification
    {
        private const int DefinedMask = (1 << (int)UnitSubType.Bullet) - 1;

        public static bool IsValidMask(double mask) =>
            !double.IsNaN(mask) && !double.IsInfinity(mask) &&
            mask >= 0 && mask <= DefinedMask && mask == Math.Truncate(mask);

        public static bool Allows(int mask, UnitSubType subType)
        {
            var value = (int)subType;
            if (!IsValidMask(mask) || value <= 0 || value > (int)UnitSubType.Bullet) return false;
            var effectiveMask = mask == 0 ? 1 << ((int)UnitSubType.Hero - 1) : mask;
            return (effectiveMask & (1 << (value - 1))) != 0;
        }
    }

    internal sealed class MobaAreaRuntimeLocalRollbackState
    {
        internal MobaAreaRuntimeLocalRollbackState(
            MobaAreaRuntimeRollbackCheckpoint checkpoint,
            MobaAreaEventSnapshotEntry[] pendingEvents)
        {
            Checkpoint = checkpoint;
            PendingEvents = pendingEvents ?? Array.Empty<MobaAreaEventSnapshotEntry>();
        }

        internal MobaAreaRuntimeRollbackCheckpoint Checkpoint { get; }
        internal MobaAreaEventSnapshotEntry[] PendingEvents { get; }
    }

    internal static class MobaAreaShapeDefaults
    {
        public const int Circle = 0;

        public static int Normalize(int shapeKind)
        {
            return shapeKind >= 0 && shapeKind <= 3 ? shapeKind : Circle;
        }

        public static Vec3 NormalizeForward(in Vec3 forward)
        {
            if (forward.SqrMagnitude <= 0.000001f) return Vec3.Forward;
            return forward.Normalized;
        }
    }
}
