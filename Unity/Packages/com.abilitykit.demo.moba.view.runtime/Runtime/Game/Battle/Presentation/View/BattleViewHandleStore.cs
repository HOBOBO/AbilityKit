using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewHandleStore
    {
        private readonly Dictionary<IEntityId, BattleViewHandle> _handles = new Dictionary<IEntityId, BattleViewHandle>();
        private readonly Dictionary<int, IEntityId> _actorIdToEntityId = new Dictionary<int, IEntityId>();

        public BattleViewHandle GetOrCreate(IEntityId entityId)
        {
            if (_handles.TryGetValue(entityId, out var handle) && handle != null)
            {
                return handle;
            }

            handle = new BattleViewHandle();
            _handles[entityId] = handle;
            return handle;
        }

        public bool TryGet(IEntityId entityId, out BattleViewHandle handle)
        {
            return _handles.TryGetValue(entityId, out handle) && handle != null;
        }

        public bool TryGetByActorId(int actorId, out IEntityId entityId, out BattleViewHandle handle)
        {
            handle = null;
            entityId = default;
            if (!_actorIdToEntityId.TryGetValue(actorId, out entityId)) return false;
            return TryGet(entityId, out handle);
        }

        public void SetActorId(BattleViewHandle handle, int actorId, IEntityId entityId)
        {
            if (handle == null || actorId <= 0) return;

            if (handle.ActorId != actorId)
            {
                if (handle.ActorId > 0 &&
                    _actorIdToEntityId.TryGetValue(handle.ActorId, out var mappedEntityId) &&
                    Equals(mappedEntityId, entityId))
                {
                    _actorIdToEntityId.Remove(handle.ActorId);
                }
                handle.ActorId = actorId;
            }

            _actorIdToEntityId[actorId] = entityId;
        }

        public void Remove(IEntityId entityId)
        {
            if (_handles.TryGetValue(entityId, out var handle) && handle != null && handle.ActorId > 0)
            {
                if (_actorIdToEntityId.TryGetValue(handle.ActorId, out var mappedEntityId) &&
                    Equals(mappedEntityId, entityId))
                {
                    _actorIdToEntityId.Remove(handle.ActorId);
                }
            }

            _handles.Remove(entityId);
        }

        public void ForEach(Action<IEntityId, BattleViewHandle> visitor)
        {
            if (visitor == null) return;

            foreach (var kv in _handles)
            {
                visitor(kv.Key, kv.Value);
            }
        }

        public void Clear()
        {
            _handles.Clear();
            _actorIdToEntityId.Clear();
        }
    }
}
