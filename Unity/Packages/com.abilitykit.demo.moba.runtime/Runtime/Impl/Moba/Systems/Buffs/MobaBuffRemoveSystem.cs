using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Effect;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Triggering.Eventing;
using EffectSourceRegistry = AbilityKit.Ability.Impl.Moba.EffectSource.MobaTraceRegistry;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsRemove, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffRemoveSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private MobaPeriodicEffectService _ongoing;
        private EffectSourceRegistry _effectSource;
        private MobaEffectInvokerService _invoker;

        private BuffContextService _ctx;
        private BuffEventPublisher _events;
        private BuffStageEffectExecutor _stageEffects;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffRemoveSystem(global::Entitas.IContexts contexts, IWorldResolver services)
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
            Services.TryResolve(out _invoker);

            Services.TryResolve(out IFrameTime frameTime);

            _ctx = new BuffContextService(_effectSource, _actionRunner, frameTime);
            _events = new BuffEventPublisher(_eventBus);
            _stageEffects = new BuffStageEffectExecutor(_invoker);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.RemoveBuffRequest));
        }

        protected override void OnExecute()
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasRemoveBuffRequest) continue;

                var req = e.removeBuffRequest;
                e.RemoveRemoveBuffRequest();

                if (req.BuffId <= 0) continue;
                if (!e.hasBuffs) continue;

                var list = e.buffs.Active;
                if (list == null || list.Count == 0) continue;

                for (int j = list.Count - 1; j >= 0; j--)
                {
                    var b = list[j];
                    if (b == null) continue;
                    if (b.BuffId != req.BuffId) continue;

                    var reason = req.Reason;
                    if (reason == EffectSourceEndReason.None) reason = EffectSourceEndReason.Dispelled;
                    _ctx?.EndByRuntimeNoClear(b, reason);

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
                            _events?.PublishRemove(buff, req.SourceId, e.actorId.Value, b, reason);
                            _stageEffects?.Execute(buff.OnRemoveEffects, buff.Id, req.SourceId, e.actorId.Value, b.SourceContextId);
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
    }
}
