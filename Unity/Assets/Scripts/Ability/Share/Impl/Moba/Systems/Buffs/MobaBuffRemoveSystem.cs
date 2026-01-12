using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsRemove, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffRemoveSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private EffectSourceRegistry _effectSource;
        private IFrameTime _frameTime;

        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffRemoveSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _eventBus);
            Services.TryGet(out _actionRunner);
            Services.TryGet(out _effectSource);
            Services.TryGet(out _frameTime);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.RemoveBuffRequest));
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

                    try
                    {
                        if (b.SourceContextId != 0)
                        {
                            _actionRunner?.CancelByOwnerKey(b.SourceContextId);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (b.SourceContextId != 0)
                        {
                            var reason = req.Reason;
                            if (reason == EffectSourceEndReason.None) reason = EffectSourceEndReason.Dispelled;
                            _effectSource?.End(b.SourceContextId, GetFrame(), reason);
                        }
                    }
                    catch
                    {
                    }

                    if (_configs != null)
                    {
                        if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                        {
                            var reason = req.Reason;
                            if (reason == EffectSourceEndReason.None) reason = EffectSourceEndReason.Dispelled;
                            PublishBuffRemove(_eventBus, buff.EffectId, req.SourceId, e.actorId.Value, b.BuffId, b.StackCount, b.SourceContextId, reason);
                        }
                    }

                    list.RemoveAt(j);
                }
            }
        }

        private static void PublishBuffRemove(IEventBus bus, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount, long sourceContextId, EffectSourceEndReason reason)
        {
            if (bus == null) return;

            PublishOnce(bus, "buff.remove", effectId, sourceActorId, targetActorId, buffId, stackCount, sourceContextId, reason);
            if (effectId > 0)
            {
                PublishOnce(bus, $"buff.remove.{effectId}", effectId, sourceActorId, targetActorId, buffId, stackCount, sourceContextId, reason);
            }
        }

        private static void PublishOnce(IEventBus bus, string eventId, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount, long sourceContextId, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args["buff.id"] = buffId;
            args["buff.effectId"] = effectId;
            args["buff.stackCount"] = stackCount;
            args["buff.removeReason"] = (int)reason;
            if (sourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = sourceContextId;
            }
            bus.Publish(new TriggerEvent(eventId, payload: null, args: args));
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
