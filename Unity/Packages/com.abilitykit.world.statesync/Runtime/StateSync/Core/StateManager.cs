using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Diff;
using AbilityKit.Ability.StateSync.Snapshot;

namespace AbilityKit.Ability.StateSync
{
    public sealed class StateManager : IStateManager
    {
        public Action<string> Log;

        private readonly Dictionary<long, IRollbackable> _rollbackables = new Dictionary<long, IRollbackable>();
        private readonly SnapshotBuffer _snapshotBuffer;
        private readonly StateDiffProvider _diffProvider;
        private readonly object _lock = new object();

        public StateManager(SnapshotBuffer snapshotBuffer, StateDiffProvider diffProvider = null)
        {
            _snapshotBuffer = snapshotBuffer ?? throw new ArgumentNullException(nameof(snapshotBuffer));
            _diffProvider = diffProvider ?? new StateDiffProvider();
        }

        public void RegisterRollbackable(IRollbackable entity)
        {
            lock (_lock)
            {
                if (_rollbackables.ContainsKey(entity.EntityId))
                {
                    Log?.Invoke($"[StateManager] Entity {entity.EntityId} already registered");
                    return;
                }

                _rollbackables[entity.EntityId] = entity;
                Log?.Invoke($"[StateManager] Registered entity {entity.EntityId} with key {entity.SnapshotKey}");
            }
        }

        public void UnregisterRollbackable(long entityId)
        {
            lock (_lock)
            {
                if (_rollbackables.Remove(entityId))
                {
                    Log?.Invoke($"[StateManager] Unregistered entity {entityId}");
                }
            }
        }

        public void CaptureState(int frame)
        {
            lock (_lock)
            {
                var snapshot = new WorldStateSnapshot
                {
                    Version = WorldStateSnapshot.CurrentVersion,
                    Frame = frame,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Entities = new List<EntityStateSnapshot>(),
                    Projectiles = new List<ProjectileStateSnapshot>(),
                    Abilities = new List<AbilityStateSnapshot>()
                };

                foreach (var kvp in _rollbackables)
                {
                    var entity = kvp.Value;
                    var rollbackState = entity.CreateRollbackState();
                    var data = rollbackState.Serialize();
                    var entitySnapshot = DeserializeEntitySnapshot(data);
                    snapshot.Entities.Add(entitySnapshot);
                }

                _snapshotBuffer.Store(frame, snapshot);
                Log?.Invoke($"[StateManager] Captured state for frame={frame} entities={snapshot.Entities.Count}");
            }
        }

        public bool TryRestore(int frame)
        {
            lock (_lock)
            {
                if (!_snapshotBuffer.TryGet(frame, out var snapshot))
                {
                    Log?.Invoke($"[StateManager] No snapshot found for frame={frame}");
                    return false;
                }

                RestoreSnapshot(snapshot);
                Log?.Invoke($"[StateManager] Restored state for frame={frame}");
                return true;
            }
        }

        public IStateDiff ComputeDiff(int fromFrame, int toFrame)
        {
            lock (_lock)
            {
                if (!_snapshotBuffer.TryGet(fromFrame, out var fromSnapshot) ||
                    !_snapshotBuffer.TryGet(toFrame, out var toSnapshot))
                {
                    Log?.Invoke($"[StateManager] Cannot compute diff: missing snapshot(s)");
                    return null;
                }

                return _diffProvider.ComputeDiff(toSnapshot, fromSnapshot);
            }
        }

        public byte[] GetFullState(int frame)
        {
            lock (_lock)
            {
                if (!_snapshotBuffer.TryGet(frame, out var snapshot))
                {
                    return null;
                }

                return snapshot.ToBytes();
            }
        }

        public IReadOnlyList<int> GetCapturedFrames()
        {
            lock (_lock)
            {
                return _snapshotBuffer.GetCapturedFrames();
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _snapshotBuffer.Clear();
                Log?.Invoke("[StateManager] Cleared snapshot history");
            }
        }

        private void RestoreSnapshot(WorldStateSnapshot snapshot)
        {
            var entityMap = new Dictionary<long, EntityStateSnapshot>();
            foreach (var entitySnapshot in snapshot.Entities)
            {
                entityMap[entitySnapshot.EntityId] = entitySnapshot;
            }

            foreach (var kvp in _rollbackables)
            {
                if (entityMap.TryGetValue(kvp.Key, out var entitySnapshot))
                {
                    var rollbackState = kvp.Value.CreateRollbackState();
                    var data = SerializeEntitySnapshot(entitySnapshot);
                    rollbackState.Deserialize(data);
                    kvp.Value.RestoreFromRollbackState(rollbackState);
                }
            }
        }

        private byte[] SerializeEntitySnapshot(EntityStateSnapshot snapshot)
        {
            return BinarySerializer.SerializeObject(snapshot);
        }

        private EntityStateSnapshot DeserializeEntitySnapshot(byte[] data)
        {
            return (EntityStateSnapshot)BinarySerializer.DeserializeObject(data, typeof(EntityStateSnapshot));
        }
    }
}
