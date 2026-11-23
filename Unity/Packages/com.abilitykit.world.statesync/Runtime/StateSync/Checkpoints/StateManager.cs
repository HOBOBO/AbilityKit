using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Diff;
using AbilityKit.Ability.StateSync.Snapshot;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 在调用方指定的模拟帧捕获和恢复已注册实体的本地状态。
    /// 由同一模拟执行线驱动；内部锁不保护业务回调，不负责准入、输入确认或重演。
    /// </summary>
    public sealed class StateManager : IStateManager
    {
        /// <summary>
        /// 可选诊断回调，在管理器内部锁之外调用。
        /// </summary>
        public Action<string> Log;

        private readonly Dictionary<long, IRollbackable> _rollbackables = new Dictionary<long, IRollbackable>();
        private KeyValuePair<long, IRollbackable>[] _captureRegistrationSnapshot;
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

        /// <summary>
        /// 使用指定的本地快照缓冲及可选的快照差分提供器。
        /// </summary>
        public StateManager(SnapshotBuffer snapshotBuffer, StateDiffProvider diffProvider = null)
        {
            _snapshotBuffer = snapshotBuffer ?? throw new ArgumentNullException(nameof(snapshotBuffer));
            _diffProvider = diffProvider ?? new StateDiffProvider();
        }

        /// <summary>
        /// 获取仍有快照和实体回滚记录的帧数，同时清理已淘汰的回滚记录。
        /// </summary>
        public int RetainedRollbackFrameCount
        {
            get
            {
                lock (_lock)
                {
                    TrimRollbackBuffers();
                    return _entityRollbackBuffers.Count;
                }
            }
        }

        /// <summary>
        /// 注册实体；重复标识保留已注册的实例。
        /// </summary>
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
                    _captureRegistrationSnapshot = null;
                }
            }

            Log?.Invoke(registered
                ? $"[StateManager] Registered entity {entity.EntityId} with key {entity.SnapshotKey}"
                : $"[StateManager] Entity {entity.EntityId} already registered");
        }

        /// <summary>
        /// 注销实体，不修改已捕获的历史记录。
        /// </summary>
        public void UnregisterRollbackable(long entityId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _rollbackables.Remove(entityId);
                if (removed) _captureRegistrationSnapshot = null;
            }

            if (removed)
            {
                Log?.Invoke($"[StateManager] Unregistered entity {entityId}");
            }
        }

        /// <summary>
        /// 按帧保存本次捕获开始时已注册的实体；捕获回调在锁外执行。
        /// </summary>
        public void CaptureState(int frame)
        {
            KeyValuePair<long, IRollbackable>[] rollbackables;
            lock (_lock)
            {
                rollbackables = _captureRegistrationSnapshot ??=
                    CreateCaptureRegistrationSnapshot();
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

            lock (_lock)
            {
                _snapshotBuffer.Store(frame, snapshot);
                _entityRollbackBuffers[frame] = entityRollbackData;
                TrimRollbackBuffers();
            }

            Log?.Invoke($"[StateManager] Captured state for frame={frame} with {entityRollbackData.Count} entities");
        }

        /// <summary>
        /// 找不到世界快照或该帧的实体记录表时返回失败；记录表为空仍可成功。
        /// 仅恢复仍注册且提供了状态对象的实体；回调异常向外传播，不补偿已完成的恢复。
        /// </summary>
        public bool TryRestore(int frame)
        {
            Dictionary<long, byte[]> entityRollbackData = null;
            Dictionary<long, IRollbackable> rollbackables = null;
            bool hasSnapshot;
            lock (_lock)
            {
                hasSnapshot = _snapshotBuffer.Contains(frame);
                if (hasSnapshot && _entityRollbackBuffers.TryGetValue(frame, out var storedRollbackData))
                {
                    entityRollbackData = new Dictionary<long, byte[]>(storedRollbackData);
                    rollbackables = new Dictionary<long, IRollbackable>(_rollbackables);
                }
            }

            if (entityRollbackData == null)
            {
                Log?.Invoke(hasSnapshot
                    ? $"[StateManager] No entity rollback data found for frame={frame}"
                    : $"[StateManager] No snapshot found for frame={frame}");
                return false;
            }

            RestoreSnapshot(frame, entityRollbackData, rollbackables);
            Log?.Invoke($"[StateManager] Restored state for frame={frame}");
            return true;
        }

        /// <summary>
        /// 比较缓冲中的两份世界快照，不包含逐实体回滚记录；任一快照缺失时返回空值。
        /// </summary>
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

        /// <summary>
        /// 返回世界快照的序列化结果，不包含逐实体回滚记录；帧不存在时返回空值。
        /// </summary>
        public byte[] GetFullState(int frame)
        {
            return _snapshotBuffer.TryGet(frame, out var snapshot) ? snapshot.ToBytes() : null;
        }

        /// <summary>
        /// 返回快照缓冲中按帧号升序保留的帧。
        /// </summary>
        public IReadOnlyList<int> GetCapturedFrames()
        {
            return _snapshotBuffer.GetCapturedFrames();
        }

        /// <summary>
        /// 清空世界快照和逐实体记录，保留实体注册。
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _snapshotBuffer.Clear();
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

        private KeyValuePair<long, IRollbackable>[] CreateCaptureRegistrationSnapshot()
        {
            var registrations = new KeyValuePair<long, IRollbackable>[_rollbackables.Count];
            var index = 0;
            foreach (var registration in _rollbackables)
            {
                registrations[index++] = registration;
            }

            return registrations;
        }

        private void TrimRollbackBuffers()
        {
            if (_entityRollbackBuffers.Count == 0) return;

            var retainedFrames = _retainedFramesScratch;
            _snapshotBuffer.CopyCapturedFrames(retainedFrames);
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
