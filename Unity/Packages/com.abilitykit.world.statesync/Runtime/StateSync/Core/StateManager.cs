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

        /// <summary>
        /// 实体回滚数据缓冲区：Frame -> (EntityId -> RollbackState bytes)
        /// 与 WorldStateSnapshot 分离，用于存储实体的完整回滚状态
        /// </summary>
        private readonly Dictionary<int, Dictionary<long, byte[]>> _entityRollbackBuffers = new Dictionary<int, Dictionary<long, byte[]>>();
        private readonly List<int> _retainedFramesScratch = new List<int>(128);
        private readonly List<int> _staleFramesScratch = new List<int>(128);

        public StateManager(SnapshotBuffer snapshotBuffer, StateDiffProvider diffProvider = null)
        {
            _snapshotBuffer = snapshotBuffer ?? throw new ArgumentNullException(nameof(snapshotBuffer));
            _diffProvider = diffProvider ?? new StateDiffProvider();
        }

        public int RetainedRollbackFrameCount
        {
            get
            {
                lock (_lock)
                {
                    return _entityRollbackBuffers.Count;
                }
            }
        }

        public void RegisterRollbackable(IRollbackable entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            bool registered;
            lock (_lock)
            {
                registered = !_rollbackables.ContainsKey(entity.EntityId);
                if (registered)
                {
                    _rollbackables[entity.EntityId] = entity;
                }
            }

            Log?.Invoke(registered
                ? $"[StateManager] Registered entity {entity.EntityId} with key {entity.SnapshotKey}"
                : $"[StateManager] Entity {entity.EntityId} already registered");
        }

        public void UnregisterRollbackable(long entityId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _rollbackables.Remove(entityId);
            }

            if (removed)
            {
                Log?.Invoke($"[StateManager] Unregistered entity {entityId}");
            }
        }

        public void CaptureState(int frame)
        {
            KeyValuePair<long, IRollbackable>[] rollbackables;
            lock (_lock)
            {
                rollbackables = new KeyValuePair<long, IRollbackable>[_rollbackables.Count];
                var index = 0;
                foreach (var pair in _rollbackables)
                {
                    rollbackables[index++] = pair;
                }
            }

            var snapshot = new WorldStateSnapshot
            {
                Version = WorldStateSnapshot.CurrentVersion,
                Frame = frame,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var entityRollbackData = new Dictionary<long, byte[]>(rollbackables.Length);
            for (int i = 0; i < rollbackables.Length; i++)
            {
                var pair = rollbackables[i];
                var rollbackState = pair.Value.CreateRollbackState();
                if (rollbackState != null)
                {
                    entityRollbackData[pair.Key] = rollbackState.Serialize() ?? Array.Empty<byte>();
                }
            }

            _snapshotBuffer.Store(frame, snapshot);
            lock (_lock)
            {
                _snapshotBuffer.CopyCapturedFrames(_retainedFramesScratch);
                _entityRollbackBuffers[frame] = entityRollbackData;
                TrimRollbackBuffers(_retainedFramesScratch);
            }

            Log?.Invoke($"[StateManager] Captured state for frame={frame} with {entityRollbackData.Count} entities");
        }

        public bool TryRestore(int frame)
        {
            if (!_snapshotBuffer.TryGet(frame, out _))
            {
                Log?.Invoke($"[StateManager] No snapshot found for frame={frame}");
                return false;
            }

            Dictionary<long, byte[]> entityRollbackData;
            Dictionary<long, IRollbackable> rollbackables;
            lock (_lock)
            {
                if (!_entityRollbackBuffers.TryGetValue(frame, out var storedRollbackData))
                {
                    entityRollbackData = null;
                    rollbackables = null;
                }
                else
                {
                    entityRollbackData = new Dictionary<long, byte[]>(storedRollbackData);
                    rollbackables = new Dictionary<long, IRollbackable>(_rollbackables);
                }
            }

            if (entityRollbackData == null)
            {
                Log?.Invoke($"[StateManager] No entity rollback data found for frame={frame}");
                return false;
            }

            RestoreSnapshot(frame, entityRollbackData, rollbackables);
            Log?.Invoke($"[StateManager] Restored state for frame={frame}");
            return true;
        }

        public IStateDiff ComputeDiff(int fromFrame, int toFrame)
        {
            if (!_snapshotBuffer.TryGet(fromFrame, out var fromSnapshot) ||
                !_snapshotBuffer.TryGet(toFrame, out var toSnapshot))
            {
                Log?.Invoke("[StateManager] Cannot compute diff: missing snapshot(s)");
                return null;
            }

            return _diffProvider.ComputeDiff(toSnapshot, fromSnapshot);
        }

        public byte[] GetFullState(int frame)
        {
            return _snapshotBuffer.TryGet(frame, out var snapshot) ? snapshot.ToBytes() : null;
        }

        public IReadOnlyList<int> GetCapturedFrames()
        {
            return _snapshotBuffer.GetCapturedFrames();
        }

        public void ClearHistory()
        {
            _snapshotBuffer.Clear();
            lock (_lock)
            {
                _entityRollbackBuffers.Clear();
            }

            Log?.Invoke("[StateManager] Cleared snapshot history");
        }

        /// <summary>
        /// 从回滚数据恢复所有实体的状态。
        /// 恢复路径刻意只回放逐实体回滚数据（IRollbackable）；WorldStateSnapshot 服务于
        /// diff/哈希/网络面，不参与恢复，因此不再传入。
        /// 要求业务层实现 IRollbackable 接口
        /// </summary>
        private void RestoreSnapshot(
            int frame,
            Dictionary<long, byte[]> entityRollbackData,
            Dictionary<long, IRollbackable> rollbackables)
        {
            foreach (var kvp in entityRollbackData)
            {
                var entityId = kvp.Key;
                var rollbackData = kvp.Value;

                if (rollbackables.TryGetValue(entityId, out var entity))
                {
                    var rollbackState = entity.CreateRollbackState();
                    if (rollbackState != null)
                    {
                        rollbackState.Deserialize(rollbackData);
                        entity.RestoreFromRollbackState(rollbackState);
                        Log?.Invoke($"[StateManager] Restored entity {entityId} for frame={frame}");
                    }
                }
                else
                {
                    Log?.Invoke($"[StateManager] Entity {entityId} not found for rollback");
                }
            }
        }

        private void TrimRollbackBuffers(List<int> retainedFrames)
        {
            if (_entityRollbackBuffers.Count <= retainedFrames.Count) return;

            var staleFrames = _staleFramesScratch;
            staleFrames.Clear();
            foreach (var frame in _entityRollbackBuffers.Keys)
            {
                if (retainedFrames.BinarySearch(frame) < 0) staleFrames.Add(frame);
            }

            for (int i = 0; i < staleFrames.Count; i++)
            {
                _entityRollbackBuffers.Remove(staleFrames[i]);
            }
        }
    }
}
