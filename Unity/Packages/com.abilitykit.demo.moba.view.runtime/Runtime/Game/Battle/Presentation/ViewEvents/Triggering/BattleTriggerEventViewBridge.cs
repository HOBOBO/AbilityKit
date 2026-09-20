using System;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Triggering
{
    public sealed class BattleTriggerEventViewBridge : IDisposable
    {
        private readonly IEventBus _bus;
        private readonly IBattleViewEventSink _sink;
        private readonly BattleEventSubscriptionGroup _subscriptions = new BattleEventSubscriptionGroup(8);
        private bool _disposed;

        public BattleTriggerEventViewBridge(IEventBus bus, IBattleViewEventSink sink)
        {
            _bus = bus;
            _sink = sink;

            if (_bus == null) return;

            _subscriptions.Add(_bus.Subscribe(
                new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)),
                HandleDamageResult));
            _subscriptions.Add(_bus.Subscribe(
                new EventKey<ProjectileHitEvent>(TriggeringIdUtil.GetEventEid(ProjectileTriggering.Events.Hit)),
                HandleProjectileHit));
        }

        private void HandleDamageResult(DamageResult result)
        {
            _sink?.OnDamageResult(in result);
        }

        private void HandleProjectileHit(ProjectileHitEvent evt)
        {
            _sink?.OnProjectileHit(in evt);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _subscriptions.Clear();
        }
    }
}
