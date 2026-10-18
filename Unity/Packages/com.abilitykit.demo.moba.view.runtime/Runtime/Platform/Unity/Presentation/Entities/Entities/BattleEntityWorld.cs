using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Entity
{
    public sealed class BattleEntityLookup
    {
        private readonly Dictionary<int, IEntityId> _netIdToEntityId = new Dictionary<int, IEntityId>();

        public int Count => _netIdToEntityId.Count;

        public void Bind(BattleNetId netId, IEntity entity)
        {
            if (entity.World == null) throw new ArgumentException("Entity has no world", nameof(entity));
            _netIdToEntityId[netId.Value] = entity.Id;
        }

        public bool TryResolve(IECWorld world, BattleNetId netId, out IEntity entity)
        {
            entity = default;
            if (world == null) return false;
            if (!_netIdToEntityId.TryGetValue(netId.Value, out var id)) return false;
            if (!world.IsAlive(id)) return false;
            entity = world.Wrap(id);
            return true;
        }

        public bool Unbind(BattleNetId netId)
        {
            return _netIdToEntityId.Remove(netId.Value);
        }

        public bool UnbindByEntityId(IEntityId id)
        {
            foreach (var kv in _netIdToEntityId)
            {
                if (kv.Value.Equals(id))
                {
                    return _netIdToEntityId.Remove(kv.Key);
                }
            }
            return false;
        }

        public void Clear()
        {
            _netIdToEntityId.Clear();
        }
    }
}
