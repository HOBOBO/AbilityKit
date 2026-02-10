using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffApplySystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private MobaOngoingEffectService _ongoing;
        private EffectSourceRegistry _effectSource;
        private MobaEffectExecutionService _effectExec;

        private BuffRepository _repo;
        private BuffStackingPolicyApplier _stacking;
        private BuffContextService _ctx;
        private BuffEventPublisher _events;
        private BuffOngoingEffectBinder _ongoingBinder;
        private BuffStageEffectExecutor _stageEffects;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffApplySystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _configs);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _actionRunner);
            Services.TryResolve(out _ongoing);
            Services.TryResolve(out _effectSource);
            Services.TryResolve(out _effectExec);

            Services.TryResolve(out IFrameTime frameTime);

            _repo = new BuffRepository();
            _stacking = new BuffStackingPolicyApplier();
            _ctx = new BuffContextService(_effectSource, _actionRunner, frameTime);
            _events = new BuffEventPublisher(_eventBus, _effectSource);
            _ongoingBinder = new BuffOngoingEffectBinder(_ongoing, _actionRunner);
            _stageEffects = new BuffStageEffectExecutor(_effectExec, Services, _eventBus);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.ApplyBuffRequest));
        }

        protected override void OnExecute()
        {
            if (_configs == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasApplyBuffRequest) continue;

                var req = e.applyBuffRequest;
                e.RemoveApplyBuffRequest();

                if (req.BuffId <= 0) continue;

                if (!_configs.TryGetBuff(req.BuffId, out var buff) || buff == null) continue;

                var list = _repo.GetOrCreateList(e);

                var duration = req.DurationOverrideMs > 0 ? req.DurationOverrideMs : buff.DurationMs;

                var durationSeconds = duration > 0 ? duration / 1000f : 0f;

                var targetActorId = e.actorId.Value;
                var existingIndex = BuffRepository.FindExistingBuffIndex(list, buff.Id);
                if (existingIndex >= 0)
                {
                    var rt = list[existingIndex];
                    var applied = _stacking.ApplyToExisting(rt, buff, req.SourceId, durationSeconds, _ctx);
                    var originSource = req.OriginSourceActorId > 0 ? (object)req.OriginSourceActorId : null;
                    var originTarget = req.OriginTargetActorId > 0 ? (object)req.OriginTargetActorId : null;
                    _ctx.EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, originSource: originSource, originTarget: originTarget, parentContextId: req.ParentContextId);
                    _ongoingBinder.TryStartOngoingEffectByBuff(buff, rt, req.SourceId, targetActorId);
                    TryUpsertOngoingTriggerPlans(e, rt.SourceContextId, buff);
                    _events.PublishApplyOrRefresh(buff, req.SourceId, targetActorId, durationSeconds, rt);
                    if (applied)
                    {
                        BuffStackingPolicyApplier.ResetInterval(rt, buff);
                        _stageEffects.Execute(buff.OnAddEffects, buff.Id, req.SourceId, targetActorId, rt.SourceContextId);
                        _events.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, runtime: rt);
                    }
                }
                else
                {
                    var rt = _stacking.CreateNewRuntime(buff, req.SourceId, durationSeconds);
                    var originSource = req.OriginSourceActorId > 0 ? (object)req.OriginSourceActorId : null;
                    var originTarget = req.OriginTargetActorId > 0 ? (object)req.OriginTargetActorId : null;
                    _ctx.EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, originSource: originSource, originTarget: originTarget, parentContextId: req.ParentContextId);
                    list.Add(rt);
                    _ongoingBinder.TryStartOngoingEffectByBuff(buff, rt, req.SourceId, targetActorId);
                    TryUpsertOngoingTriggerPlans(e, rt.SourceContextId, buff);
                    _events.PublishApplyOrRefresh(buff, req.SourceId, targetActorId, durationSeconds, rt);
                    _stageEffects.Execute(buff.OnAddEffects, buff.Id, req.SourceId, targetActorId, rt.SourceContextId);
                    _events.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, runtime: rt);
                }
            }
        }

        private static void TryUpsertOngoingTriggerPlans(global::ActorEntity e, long ownerKey, global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff)
        {
            if (e == null) return;
            if (ownerKey == 0) return;
            if (buff == null) return;

            if (buff.TriggerIds == null || buff.TriggerIds.Count == 0)
            {
                RemoveOngoingTriggerPlansEntry(e, ownerKey);
                return;
            }

            var ids = new int[buff.TriggerIds.Count];
            for (int i = 0; i < buff.TriggerIds.Count; i++) ids[i] = buff.TriggerIds[i];

            var oldList = e.hasOngoingTriggerPlans ? e.ongoingTriggerPlans.Active : null;
            var newList = oldList != null && oldList.Count > 0 ? new List<OngoingTriggerPlanEntry>(oldList.Count + 1) : new List<OngoingTriggerPlanEntry>(1);
            var replaced = false;

            if (oldList != null)
            {
                for (int i = 0; i < oldList.Count; i++)
                {
                    var it = oldList[i];
                    if (it == null) continue;
                    if (it.OwnerKey == ownerKey)
                    {
                        newList.Add(new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = ids });
                        replaced = true;
                    }
                    else
                    {
                        newList.Add(new OngoingTriggerPlanEntry { OwnerKey = it.OwnerKey, TriggerIds = it.TriggerIds });
                    }
                }
            }

            if (!replaced)
            {
                newList.Add(new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = ids });
            }

            var rev = e.hasOngoingTriggerPlans ? e.ongoingTriggerPlans.Revision + 1 : 1;
            if (e.hasOngoingTriggerPlans) e.ReplaceOngoingTriggerPlans(newList, rev);
            else e.AddOngoingTriggerPlans(newList, rev);
        }

        private static void RemoveOngoingTriggerPlansEntry(global::ActorEntity e, long ownerKey)
        {
            if (e == null) return;
            if (ownerKey == 0) return;
            if (!e.hasOngoingTriggerPlans) return;

            var oldList = e.ongoingTriggerPlans.Active;
            if (oldList == null || oldList.Count == 0) return;

            var newList = new List<OngoingTriggerPlanEntry>(oldList.Count);
            var removedAny = false;

            for (int i = 0; i < oldList.Count; i++)
            {
                var it = oldList[i];
                if (it == null) continue;
                if (it.OwnerKey == ownerKey)
                {
                    removedAny = true;
                    continue;
                }
                newList.Add(new OngoingTriggerPlanEntry { OwnerKey = it.OwnerKey, TriggerIds = it.TriggerIds });
            }

            if (!removedAny) return;

            var rev = e.ongoingTriggerPlans.Revision + 1;
            if (newList.Count == 0) e.RemoveOngoingTriggerPlans();
            else e.ReplaceOngoingTriggerPlans(newList, rev);
        }
    }
}
