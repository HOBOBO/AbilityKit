using System;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudEntityLifecycleBinding : IDisposable
    {
        private readonly BattleHudEntityLifecycleSubscriptionFactory _subscriptions;
        private IDisposable _subscription;
        private BattleHudBinder _binder;

        public BattleHudEntityLifecycleBinding(BattleHudEntityLifecycleSubscriptionFactory subscriptions = null)
        {
            _subscriptions = subscriptions ?? new BattleHudEntityLifecycleSubscriptionFactory();
        }

        public void Bind(IBattleEntityContext ctx, BattleHudBinder binder)
        {
            Dispose();

            _binder = binder;
            if (ctx?.EntityWorld == null) return;

            _subscription = _subscriptions.SubscribeDestroyed(ctx.EntityWorld, OnEntityDestroyed);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            _binder = null;
        }

        private void OnEntityDestroyed(EC.EntityDestroyed evt)
        {
            _binder?.OnEntityDestroyed(evt.EntityId);
        }
    }

    internal sealed class BattleHudEntityLifecycleSubscriptionFactory
    {
        public IDisposable SubscribeDestroyed(
            IECWorld world,
            Action<EC.EntityDestroyed> handler)
        {
            if (world == null) return null;
            return world.EntityDestroyed(handler);
        }
    }
}
