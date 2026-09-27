using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed class PresentationActorIndex<TEntityId>
    {
        private readonly Dictionary<int, TEntityId> _entities = new Dictionary<int, TEntityId>();
        private readonly IEqualityComparer<TEntityId> _comparer = EqualityComparer<TEntityId>.Default;

        public void Rebind(int previousActorId, int actorId, TEntityId entityId)
        {
            if (previousActorId > 0 && previousActorId != actorId)
            {
                Remove(previousActorId, entityId);
            }

            if (actorId > 0) _entities[actorId] = entityId;
        }

        public bool TryResolve(int actorId, out TEntityId entityId)
        {
            return _entities.TryGetValue(actorId, out entityId);
        }

        public void Remove(int actorId, TEntityId entityId)
        {
            if (actorId > 0 &&
                _entities.TryGetValue(actorId, out var mapped) &&
                _comparer.Equals(mapped, entityId))
            {
                _entities.Remove(actorId);
            }
        }

        public void Clear() => _entities.Clear();
    }
}
