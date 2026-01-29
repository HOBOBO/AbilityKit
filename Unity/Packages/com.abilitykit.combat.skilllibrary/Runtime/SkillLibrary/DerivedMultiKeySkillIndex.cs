using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public sealed class DerivedMultiKeySkillIndex<TIndexKey, TKey, TData> : ISkillIndex<TKey, TData>, IKeyedSkillIndex<TIndexKey, TKey>
    {
        private readonly Func<TData, IEnumerable<TIndexKey>> _selector;
        private readonly Dictionary<TIndexKey, HashSet<TKey>> _map;
        private readonly IEqualityComparer<TIndexKey> _keyComparer;

        public DerivedMultiKeySkillIndex(Func<TData, IEnumerable<TIndexKey>> selector, IEqualityComparer<TIndexKey> keyComparer = null)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _keyComparer = keyComparer;
            _map = new Dictionary<TIndexKey, HashSet<TKey>>(keyComparer);
        }

        public IReadOnlyCollection<TKey> Get(TIndexKey key)
        {
            return _map.TryGetValue(key, out var set) ? set : Array.Empty<TKey>();
        }

        public bool TryGet(TIndexKey key, out IReadOnlyCollection<TKey> skills)
        {
            if (_map.TryGetValue(key, out var set))
            {
                skills = set;
                return true;
            }

            skills = null;
            return false;
        }

        public void OnAdded(TKey key, TData data)
        {
            foreach (var indexKey in EnumerateKeys(data))
            {
                AddInternal(indexKey, key);
            }
        }

        public void OnRemoved(TKey key, TData data)
        {
            foreach (var indexKey in EnumerateKeys(data))
            {
                RemoveInternal(indexKey, key);
            }
        }

        public void OnUpdated(TKey key, TData oldData, TData newData, SkillUpdate update)
        {
            var oldKeys = new HashSet<TIndexKey>(_keyComparer);
            foreach (var k in EnumerateKeys(oldData)) oldKeys.Add(k);

            var newKeys = new HashSet<TIndexKey>(_keyComparer);
            foreach (var k in EnumerateKeys(newData)) newKeys.Add(k);

            foreach (var k in oldKeys)
            {
                if (!newKeys.Contains(k)) RemoveInternal(k, key);
            }

            foreach (var k in newKeys)
            {
                if (!oldKeys.Contains(k)) AddInternal(k, key);
            }
        }

        private IEnumerable<TIndexKey> EnumerateKeys(TData data)
        {
            var keys = _selector(data);
            return keys ?? Array.Empty<TIndexKey>();
        }

        private void AddInternal(TIndexKey indexKey, TKey skillKey)
        {
            if (!_map.TryGetValue(indexKey, out var set))
            {
                set = new HashSet<TKey>();
                _map.Add(indexKey, set);
            }

            set.Add(skillKey);
        }

        private void RemoveInternal(TIndexKey indexKey, TKey skillKey)
        {
            if (!_map.TryGetValue(indexKey, out var set)) return;

            set.Remove(skillKey);
            if (set.Count == 0)
            {
                _map.Remove(indexKey);
            }
        }
    }
}
