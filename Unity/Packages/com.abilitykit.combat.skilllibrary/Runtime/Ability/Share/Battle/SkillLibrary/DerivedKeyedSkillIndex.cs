using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public sealed class DerivedKeyedSkillIndex<TIndexKey, TKey, TData> : ISkillIndex<TKey, TData>, IKeyedSkillIndex<TIndexKey, TKey>
    {
        private readonly Func<TData, TIndexKey> _selector;
        private readonly Dictionary<TIndexKey, HashSet<TKey>> _map;

        public DerivedKeyedSkillIndex(Func<TData, TIndexKey> selector, IEqualityComparer<TIndexKey> keyComparer = null)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
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
            var indexKey = _selector(data);
            AddInternal(indexKey, key);
        }

        public void OnRemoved(TKey key, TData data)
        {
            var indexKey = _selector(data);
            RemoveInternal(indexKey, key);
        }

        public void OnUpdated(TKey key, TData oldData, TData newData, SkillUpdate update)
        {
            var oldKey = _selector(oldData);
            var newKey = _selector(newData);

            if (EqualityComparer<TIndexKey>.Default.Equals(oldKey, newKey)) return;

            RemoveInternal(oldKey, key);
            AddInternal(newKey, key);
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
