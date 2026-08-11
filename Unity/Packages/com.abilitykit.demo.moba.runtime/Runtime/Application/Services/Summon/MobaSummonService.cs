using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Effect;
using AbilityKit.Core.Eventing;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Components;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSummonService))]
    public sealed class MobaSummonService : IService
    {
        [WorldInject] private ActorIdAllocator _actorIds = null;
        [WorldInject] private MobaActorRegistry _registry = null;
        [WorldInject] private MobaEntityManager _entities = null;
        [WorldInject] private AbilityKit.Demo.Moba.Services.EntityConstruction.ActorEntityInitPipeline _generator = null;
        [WorldInject] private MobaConfigDatabase _config = null;
        [WorldInject] private MobaComponentTemplateService _componentTemplates = null;
        [WorldInject] private AbilityKit.Triggering.Eventing.IEventBus _eventBus = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IWorldClock _clock = null;
        [WorldInject(required: false)] private MobaTraceRegistry _trace = null;
        [WorldInject(required: false)] private IMobaActorSpawnService _actorSpawn = null;
        [WorldInject(required: false)] private IMobaTemporaryEntityLifecycleService _lifecycle = null;
        [WorldInject(required: false)] private MobaSkillCastRuntimeService _skillRuntimes = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;

        private enum SummonOverflowPolicy
        {
            ReplaceOldest = 0,
            RejectNew = 1,
            ReplaceNewest = 2,
            AllowOverflow = 3,
        }

        private readonly Dictionary<int, List<int>> _summonsByRootOwner = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, SummonSourceContext> _sourceBySummonActorId = new Dictionary<int, SummonSourceContext>();
        private readonly Dictionary<int, MobaSkillRuntimeRetainHandle> _skillRuntimeRetainsBySummonActorId = new Dictionary<int, MobaSkillRuntimeRetainHandle>();
        private readonly List<int> _queryBuffer = new List<int>(16);

        public int ActiveCount => CountAllTrackedSummons();

        public bool TrySummon(int casterActorId, int summonId, in Vec3 pos)
        {
            return TrySummonInternal(casterActorId, summonId, in pos, hasForward: false, forward: default, sourceContext: default);
        }

        public bool TrySummon(int casterActorId, int summonId, in Vec3 pos, in Vec3 forward)
        {
            return TrySummonInternal(casterActorId, summonId, in pos, hasForward: true, forward: in forward, sourceContext: default);
        }

        public bool TrySummon(int casterActorId, int summonId, in Vec3 pos, in SummonSourceContext sourceContext)
        {
            return TrySummonInternal(casterActorId, summonId, in pos, hasForward: false, forward: default, sourceContext: in sourceContext);
        }

        public bool TrySummon(int casterActorId, int summonId, in Vec3 pos, in Vec3 forward, in SummonSourceContext sourceContext)
        {
            return TrySummonInternal(casterActorId, summonId, in pos, hasForward: true, forward: in forward, sourceContext: in sourceContext);
        }

        private bool TrySummonInternal(int casterActorId, int summonId, in Vec3 pos, bool hasForward, in Vec3 forward, in SummonSourceContext sourceContext)
        {
            if (casterActorId <= 0) return false;
            if (summonId <= 0) return false;
            if (_actorIds == null || _registry == null || _entities == null) return false;
            if (_config == null) return false;

            if (!_config.TryGetSummon(summonId, out var summon) || summon == null) return false;

            if (!_entities.TryGetActorEntity(casterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                return false;
            }

            if (_actorSpawn == null) return false;

            var rootOwner = OwnerLinkUtil.ResolveRootOwner(caster);
            if (rootOwner <= 0) rootOwner = casterActorId;
            if (!PrepareCapacityForSummon(rootOwner, summonId, summon.MaxAlivePerOwner, summon.OverflowPolicy))
            {
                _lifecycle?.RecordRejected(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
                Log.Warning($"[MobaSummonService] summon rejected by overflow policy. summonId={summonId} casterActorId={casterActorId} rootOwner={rootOwner} maxAlive={summon.MaxAlivePerOwner} policy={summon.OverflowPolicy}");
                return false;
            }

            var actorId = _actorIds.Next();

            var spec = MobaConverter.ToSummonActorBuildSpec(actorId, summonId, summon, caster, in pos, hasForward, in forward);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.PostSetup = new MobaActorSpawnPostSetup
            {
                SetOwnerLink = true,
                OwnerActorId = casterActorId,
                RootOwnerActorId = rootOwner,
                SetSummonMeta = true,
                SummonId = summonId,
                DespawnOnOwnerDie = summon.DespawnOnOwnerDie,
                SetLifetime = summon.LifetimeMs > 0,
                LifetimeEndTimeMs = summon.LifetimeMs > 0 ? NowMs() + summon.LifetimeMs : 0L,
                SetModelId = summon.ModelId > 0,
                ModelId = summon.ModelId,
                SetBrain = summon.BrainId > 0,
                BrainId = summon.BrainId,
                BrainOwnerActorId = rootOwner,
                BrainSourceKind = (int)MobaActorBuildSourceKind.Summon,
                BrainSourceId = summonId,
            };

            if (!_actorSpawn.TrySpawn(in request, out var spawnResult) || !spawnResult.Success)
            {
                _lifecycle?.RecordRejected(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
                Log.Warning($"[MobaSummonService] spawn failed. summonId={summonId} actorId={actorId} casterActorId={casterActorId} error={spawnResult.Error}");
                return false;
            }

            var spawnSourceContext = default(SummonSourceContext);
            var createdTraceContextId = 0L;
            try
            {
                var entity = spawnResult.Entity;
                if (entity == null)
                {
                    throw new InvalidOperationException(
                        $"Summon spawn returned no entity. summonId={summonId} actorId={actorId}");
                }

                spawnSourceContext = CreateSpawnSourceContext(
                    casterActorId,
                    actorId,
                    summonId,
                    in sourceContext,
                    out createdTraceContextId);

                if (_generator != null)
                {
                    _generator.InitializeFromAttributeTemplate(entity, summon.AttributeTemplateId);
                }

                // 继承属性配置：按 施法者属性 × Ratio + Add 覆写召唤物属性基础值
                TryApplyInheritAttributes(entity, caster, summon);

                TryApplyDefaultComponentTemplates(entity, summon.DefaultComponentTemplateIds);

                TryInitSkillLoadout(entity, summon.SkillIds, summon.PassiveSkillIds);

                TrackSummon(rootOwner, actorId);
                TrackSourceContext(actorId, in spawnSourceContext);
                RetainSkillRuntime(actorId, summonId, in spawnSourceContext);
                _lifecycle?.RecordSpawn(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
                CollectSummonSpawned(actorId, summonId, in spawnSourceContext);

                PublishSummonEvent(MobaSummonTriggering.Events.Spawned, rootOwner, casterActorId, actorId, summonId, (int)SummonDespawnReason.None, in spawnSourceContext);
                PublishSummonEvent(MobaSummonTriggering.Events.SpawnedByOwner(rootOwner), rootOwner, casterActorId, actorId, summonId, (int)SummonDespawnReason.None, in spawnSourceContext);

                return true;
            }
            catch (Exception ex)
            {
                CompensateFailedSpawn(
                    in spawnResult,
                    rootOwner,
                    actorId,
                    summonId,
                    in spawnSourceContext,
                    createdTraceContextId);
                _lifecycle?.RecordRejected(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
                Log.Exception(ex, $"[MobaSummonService] summon post-spawn initialization failed (summonId={summonId}, actorId={actorId}, casterActorId={casterActorId})");
                return false;
            }
        }

        private void TryApplyInheritAttributes(global::ActorEntity entity, global::ActorEntity caster, Config.BattleDemo.MO.SummonMO summon)
        {
            if (summon.InheritAttributeConfigId <= 0) return;
            if (_config == null) return;
            if (!_config.TryGetSummonAttrInherit(summon.InheritAttributeConfigId, out var inherit) || inherit == null) return;
            if (inherit.Scales == null || inherit.Scales.Count == 0) return;
            if (entity == null || caster == null) return;
            if (!entity.hasAttributeGroup || entity.attributeGroup.Group == null) return;
            if (!caster.hasAttributeGroup || caster.attributeGroup.Group == null) return;

            var summonGroup = entity.attributeGroup.Group;
            var casterGroup = caster.attributeGroup.Group;

            for (int i = 0; i < inherit.Scales.Count; i++)
            {
                var scale = inherit.Scales[i];
                if (scale.AttrId <= 0) continue;

                var attrId = Attributes.MobaAttributeIds.Get((BattleAttributeType)scale.AttrId);
                if (attrId.Equals(default(AbilityKit.Attributes.Core.AttributeId))) continue;

                var casterValue = casterGroup.GetValue(attrId);
                var inherited = casterValue * scale.Ratio + scale.Add;
                summonGroup.SetBase(attrId, inherited);
            }
        }

        private void TryApplyDefaultComponentTemplates(global::ActorEntity entity, IReadOnlyList<int> templateIds)
        {
            if (templateIds == null || templateIds.Count == 0) return;

            if (_componentTemplates == null)
            {
                throw new InvalidOperationException("MobaSummonService requires MobaComponentTemplateService when summon default component templates are configured.");
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int i = 0; i < templateIds.Count; i++)
            {
                var id = templateIds[i];
                if (id <= 0)
                {
                    throw new InvalidOperationException($"Summon default component template id must be positive. index={i}, templateId={id}");
                }

                _componentTemplates.Apply(entity, id);
            }
        }

        public bool TryDespawn(int summonActorId, SummonDespawnReason reason)
        {
            if (summonActorId <= 0) return false;
            if (_registry == null) return false;
            if (!_registry.TryGet(summonActorId, out var e) || e == null) return false;

            ActorLifecycleRequests.RequestDespawn(e, CurrentFrame, ToActorDespawnReason(reason));

            return true;
        }

        public bool ExecuteRequestedDespawn(int summonActorId, SummonDespawnReason reason)
        {
            if (summonActorId <= 0) return false;
            if (_registry == null) return false;

            if (!_registry.TryGet(summonActorId, out var e) || e == null) return false;

            var rootOwner = 0;
            var owner = 0;
            var summonId = 0;

            if (e.hasOwnerLink && e.ownerLink != null)
            {
                owner = e.ownerLink.OwnerActorId;
                rootOwner = e.ownerLink.RootOwnerActorId;
            }
            if (rootOwner <= 0) rootOwner = owner;
            if (e.hasSummonMeta && e.summonMeta != null) summonId = e.summonMeta.SummonId;

            new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                summonActorId,
                out _,
                publishDespawn: true);

            UntrackSummon(rootOwner, summonActorId);
            var sourceContext = ConsumeSourceContext(summonActorId);
            ReleaseSkillRuntime(summonActorId, summonId);
            EndSpawnTrace(in sourceContext, reason);
            _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
            if (reason == SummonDespawnReason.ReplacedByLimit) _lifecycle?.RecordReplaced(MobaTemporaryEntityKind.Summon, ActiveCount, CurrentFrame);
            CollectSummonEnded(summonActorId, summonId, (int)reason, in sourceContext);

            if (reason == SummonDespawnReason.Killed)
            {
                PublishSummonEvent(MobaSummonTriggering.Events.Died, rootOwner, owner, summonActorId, summonId, (int)reason, in sourceContext);
                if (rootOwner > 0)
                {
                    PublishSummonEvent(MobaSummonTriggering.Events.DiedByOwner(rootOwner), rootOwner, owner, summonActorId, summonId, (int)reason, in sourceContext);
                }
            }

            PublishSummonEvent(MobaSummonTriggering.Events.Despawned, rootOwner, owner, summonActorId, summonId, (int)reason, in sourceContext);
            if (rootOwner > 0)
            {
                PublishSummonEvent(MobaSummonTriggering.Events.DespawnedByOwner(rootOwner), rootOwner, owner, summonActorId, summonId, (int)reason, in sourceContext);
            }

            try { e.Destroy(); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] destroy summon entity failed (summonActorId={summonActorId}, summonId={summonId})"); }

            return true;
        }

        public int GetAliveCount(int rootOwnerActorId, int summonId = 0)
        {
            _queryBuffer.Clear();
            TryGetSummons(rootOwnerActorId, _queryBuffer, summonId);
            return _queryBuffer.Count;
        }

        public bool TryGetSummons(int rootOwnerActorId, List<int> results, int summonId = 0)
        {
            if (results == null) return false;
            results.Clear();
            if (rootOwnerActorId <= 0) return false;
            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null) return false;

            CompactTrackedSummons(rootOwnerActorId, list);
            for (var i = 0; i < list.Count; i++)
            {
                var actorId = list[i];
                if (!IsTrackedSummon(actorId, summonId)) continue;
                results.Add(actorId);
            }

            return results.Count > 0;
        }

        public int RemoveSummons(int rootOwnerActorId, int summonId, bool removeAll, SummonDespawnReason reason)
        {
            if (rootOwnerActorId <= 0) return 0;
            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null) return 0;

            CompactTrackedSummons(rootOwnerActorId, list);
            _queryBuffer.Clear();
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var actorId = list[i];
                if (!IsTrackedSummon(actorId, summonId)) continue;
                _queryBuffer.Add(actorId);
                if (!removeAll) break;
            }

            var removed = 0;
            for (var i = 0; i < _queryBuffer.Count; i++)
            {
                if (TryDespawn(_queryBuffer[i], reason)) removed++;
            }

            _queryBuffer.Clear();
            return removed;
        }

        private int CountAllTrackedSummons()
        {
            var count = 0;
            foreach (var kv in _summonsByRootOwner)
            {
                var list = kv.Value;
                if (list == null) continue;
                for (var i = 0; i < list.Count; i++)
                {
                    if (IsTrackedSummon(list[i], 0)) count++;
                }
            }

            return count;
        }

        private void TrackSummon(int rootOwnerActorId, int summonActorId)
        {
            if (rootOwnerActorId <= 0) return;

            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null)
            {
                list = new List<int>(8);
                _summonsByRootOwner[rootOwnerActorId] = list;
            }

            if (!list.Contains(summonActorId)) list.Add(summonActorId);
        }

        private bool PrepareCapacityForSummon(int rootOwnerActorId, int summonId, int maxAlivePerOwner, int overflowPolicy)
        {
            if (rootOwnerActorId <= 0) return false;
            if (maxAlivePerOwner <= 0) return true;

            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null)
            {
                return true;
            }

            CompactTrackedSummons(rootOwnerActorId, list);
            var count = CountTrackedSummons(list, summonId);
            if (count < maxAlivePerOwner) return true;

            var policy = (SummonOverflowPolicy)overflowPolicy;
            switch (policy)
            {
                case SummonOverflowPolicy.RejectNew:
                    return false;
                case SummonOverflowPolicy.AllowOverflow:
                    return true;
                case SummonOverflowPolicy.ReplaceNewest:
                    return TryDespawn(FindNewestTrackedSummon(list, summonId), SummonDespawnReason.ReplacedByLimit);
                case SummonOverflowPolicy.ReplaceOldest:
                default:
                    return TryDespawn(FindOldestTrackedSummon(list, summonId), SummonDespawnReason.ReplacedByLimit);
            }
        }

        private void UntrackSummon(int rootOwnerActorId, int summonActorId)
        {
            if (rootOwnerActorId <= 0) return;
            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null) return;
            list.Remove(summonActorId);
            if (list.Count == 0) _summonsByRootOwner.Remove(rootOwnerActorId);
        }

        private void TrackSourceContext(int summonActorId, in SummonSourceContext sourceContext)
        {
            if (summonActorId <= 0) return;
            if (!sourceContext.IsValid) return;
            _sourceBySummonActorId[summonActorId] = sourceContext;
        }

        private SummonSourceContext ConsumeSourceContext(int summonActorId)
        {
            if (summonActorId <= 0) return default;
            if (!_sourceBySummonActorId.TryGetValue(summonActorId, out var sourceContext)) return default;
            _sourceBySummonActorId.Remove(summonActorId);
            return sourceContext;
        }

        private bool RetainSkillRuntime(int summonActorId, int summonId, in SummonSourceContext sourceContext)
        {
            if (summonActorId <= 0) return false;
            if (_skillRuntimes == null) return false;
            if (!sourceContext.SkillRuntimeHandle.IsValid) return false;
            if (_skillRuntimeRetainsBySummonActorId.ContainsKey(summonActorId)) return true;

            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Summon, summonActorId, sourceContext.SourceContextId, summonId);
            var runtimeHandle = sourceContext.SkillRuntimeHandle;
            if (_skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle))
            {
                _skillRuntimeRetainsBySummonActorId[summonActorId] = retainHandle;
                return true;
            }

            return false;
        }

        private void ReleaseSkillRuntime(int summonActorId, int summonId)
        {
            if (summonActorId <= 0) return;
            if (!_skillRuntimeRetainsBySummonActorId.TryGetValue(summonActorId, out var retainHandle)) return;
            _skillRuntimeRetainsBySummonActorId.Remove(summonActorId);

            try
            {
                _skillRuntimes?.ReleaseChild(in retainHandle);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaSummonService] Release skill runtime retain failed (summonActorId={summonActorId}, summonId={summonId})");
            }
        }

        private void ReleaseAllSkillRuntimes()
        {
            if (_skillRuntimeRetainsBySummonActorId.Count == 0) return;

            foreach (var kv in _skillRuntimeRetainsBySummonActorId)
            {
                var summonActorId = kv.Key;
                var retainHandle = kv.Value;
                try
                {
                    _skillRuntimes?.ReleaseChild(in retainHandle);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaSummonService] Release skill runtime retain failed during dispose (summonActorId={summonActorId})");
                }
            }

            _skillRuntimeRetainsBySummonActorId.Clear();
        }

        private void EndSpawnTrace(in SummonSourceContext sourceContext, SummonDespawnReason reason)
        {
            EndSpawnTrace(sourceContext.SourceContextId, reason);
        }

        private void EndSpawnTrace(long sourceContextId, SummonDespawnReason reason)
        {
            if (_trace == null) return;
            if (sourceContextId == 0L) return;
            _trace.EndContext(sourceContextId, ToTraceReason(reason));
        }

        private void CompensateFailedSpawn(
            in MobaActorSpawnResult spawnResult,
            int rootOwnerActorId,
            int summonActorId,
            int summonId,
            in SummonSourceContext createdSourceContext,
            long createdTraceContextId)
        {
            try { ReleaseSkillRuntime(summonActorId, summonId); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] rollback skill-runtime release failed (summonActorId={summonActorId}, summonId={summonId})"); }

            var trackedSourceContext = default(SummonSourceContext);
            try { trackedSourceContext = ConsumeSourceContext(summonActorId); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] rollback source-context removal failed (summonActorId={summonActorId}, summonId={summonId})"); }
            try { UntrackSummon(rootOwnerActorId, summonActorId); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] rollback summon untracking failed (summonActorId={summonActorId}, summonId={summonId})"); }

            var traceContextId = trackedSourceContext.SourceContextId != 0L
                ? trackedSourceContext.SourceContextId
                : createdSourceContext.SourceContextId != 0L
                    ? createdSourceContext.SourceContextId
                    : createdTraceContextId;
            try { EndSpawnTrace(traceContextId, SummonDespawnReason.SceneCleanup); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] rollback trace end failed (summonActorId={summonActorId}, summonId={summonId})"); }

            try
            {
                new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                    summonActorId,
                    out _,
                    publishDespawn: false);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaSummonService] rollback actor unregister failed (summonActorId={summonActorId}, summonId={summonId})");
                return;
            }

            try { ActorSpawnPipeline.DestroyBuiltEntity(spawnResult.Entity); }
            catch (Exception ex) { Log.Exception(ex, $"[MobaSummonService] rollback entity destroy failed (summonActorId={summonActorId}, summonId={summonId})"); }
        }

        private void CompactTrackedSummons(int rootOwnerActorId, List<int> list)
        {
            if (list == null) return;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (IsTrackedSummon(list[i], 0)) continue;
                list.RemoveAt(i);
            }

            if (list.Count == 0 && rootOwnerActorId > 0)
            {
                _summonsByRootOwner.Remove(rootOwnerActorId);
            }
        }

        private int CountTrackedSummons(List<int> list, int summonId)
        {
            if (list == null || list.Count == 0) return 0;
            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (IsTrackedSummon(list[i], summonId)) count++;
            }

            return count;
        }

        private int FindOldestTrackedSummon(List<int> list, int summonId)
        {
            if (list == null) return 0;
            for (var i = 0; i < list.Count; i++)
            {
                var actorId = list[i];
                if (IsTrackedSummon(actorId, summonId)) return actorId;
            }

            return 0;
        }

        private int FindNewestTrackedSummon(List<int> list, int summonId)
        {
            if (list == null) return 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var actorId = list[i];
                if (IsTrackedSummon(actorId, summonId)) return actorId;
            }

            return 0;
        }

        private bool IsTrackedSummon(int summonActorId, int summonId)
        {
            if (summonActorId <= 0 || _registry == null) return false;
            if (!_registry.TryGet(summonActorId, out var e) || e == null) return false;
            if (!e.hasSummonMeta || e.summonMeta == null) return false;
            return summonId <= 0 || e.summonMeta.SummonId == summonId;
        }

        private void PublishSummonEvent(string eventId, int rootOwnerActorId, int ownerActorId, int summonActorId, int summonId, int reason, in SummonSourceContext sourceContext)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var payload = new SummonEventPayload
            {
                SummonActorId = summonActorId,
                SummonId = summonId,
                OwnerActorId = ownerActorId,
                RootOwnerActorId = rootOwnerActorId,
                Reason = reason,
                SourceContext = sourceContext,
            };

            var eid = TriggeringIdUtil.GetEventEid(eventId);
            _eventBus.Publish(new EventKey<SummonEventPayload>(eid), in payload);
            var objectKey = new EventKey<object>(eid);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = payload;
                _eventBus.Publish(objectKey, in boxed);
            }
        }

        private SummonSourceContext CreateSpawnSourceContext(int casterActorId, int summonActorId, int summonId, in SummonSourceContext sourceContext, out long createdTraceContextId)
        {
            createdTraceContextId = 0L;
            var origin = sourceContext.TryGetOrigin(out var sourceOrigin)
                ? sourceOrigin.WithActors(casterActorId, summonActorId)
                : new MobaGameplayOrigin(
                    casterActorId,
                    summonActorId,
                    MobaTraceKind.SummonSpawn,
                    summonId,
                    0,
                    0,
                    0,
                    0,
                    default);

            var parentContextId = origin.EffectiveParentContextId;
            var spawnContextId = 0L;
            if (_trace != null)
            {
                spawnContextId = parentContextId != 0L
                    ? _trace.CreateChildContext(parentContextId, MobaTraceKind.SummonSpawn, summonId, casterActorId, summonActorId, TraceEndpoint.Actor(casterActorId), TraceEndpoint.Actor(summonActorId))
                    : _trace.CreateRootContext(MobaTraceKind.SummonSpawn, summonId, casterActorId, summonActorId, TraceEndpoint.Actor(casterActorId), TraceEndpoint.Actor(summonActorId));
                createdTraceContextId = spawnContextId;
            }

            if (spawnContextId == 0L)
            {
                throw new InvalidOperationException($"Summon spawn requires trace context. casterActorId={casterActorId} summonActorId={summonActorId} summonId={summonId} parentContextId={parentContextId}");
            }

            origin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in origin)
                .WithActors(casterActorId, summonActorId)
                .WithLifecycleNode(MobaTraceKind.SummonSpawn, summonId, spawnContextId)
                .WithRootContext(origin.EffectiveRootContextId != 0L ? origin.EffectiveRootContextId : spawnContextId)
                .WithOwnerContext(origin.OwnerContextId != 0L ? origin.OwnerContextId : spawnContextId)
                .Build();

            return SummonSourceContextBuilder.Create()
                .WithActors(casterActorId, summonActorId)
                .WithSummonConfig(summonId)
                .WithSourceContext(spawnContextId)
                .WithRootContext(origin.EffectiveRootContextId)
                .WithOwnerContext(origin.OwnerContextId)
                .WithOrigin(in origin)
                .Build();
        }

        private void TryInitSkillLoadout(global::ActorEntity entity, IReadOnlyList<int> skillIds, IReadOnlyList<int> passiveSkillIds)
        {
            if (entity == null) return;

            var active = CreateActiveSkillRuntimes(skillIds);
            var passive = CreatePassiveSkillRuntimes(passiveSkillIds);

            if (entity.hasSkillLoadout) entity.ReplaceSkillLoadout(active, passive);
            else entity.AddSkillLoadout(active, passive);
        }

        private static AbilityKit.Demo.Moba.Components.ActiveSkillRuntime[] CreateActiveSkillRuntimes(IReadOnlyList<int> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return Array.Empty<AbilityKit.Demo.Moba.Components.ActiveSkillRuntime>();
            var list = new List<AbilityKit.Demo.Moba.Components.ActiveSkillRuntime>(skillIds.Count);
            for (int i = 0; i < skillIds.Count; i++)
            {
                var id = skillIds[i];
                if (id <= 0) continue;
                list.Add(new AbilityKit.Demo.Moba.Components.ActiveSkillRuntime { SkillId = id, Level = 1, CooldownDurationMs = 0, CooldownEndTimeMs = 0L });
            }
            return list.Count == 0 ? Array.Empty<AbilityKit.Demo.Moba.Components.ActiveSkillRuntime>() : list.ToArray();
        }

        private static AbilityKit.Demo.Moba.Components.PassiveSkillRuntime[] CreatePassiveSkillRuntimes(IReadOnlyList<int> passiveSkillIds)
        {
            if (passiveSkillIds == null || passiveSkillIds.Count == 0) return Array.Empty<AbilityKit.Demo.Moba.Components.PassiveSkillRuntime>();
            var list = new List<AbilityKit.Demo.Moba.Components.PassiveSkillRuntime>(passiveSkillIds.Count);
            for (int i = 0; i < passiveSkillIds.Count; i++)
            {
                var id = passiveSkillIds[i];
                if (id <= 0) continue;
                list.Add(new AbilityKit.Demo.Moba.Components.PassiveSkillRuntime { PassiveSkillId = id, Level = 1, CooldownDurationMs = 0, CooldownEndTimeMs = 0L });
            }
            return list.Count == 0 ? Array.Empty<AbilityKit.Demo.Moba.Components.PassiveSkillRuntime>() : list.ToArray();
        }

        private static ActorDespawnReason ToActorDespawnReason(SummonDespawnReason reason)
        {
            switch (reason)
            {
                case SummonDespawnReason.Timeout:
                    return ActorDespawnReason.SummonTimeout;
                case SummonDespawnReason.OwnerDead:
                    return ActorDespawnReason.SummonOwnerDead;
                case SummonDespawnReason.ReplacedByLimit:
                    return ActorDespawnReason.SummonReplacedByLimit;
                case SummonDespawnReason.ManualRemove:
                    return ActorDespawnReason.SummonManualRemove;
                case SummonDespawnReason.Killed:
                    return ActorDespawnReason.SummonKilled;
                case SummonDespawnReason.SceneCleanup:
                    return ActorDespawnReason.SceneCleanup;
                case SummonDespawnReason.None:
                default:
                    return ActorDespawnReason.Unknown;
            }
        }

        private static TraceLifecycleReason ToTraceReason(SummonDespawnReason reason)
        {
            switch (reason)
            {
                case SummonDespawnReason.OwnerDead:
                case SummonDespawnReason.Killed:
                    return TraceLifecycleReason.Dead;
                case SummonDespawnReason.ReplacedByLimit:
                    return TraceLifecycleReason.Replaced;
                case SummonDespawnReason.SceneCleanup:
                    return TraceLifecycleReason.Cancelled;
                case SummonDespawnReason.ManualRemove:
                    return TraceLifecycleReason.Dispelled;
                case SummonDespawnReason.Timeout:
                case SummonDespawnReason.None:
                default:
                    return TraceLifecycleReason.Completed;
            }
        }

        private int CurrentFrame
        {
            get
            {
                if (_frameTime != null) return _frameTime.Frame.Value;
                throw new InvalidOperationException("MobaSummonService requires IFrameTime for current frame.");
            }
        }

        private long NowMs()
        {
            if (_frameTime != null)
            {
                return (long)System.MathF.Round(_frameTime.Time * 1000f);
            }
            if (_clock != null)
            {
                return (long)System.MathF.Round(_clock.Time * 1000f);
            }
            throw new InvalidOperationException("MobaSummonService requires IFrameTime or IWorldClock for current time.");
        }

        internal static MobaBattleDiagnosticEventDraft CreateSummonSpawnedDraft(
            int summonActorId,
            int summonConfigId,
            in SummonSourceContext sourceContext)
        {
            sourceContext.TryGetOrigin(out var resolvedOrigin);
            var handle = sourceContext.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : sourceContext.RootContextId;
            var contextId = sourceContext.SourceContextId != 0L
                ? sourceContext.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = $"summonConfigId={summonConfigId}, summonActorId={summonActorId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.SummonSpawned,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceContext.SourceActorId,
                summonActorId,
                summonConfigId,
                rootContextId,
                contextId,
                runtime,
                summary: summary);
        }

        internal static MobaBattleDiagnosticEventDraft CreateSummonEndedDraft(
            int summonActorId,
            int summonConfigId,
            int despawnReason,
            in SummonSourceContext sourceContext)
        {
            sourceContext.TryGetOrigin(out var resolvedOrigin);
            var handle = sourceContext.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : sourceContext.RootContextId;
            var contextId = sourceContext.SourceContextId != 0L
                ? sourceContext.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = $"summonConfigId={summonConfigId}, summonActorId={summonActorId}, despawnReason={despawnReason}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.SummonEnded,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceContext.SourceActorId,
                summonActorId,
                summonConfigId,
                rootContextId,
                contextId,
                runtime,
                summary: summary);
        }

        private void CollectSummonSpawned(
            int summonActorId,
            int summonConfigId,
            in SummonSourceContext sourceContext)
        {
            if (_eventCollector == null) return;
            if (!sourceContext.IsValid) return;

            try
            {
                var draft = CreateSummonSpawnedDraft(summonActorId, summonConfigId, in sourceContext);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响召唤生成流程，静默吞掉异常。
            }
        }

        private void CollectSummonEnded(
            int summonActorId,
            int summonConfigId,
            int despawnReason,
            in SummonSourceContext sourceContext)
        {
            if (_eventCollector == null) return;
            if (!sourceContext.IsValid) return;

            try
            {
                var draft = CreateSummonEndedDraft(summonActorId, summonConfigId, despawnReason, in sourceContext);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响召唤销毁流程，静默吞掉异常。
            }
        }

        public void Dispose()
        {
            _summonsByRootOwner.Clear();
            _sourceBySummonActorId.Clear();
            ReleaseAllSkillRuntimes();
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Summon, 0, CurrentFrame);
        }
    }
}

