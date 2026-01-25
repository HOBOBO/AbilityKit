using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.EntityManager
{
    public sealed class KeyedEntityIndex<TKey, TId> : IKeyedEntityIndex<TKey, TId>
    {
        private readonly Dictionary<TKey, HashSet<TId>> _map;
        private readonly Dictionary<TId, TKey> _currentKey;
        private readonly IEqualityComparer<TId> _idComparer;

        public KeyedEntityIndex(IEqualityComparer<TKey> keyComparer = null, IEqualityComparer<TId> idComparer = null)
        {
            _map = new Dictionary<TKey, HashSet<TId>>(keyComparer);
            _currentKey = new Dictionary<TId, TKey>(idComparer);
            _idComparer = idComparer;
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

        public bool TryGetKey(TId id, out TKey key)
        {
            return _currentKey.TryGetValue(id, out key);
        }

        public void SetKey(TId id, TKey key)
        {
            if (_currentKey.TryGetValue(id, out var oldKey))
            {
                if (EqualityComparer<TKey>.Default.Equals(oldKey, key)) return;
                RemoveFromKey(id, oldKey);
            }

            _currentKey[id] = key;
            AddToKey(id, key);
        }

        public void ClearKey(TId id)
        {
            if (_currentKey.TryGetValue(id, out var oldKey))
            {
                _currentKey.Remove(id);
                RemoveFromKey(id, oldKey);
            }
        }

        public void OnEntityAdded(TId id)
        {
        }

        public void OnEntityRemoved(TId id)
        {
            ClearKey(id);
        }

        public void OnEntityUpdated(TId id, EntityUpdate update)
        {
        }

        private void AddToKey(TId id, TKey key)
        {
            if (!_map.TryGetValue(key, out var set))
            {
                set = new HashSet<TId>(_idComparer);
                _map.Add(key, set);
            }

            set.Add(id);
        }

        private void RemoveFromKey(TId id, TKey key)
        {
            if (!_map.TryGetValue(key, out var set)) return;

            set.Remove(id);
            if (set.Count == 0)
            {
                _map.Remove(key);
            }
        }
    }
}
