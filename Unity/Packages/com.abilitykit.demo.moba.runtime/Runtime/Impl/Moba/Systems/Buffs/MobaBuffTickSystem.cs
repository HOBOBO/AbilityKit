using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Impl.Moba;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffTickSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IWorldClock _clock;
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private MobaPeriodicEffectService _ongoing;
        private EffectSourceRegistry _effectSource;
        private MobaEffectInvokerService _invoker;
        private BuffContextService _buffContext;
        private BuffEventPublisher _buffEvents;
        private BuffStageEffectExecutor _stageEffects;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _configs);
            Services.TryResolve(out _clock);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _actionRunner);
            Services.TryResolve(out _ongoing);
            Services.TryResolve(out _effectSource);
            Services.TryResolve(out _invoker);

            Services.TryResolve(out IFrameTime frameTime);

            _buffContext = new BuffContextService(_effectSource, _actionRunner, frameTime);
            _buffEvents = new BuffEventPublisher(_eventBus);
            _stageEffects = new BuffStageEffectExecutor(_invoker);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.Buffs));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;
            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasBuffs) continue;

                var list = e.buffs.Active;
                if (list == null || list.Count == 0) continue;

                for (int j = list.Count - 1; j >= 0; j--)
                {
                    var b = list[j];
                    if (b == null)
                    {
                        list.RemoveAt(j);
                        continue;
                    }

                    // interval tick
                    if (_configs != null && _configs.TryGetBuff(b.BuffId, out var buffCfg) && buffCfg != null)
                    {
                        TryIntervalTick(buffCfg, b, e.actorId.Value, dt);
                    }

                    b.Remaining -= dt;
                    if (b.Remaining > 0f) continue;

                    _buffContext?.EndByRuntimeNoClear(b, EffectSourceEndReason.Expired);

                    var ownerKey = b.SourceContextId;
                    if (ownerKey != 0)
                    {
                        try
                        {
                            _ongoing?.StopByOwnerKey(e.actorId.Value, ownerKey);
                        }
                        catch
                        {
                        }
                    }

                    if (b.SourceContextId != 0)
                    {
                        RemoveOngoingTriggerPlansEntry(e, b.SourceContextId);
                    }

                    if (_configs != null)
                    {
                        if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                        {
                            _buffEvents?.PublishRemove(buff, b.SourceId, e.actorId.Value, b, EffectSourceEndReason.Expired);
                            _stageEffects?.Execute(buff.OnRemoveEffects, buff.Id, b.SourceId, e.actorId.Value, b.SourceContextId);
                        }
                    }

                    if (b.SourceContextId != 0 && e.hasEffectListeners)
                    {
                        var listeners = e.effectListeners.Active;
                        if (listeners != null && listeners.Count > 0)
                        {
                            for (int k = listeners.Count - 1; k >= 0; k--)
                            {
                                var l = listeners[k];
                                if (l == null) continue;
                                if (l.SourceContextId != b.SourceContextId) continue;
                                listeners.RemoveAt(k);
                            }
                        }
                    }

                    b.SourceContextId = 0;

                    list.RemoveAt(j);
                }
            }
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

        private void TryIntervalTick(BuffMO buff, BuffRuntime rt, int targetActorId, float dt)
        {
            if (buff == null) return;
            if (rt == null) return;
            if (buff.IntervalMs <= 0) return;
            if (buff.OnIntervalEffects == null || buff.OnIntervalEffects.Count == 0) return;

            rt.IntervalRemainingSeconds -= dt;
            if (rt.IntervalRemainingSeconds > 0f) return;

            // reset first to avoid re-entrancy issues
            rt.IntervalRemainingSeconds = buff.IntervalMs / 1000f;

            _stageEffects?.Execute(buff.OnIntervalEffects, buff.Id, rt.SourceId, targetActorId, rt.SourceContextId);
            _buffEvents?.PublishInterval(buff, rt.SourceId, targetActorId, rt);
            _buffEvents?.PublishPerEffect(MobaBuffTriggering.Events.Interval, buff.OnIntervalEffects, stage: "interval", sourceActorId: rt.SourceId, targetActorId: targetActorId, runtime: rt);
        }
    }
}

