namespace AbilityKit.Ability.Battle.EntityManager
{
    public sealed class BattleEntityManager<TId>
    {
        private readonly System.Collections.Generic.IEqualityComparer<TId> _idComparer;

        public readonly EntityRegistry<TId> Registry;

        public BattleEntityManager(System.Collections.Generic.IEqualityComparer<TId> idComparer = null)
        {
            _idComparer = idComparer;
            Registry = new EntityRegistry<TId>(idComparer);
        }

        public void Add(TId id)
        {
            Registry.Add(id);
        }

        public int AddRange(System.Collections.Generic.IEnumerable<TId> ids)
        {
            return Registry.AddRange(ids);
        }

        public void Remove(TId id)
        {
            Registry.Remove(id);
        }

        public int RemoveRange(System.Collections.Generic.IEnumerable<TId> ids)
        {
            return Registry.RemoveRange(ids);
        }

        public KeyedEntityIndex<TKey, TId> CreateKeyedIndex<TKey>(System.Collections.Generic.IEqualityComparer<TKey> keyComparer = null)
        {
            var index = new KeyedEntityIndex<TKey, TId>(keyComparer, _idComparer);
            Registry.AddIndex(index);
            return index;
        }

        public MultiKeyEntityIndex<TKey, TId> CreateMultiKeyIndex<TKey>(System.Collections.Generic.IEqualityComparer<TKey> keyComparer = null)
        {
            var index = new MultiKeyEntityIndex<TKey, TId>(keyComparer, _idComparer);
            Registry.AddIndex(index);
            return index;
        }

        public void NotifyUpdated(TId id, EntityUpdate update)
        {
            Registry.NotifyUpdated(id, update);
        }

        public int NotifyUpdatedBatch(System.Collections.Generic.IEnumerable<(TId id, EntityUpdate update)> updates)
        {
            return Registry.NotifyUpdatedBatch(updates);
        }
    }
}
