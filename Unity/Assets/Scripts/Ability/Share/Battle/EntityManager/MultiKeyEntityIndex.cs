using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.EntityManager
{
    public sealed class MultiKeyEntityIndex<TKey, TId> : IKeyedEntityIndex<TKey, TId>
    {
        private readonly Dictionary<TKey, HashSet<TId>> _map;
        private readonly Dictionary<TId, HashSet<TKey>> _keysByEntity;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private readonly IEqualityComparer<TId> _idComparer;

        public MultiKeyEntityIndex(IEqualityComparer<TKey> keyComparer = null, IEqualityComparer<TId> idComparer = null)
        {
            _keyComparer = keyComparer;
            _idComparer = idComparer;
            _map = new Dictionary<TKey, HashSet<TId>>(keyComparer);
            _keysByEntity = new Dictionary<TId, HashSet<TKey>>(idComparer);
        }

        public IReadOnlyCollection<TId> Get(TKey key)
        {
            return _map.TryGetValue(key, out var set) ? set : Array.Empty<TId>();
        }

        public bool TryGet(TKey key, out IReadOnlyCollection<TId> entities)
        {
            if (_map.TryGetValue(key, out var set))
            {
                entities = set;
                return true;
            }

            entities = null;
            return false;
        }

        public bool TryGetKeys(TId id, out IReadOnlyCollection<TKey> keys)
        {
            if (_keysByEntity.TryGetValue(id, out var set))
            {
                keys = set;
                return true;
            }

            keys = null;
            return false;
        }

        public void AddKey(TId id, TKey key)
        {
            if (!_keysByEntity.TryGetValue(id, out var keys))
            {
                keys = new HashSet<TKey>(_keyComparer);
                _keysByEntity.Add(id, keys);
            }

            if (!keys.Add(key)) return;

            if (!_map.TryGetValue(key, out var entities))
            {
                entities = new HashSet<TId>(_idComparer);
                _map.Add(key, entities);
            }

            entities.Add(id);
        }

        public void RemoveKey(TId id, TKey key)
        {
            if (_keysByEntity.TryGetValue(id, out var keys))
            {
                if (keys.Remove(key))
                {
                    if (keys.Count == 0) _keysByEntity.Remove(id);
                }
            }

            if (_map.TryGetValue(key, out var entities))
            {
                entities.Remove(id);
                if (entities.Count == 0) _map.Remove(key);
            }
        }

        public void ClearKeys(TId id)
        {
            if (!_keysByEntity.TryGetValue(id, out var keys)) return;

            foreach (var k in keys)
            {
                if (_map.TryGetValue(k, out var entities))
                {
                    entities.Remove(id);
                    if (entities.Count == 0) _map.Remove(k);
                }
            }

            _keysByEntity.Remove(id);
        }

        public void OnEntityAdded(TId id)
        {
        }

        public void OnEntityRemoved(TId id)
        {
            ClearKeys(id);
        }

        public void OnEntityUpdated(TId id, EntityUpdate update)
        {
        }
    }
}
