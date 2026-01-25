using System;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Share.Effect
{
    public sealed class EventBusEffectEventSink : IEffectEventSink
    {
        private readonly IEventBus _bus;

        public EventBusEffectEventSink(IEventBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void Publish(string eventId, object payload = null, Action<PooledTriggerArgs> fillArgs = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            try
            {
                fillArgs?.Invoke(args);
                _bus.Publish(new TriggerEvent(eventId, payload, args));
            }
            catch
            {
                args.Dispose();
                throw;
            }
        }
    }
}
