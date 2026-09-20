using System;
using AbilityKit.Core.Logging;
using AbilityKit.Game.View.Foundation;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleEventSubscriptionGroup : IDisposable
    {
        private readonly SubscriptionGroup<IDisposable> _inner;

        public BattleEventSubscriptionGroup(int capacity = 4)
        {
            _inner = new SubscriptionGroup<IDisposable>(
                subscription => subscription.Dispose(),
                ex => Log.Exception(ex),
                capacity);
        }

        public IDisposable Add(IDisposable subscription)
        {
            _inner.Add(subscription);
            return subscription;
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
