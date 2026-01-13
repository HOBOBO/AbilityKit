using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectExecuteSubscriber : IService, IEventHandler
    {
        private readonly IEventBus _eventBus;
        private IEventSubscription _sub;

        public MobaEffectExecuteSubscriber(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _sub = _eventBus?.Subscribe(MobaTriggerEventIds.EffectExecute, this);
        }

        public void Handle(in TriggerEvent evt)
        {
            var bus = _eventBus;
            if (bus == null) return;

            if (evt.Args == null) return;
            if (!evt.Args.TryGetValue("effect.id", out var effectIdObj) || effectIdObj is not int effectId || effectId <= 0)
            {
                return;
            }

            Publish(bus, MobaTriggerEventIds.EffectApply, in evt, effectId);
            Publish(bus, MobaTriggerEventIds.EffectApplyById(effectId), in evt, effectId);
        }

        private static void Publish(IEventBus bus, string eventId, in TriggerEvent evt, int effectId)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            try
            {
                if (evt.Args != null)
                {
                    foreach (var kv in evt.Args)
                    {
                        if (kv.Key == null) continue;
                        args[kv.Key] = kv.Value;
                    }
                }

                args["effect.id"] = effectId;

                bus.Publish(new TriggerEvent(eventId, payload: evt.Payload, args: args));
            }
            catch
            {
                args.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            var s = _sub;
            if (s != null)
            {
                _sub = null;
                s.Unsubscribe();
            }
        }
    }
}
