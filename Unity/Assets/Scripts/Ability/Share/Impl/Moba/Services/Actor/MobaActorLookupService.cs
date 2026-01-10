using System;
using Entitas;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{ 
    public sealed class MobaActorLookupService
    {
        private readonly ActorIdIndex _index;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly IGroup<global::ActorEntity> _group;

        public MobaActorLookupService(ActorIdIndex index, MobaActorRegistry registry, MobaEntityManager entities, global::Contexts contexts)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            _group = contexts.actor.GetGroup(global::ActorMatcher.ActorId);
        }

        public bool TryGetActorEntity(int actorId, out global::ActorEntity entity)
        {
            if (actorId <= 0)
            {
                entity = null;
                return false;
            }

            if (_entities != null && _entities.TryGetActorEntity(actorId, out entity) && entity != null)
            {
                return true;
            }

            if (_index.TryGet(actorId, out entity))
            {
                if (entity != null) _registry.Register(actorId, entity);
                return entity != null;
            }

            if (_registry.TryGet(actorId, out entity) && entity != null)
            {
                return true;
            }

            var entities = _group.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId) continue;
                if (e.actorId.Value != actorId) continue;

                _registry.Register(actorId, e);
                entity = e;
                return true;
            }

            entity = null;
            return false;
        }
    }
}
