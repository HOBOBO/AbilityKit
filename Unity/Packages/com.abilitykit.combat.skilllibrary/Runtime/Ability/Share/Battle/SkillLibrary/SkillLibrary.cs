using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public sealed class SkillLibrary<TKey, TData>
    {
        private readonly Dictionary<TKey, TData> _skills;
        private readonly List<ISkillIndex<TKey, TData>> _indices;

        public SkillLibrary(IEqualityComparer<TKey> comparer = null)
        {
            _skills = new Dictionary<TKey, TData>(comparer);
            _indices = new List<ISkillIndex<TKey, TData>>(8);
        }

        public int Count => _skills.Count;

        public bool ContainsKey(TKey key) => _skills.ContainsKey(key);

        public IEnumerable<TKey> Keys => _skills.Keys;

        public bool TryGet(TKey key, out TData data) => _skills.TryGetValue(key, out data);

        public TData Get(TKey key)
        {
            if (!_skills.TryGetValue(key, out var data))
                throw new KeyNotFoundException($"Skill not found: {key}");
            return data;
        }

        public void AddIndex(ISkillIndex<TKey, TData> index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            _indices.Add(index);

            foreach (var kv in _skills)
            {
                index.OnAdded(kv.Key, kv.Value);
            }
        }

        public bool RemoveIndex(ISkillIndex<TKey, TData> index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            return _indices.Remove(index);
        }

        public bool Add(TKey key, TData data)
        {
            if (_skills.ContainsKey(key)) return false;
            _skills.Add(key, data);

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnAdded(key, data);
            }

            return true;
        }

        public bool Remove(TKey key)
        {
            if (!_skills.TryGetValue(key, out var old)) return false;
            _skills.Remove(key);

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnRemoved(key, old);
            }

            return true;
        }

        public bool Update(TKey key, TData newData, SkillUpdate update)
        {
            if (!_skills.TryGetValue(key, out var old)) return false;
            _skills[key] = newData;

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnUpdated(key, old, newData, update);
            }

            return true;
        }

        public void NotifyUpdated(TKey key, SkillUpdate update)
        {
            if (!_skills.TryGetValue(key, out var data)) return;

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnUpdated(key, data, data, update);
            }
        }

        public TIndex GetIndex<TIndex>() where TIndex : class
        {
            for (var i = 0; i < _indices.Count; i++)
            {
                if (_indices[i] is TIndex t) return t;
            }

            return null;
        }

        public DerivedKeyedSkillIndex<TIndexKey, TKey, TData> CreateDerivedKeyedIndex<TIndexKey>(Func<TData, TIndexKey> selector, IEqualityComparer<TIndexKey> keyComparer = null)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var idx = new DerivedKeyedSkillIndex<TIndexKey, TKey, TData>(selector, keyComparer);
            AddIndex(idx);
            return idx;
        }

        public DerivedMultiKeySkillIndex<TIndexKey, TKey, TData> CreateDerivedMultiKeyIndex<TIndexKey>(Func<TData, IEnumerable<TIndexKey>> selector, IEqualityComparer<TIndexKey> keyComparer = null)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var idx = new DerivedMultiKeySkillIndex<TIndexKey, TKey, TData>(selector, keyComparer);
            AddIndex(idx);
            return idx;
        }
    }
}
