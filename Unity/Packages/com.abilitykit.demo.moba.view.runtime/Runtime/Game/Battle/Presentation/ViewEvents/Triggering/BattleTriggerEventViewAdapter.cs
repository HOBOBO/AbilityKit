using System;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Triggering
{
    internal sealed class BattleTriggerEventViewAdapter : IDisposable
    {
        private IDisposable _subscription;

        public BattleTriggerEventViewAdapter(IDisposable subscription)
        {
            _subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
