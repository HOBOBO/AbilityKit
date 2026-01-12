using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffTickSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IWorldClock _clock;
        private IEventBus _eventBus;
        private AbilityKit.Ability.Triggering.Runtime.ITriggerActionRunner _actionRunner;
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffTickSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _clock);
            Services.TryGet(out _eventBus);
            Services.TryGet(out _actionRunner);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.Buffs));
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

                    b.Remaining -= dt;
                    if (b.Remaining > 0f) continue;

                    try
                    {
                        _actionRunner?.CancelByOwner(b);
                    }
                    catch
                    {
                    }

                    if (_configs != null)
                    {
                        if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                        {
                            PublishBuffRemove(_eventBus, buff.EffectId, b.SourceId, e.actorId.Value, b.BuffId, b.StackCount);
                        }
                    }

                    list.RemoveAt(j);
                }
            }
        }

        private static void PublishBuffRemove(IEventBus bus, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount)
        {
            if (bus == null) return;

            PublishOnce(bus, "buff.remove", effectId, sourceActorId, targetActorId, buffId, stackCount);
            if (effectId > 0)
            {
                PublishOnce(bus, $"buff.remove.{effectId}", effectId, sourceActorId, targetActorId, buffId, stackCount);
            }
        }

        private static void PublishOnce(IEventBus bus, string eventId, int effectId, int sourceActorId, int targetActorId, int buffId, int stackCount)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args["buff.id"] = buffId;
            args["buff.effectId"] = effectId;
            args["buff.stackCount"] = stackCount;
            bus.Publish(new TriggerEvent(eventId, payload: null, args: args));
        }
    }
}
