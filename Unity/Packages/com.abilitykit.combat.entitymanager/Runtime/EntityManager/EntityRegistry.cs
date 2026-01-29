using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.EntityManager
{
    public sealed class EntityRegistry<TId>
    {
        private readonly HashSet<TId> _entities;
        private readonly List<IEntityIndex<TId>> _indices;

        public EntityRegistry(IEqualityComparer<TId> comparer = null)
        {
            _entities = new HashSet<TId>(comparer);
            _indices = new List<IEntityIndex<TId>>(8);
        }

        public int Count => _entities.Count;

        public IEnumerable<TId> Entities => _entities;

        public void AddIndex(IEntityIndex<TId> index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            _indices.Add(index);

            foreach (var id in _entities)
            {
                index.OnEntityAdded(id);
            }
        }

        public bool RemoveIndex(IEntityIndex<TId> index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            return _indices.Remove(index);
        }

        public bool Contains(TId id) => _entities.Contains(id);

        public bool Add(TId id)
        {
            if (!_entities.Add(id)) return false;

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnEntityAdded(id);
            }

            return true;
        }

        public int AddRange(IEnumerable<TId> ids)
        {
            if (ids == null) return 0;

            var added = 0;
            foreach (var id in ids)
            {
                if (_entities.Add(id))
                {
                    added++;
                    for (var i = 0; i < _indices.Count; i++)
                    {
                        _indices[i].OnEntityAdded(id);
                    }
                }
            }

            return added;
        }

        public bool Remove(TId id)
        {
            if (!_entities.Remove(id)) return false;

            for (var i = 0; i < _indices.Count; i++)
            {
                _indices[i].OnEntityRemoved(id);
            }

            return true;
        }

        public int RemoveRange(IEnumerable<TId> ids)
        {
            if (ids == null) return 0;

            var removed = 0;
            foreach (var id in ids)
            {
                if (_entities.Remove(id))
                {
                    removed++;
                    for (var i = 0; i < _indices.Count; i++)
                    {
                        _indices[i].OnEntityRemoved(id);
                    }
                }
            }

            return removed;
        }

        public void NotifyUpdated(TId id, EntityUpdate update)
        {
            if (!_entities.Contains(id)) return;

            for (var i = 0; i < _indices.Count; i++)
            {
                var index = _indices[i];
                if (index is IUpdateTypeAwareIndex<TId> aware && !aware.Accepts(update.Type)) continue;
                index.OnEntityUpdated(id, update);
            }
        }

        public int NotifyUpdatedBatch(IEnumerable<(TId id, EntityUpdate update)> updates)
        {
            if (updates == null) return 0;

            var count = 0;
            foreach (var (id, update) in updates)
            {
                if (!_entities.Contains(id)) continue;
                count++;

                for (var i = 0; i < _indices.Count; i++)
                {
                    var index = _indices[i];
                    if (index is IUpdateTypeAwareIndex<TId> aware && !aware.Accepts(update.Type)) continue;
                    index.OnEntityUpdated(id, update);
                }
            }

            return count;
        }

        public TIndex GetIndex<TIndex>() where TIndex : class, IEntityIndex<TId>
        {
            for (var i = 0; i < _indices.Count; i++)
            {
                if (_indices[i] is TIndex t) return t;
            }

            return null;
        }
    }
}
