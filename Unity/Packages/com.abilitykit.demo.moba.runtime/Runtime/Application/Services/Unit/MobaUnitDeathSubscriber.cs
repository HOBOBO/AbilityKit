using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Events.Unit;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Demo.Moba;
    [WorldService(typeof(MobaUnitDeathSubscriber))]
    public sealed class MobaUnitDeathSubscriber : IWorldDeinitializable
    {
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly MobaEntityManager _entities;

        private readonly HashSet<int> _reported = new HashSet<int>();
        private IDisposable _sub;

        public MobaUnitDeathSubscriber(AbilityKit.Triggering.Eventing.IEventBus eventBus, MobaEntityManager entities)
        {
            _eventBus = eventBus;
            _entities = entities;

            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
            _sub = _eventBus != null ? _eventBus.Subscribe(new EventKey<MobaHealthChangeResult>(eid), OnHealthCommitted) : null;
        }

        private void OnHealthCommitted(MobaHealthChangeResult result)
        {
            if (!result.BecameDead || result.TargetActorId <= 0) return;
            if (_entities == null) return;
            if (!_entities.TryGetActorEntity(result.TargetActorId, out var e) || e == null) return;
            if (!_reported.Add(result.TargetActorId)) return;

            PublishDie(in result);
        }

        private void PublishDie(in MobaHealthChangeResult result)
        {
            var eventId = MobaUnitTriggering.Events.Die;
            var origin = result.Origin;
            var payload = new UnitDieEventPayload(
                actorId: result.TargetActorId,
                killerActorId: result.SourceActorId,
                damageType: result.ValueType,
                reasonKind: result.ReasonKind,
                reasonParam: result.ReasonParam,
                damageValue: result.AppliedValue,
                origin: in origin);

            if (_eventBus == null) return;
            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
            _eventBus.Publish(new EventKey<UnitDieEventPayload>(eid), in payload);
            var objectKey = new EventKey<object>(eid);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = payload;
                _eventBus.Publish(objectKey, in boxed);
            }
        }

        internal void NotifyRespawned(int actorId)
        {
            if (actorId > 0)
            {
                _reported.Remove(actorId);
            }
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
            _reported.Clear();
        }
    }
}
