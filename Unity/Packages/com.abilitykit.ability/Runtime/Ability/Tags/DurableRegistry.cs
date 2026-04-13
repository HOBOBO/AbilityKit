using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.GameplayTags;
using IDurableRegistry = AbilityKit.GameplayTags.IDurableRegistry;
using IDurable = AbilityKit.GameplayTags.IDurable;

namespace AbilityKit.Ability.Tags
{
    public sealed class DurableRegistry : IDurableRegistry, IService
    {
        private readonly Dictionary<int, List<IDurable>> _byOwner = new Dictionary<int, List<IDurable>>();

        public void Dispose()
        {
            _byOwner.Clear();
        }

        public void Register(IDurable durable)
        {
            if (durable == null) throw new ArgumentNullException(nameof(durable));
            var owner = durable.OwnerId;
            if (owner <= 0) return;

            if (!_byOwner.TryGetValue(owner, out var list) || list == null)
            {
                list = new List<IDurable>(8);
                _byOwner[owner] = list;
            }

            if (!list.Contains(durable))
            {
                list.Add(durable);
            }
        }

        public bool Unregister(IDurable durable)
        {
            if (durable == null) return false;
            var owner = durable.OwnerId;
            if (owner <= 0) return false;

            if (!_byOwner.TryGetValue(owner, out var list) || list == null) return false;
            var removed = list.Remove(durable);
            if (list.Count == 0)
            {
                _byOwner.Remove(owner);
            }
            return removed;
        }

        public IReadOnlyList<IDurable> GetByOwner(int ownerId)
        {
            if (ownerId <= 0) return Array.Empty<IDurable>();
            if (_byOwner.TryGetValue(ownerId, out var list) && list != null)
            {
                return list;
            }
            return Array.Empty<IDurable>();
        }
    }
}
