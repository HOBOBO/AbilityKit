using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSummonDeathSubscriber : IService, IEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly MobaActorRegistry _registry;
        private readonly MobaSummonService _summons;
        private IEventSubscription _sub;

        public MobaSummonDeathSubscriber(IEventBus eventBus, MobaActorRegistry registry, MobaSummonService summons)
        {
            _eventBus = eventBus;
            _registry = registry;
            _summons = summons;
            _sub = _eventBus?.Subscribe(DamagePipelineEvents.AfterApply, this);
        }

        public void Handle(in TriggerEvent evt)
        {
            if (_summons == null) return;
            if (_registry == null) return;

            var r = evt.Payload as DamageResult;
            if (r == null) return;
            if (r.TargetActorId <= 0) return;
            if (r.TargetHp > 0f) return;

            if (!_registry.TryGet(r.TargetActorId, out var e) || e == null) return;
            if (!e.hasSummonMeta) return;

            _summons.TryDespawn(r.TargetActorId, SummonDespawnReason.Killed);
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
