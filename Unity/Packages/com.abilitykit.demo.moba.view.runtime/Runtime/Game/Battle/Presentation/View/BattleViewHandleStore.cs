using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewHandleStore
    {
        private readonly Dictionary<IEntityId, BattleViewHandle> _handles = new Dictionary<IEntityId, BattleViewHandle>();
        private readonly PresentationActorIndex<IEntityId> _actors = new PresentationActorIndex<IEntityId>();

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
            if (!_actors.TryResolve(actorId, out entityId)) return false;
            return TryGet(entityId, out handle);
        }

        public void SetActorId(BattleViewHandle handle, int actorId, IEntityId entityId)
        {
            if (handle == null || actorId <= 0) return;

            _actors.Rebind(handle.ActorId, actorId, entityId);
            handle.ActorId = actorId;
        }

        public void Remove(IEntityId entityId)
        {
            if (_handles.TryGetValue(entityId, out var handle) && handle != null && handle.ActorId > 0)
            {
                _actors.Remove(handle.ActorId, entityId);
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
            _actors.Clear();
        }
    }
}
