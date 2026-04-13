using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Event;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Demo.Moba;
    public sealed class MobaUnitDeathSubscriber : IService
    {
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly MobaEntityManager _entities;

        private readonly HashSet<int> _reported = new HashSet<int>();
        private IDisposable _sub;

        public MobaUnitDeathSubscriber(AbilityKit.Triggering.Eventing.IEventBus eventBus, MobaEntityManager entities)
        {
            _eventBus = eventBus;
            _entities = entities;

            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply);
            _sub = _eventBus != null ? _eventBus.Subscribe(new EventKey<DamageResult>(eid), OnAfterApply) : null;
        }

        private void OnAfterApply(DamageResult r)
        {
            HandleAfterApply(r);
        }

        private void HandleAfterApply(DamageResult r)
        {
            if (r == null) return;
            if (r.TargetActorId <= 0) return;
            if (r.TargetHp > 0f) return;

            if (_entities == null) return;
            if (!_entities.TryGetActorEntity(r.TargetActorId, out var e) || e == null) return;

            if (!_reported.Add(r.TargetActorId)) return;

            PublishDie(r);
        }

        private void PublishDie(DamageResult r)
        {
            var eventId = MobaUnitTriggering.Events.Die;

            var payload = new UnitDieEventPayload(
                actorId: r.TargetActorId,
                killerActorId: r.AttackerActorId,
                damageType: (int)r.DamageType,
                reasonKind: (int)r.ReasonKind,
                reasonParam: r.ReasonParam,
                damageValue: r.Value);

            if (_eventBus == null) return;
            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
            _eventBus.Publish(new EventKey<UnitDieEventPayload>(eid), in payload);
            object boxed = payload;
            _eventBus.Publish(new EventKey<object>(eid), in boxed);
        }

        public void Dispose()
        {
            _reported.Clear();

            var s = _sub;
            if (s != null)
            {
                _sub = null;
                s.Dispose();
            }
        }
    }
}
