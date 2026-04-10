using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Event;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSummonDeathSubscriber : IService
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
                var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply);
                _sub = _eventBus.Subscribe(new EventKey<DamageResult>(eid), HandleAfterApply);
            }
        }

        private void HandleAfterApply(DamageResult r)
        {
            if (_summons == null) return;
            if (_registry == null) return;

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
                s.Dispose();
            }
        }
    }
}
