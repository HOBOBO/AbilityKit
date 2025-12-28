using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.EntityManager
{
    public interface IKeyedEntityIndex<TKey, TId> : IEntityIndex<TId>
    {
        IReadOnlyCollection<TId> Get(TKey key);
        bool TryGet(TKey key, out IReadOnlyCollection<TId> entities);
    }
}
