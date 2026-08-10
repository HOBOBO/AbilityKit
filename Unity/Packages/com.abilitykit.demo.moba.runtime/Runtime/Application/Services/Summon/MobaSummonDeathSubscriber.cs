using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Events.Summon;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSummonDeathSubscriber))]
    public sealed class MobaSummonDeathSubscriber : IWorldDeinitializable
    {
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly MobaActorRegistry _registry;
        private readonly MobaSummonService _summons;
        private IDisposable _sub;

        public MobaSummonDeathSubscriber(AbilityKit.Triggering.Eventing.IEventBus eventBus, MobaActorRegistry registry, MobaSummonService summons)
        {
            _eventBus = eventBus;
            _registry = registry;
            _summons = summons;

            if (_eventBus != null)
            {
                var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
                _sub = _eventBus.Subscribe(new EventKey<MobaHealthChangeResult>(eid), HandleHealthCommitted);
            }
        }

        private void HandleHealthCommitted(MobaHealthChangeResult result)
        {
            if (_summons == null || _registry == null) return;
            if (!result.BecameDead || result.TargetActorId <= 0) return;
            if (!_registry.TryGet(result.TargetActorId, out var e) || e == null) return;
            if (!e.hasSummonMeta) return;

            _summons.TryDespawn(result.TargetActorId, SummonDespawnReason.Killed);
        }

        public void OnDeinit(IWorldResolver services)
        {
            var s = _sub;
            if (s != null)
            {
                _sub = null;
                s.Dispose();
            }
        }

        public void Dispose()
        {
        }
    }
}
