using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaActorRegistry
    {
        private readonly Dictionary<int, global::ActorEntity> _byId = new Dictionary<int, global::ActorEntity>();

        public IEnumerable<KeyValuePair<int, global::ActorEntity>> Entries => _byId;

        public void Register(int actorId, global::ActorEntity entity)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _byId[actorId] = entity;
        }

        public bool TryGet(int actorId, out global::ActorEntity entity)
        {
            return _byId.TryGetValue(actorId, out entity);
        }

        public void Clear()
        {
            _byId.Clear();
        }
    }
}
