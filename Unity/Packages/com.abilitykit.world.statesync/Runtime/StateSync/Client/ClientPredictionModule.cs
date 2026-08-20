using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 客户端预测模块默认实现
    /// 管理所有实体的预测状态，协调输入、预测、回滚
    /// </summary>
    public sealed class ClientPredictionModule : IClientPredictionModule
    {
        private IClientPredictionConfig _config;
        private readonly Dictionary<int, IEntityPredictionState> _entityStates = new Dictionary<int, IEntityPredictionState>();
        private readonly List<IPredictableEntity> _predictableEntities = new List<IPredictableEntity>();
        private readonly ClientPredictionModuleBufferOptions _configuredBufferOptions;
        private readonly bool? _rollbackOverride;
        private ClientPredictionModuleBufferOptions _bufferOptions;
        private IInputBuffer<IInputCommand> _inputBuffer;
        private bool _enableRollback;

        private int _localPlayerId;
        private int _currentFrame;
        private int _confirmedFrame;
        private bool _initialized;
        private bool _disposed;

        public int LocalPlayerId => _localPlayerId;
        public int CurrentFrame => _currentFrame;
        public int ConfirmedFrame => _confirmedFrame;
        public bool HasUnconfirmedPrediction => _currentFrame > _confirmedFrame;
        public ClientPredictionModuleBufferOptions BufferOptions => _bufferOptions;
        public bool InputBufferEnabled => _inputBuffer != null;
        public IBufferCapacityControl InputBufferCapacityControl =>
            _inputBuffer as IBufferCapacityControl;
        public bool EntitySnapshotHistoryEnabled =>
            _bufferOptions != null
            && _bufferOptions.Has(ClientPredictionModuleBufferFeatures.EntitySnapshotHistory);
        public bool RollbackEnabled => _enableRollback;

        public event Action<StateChangeEvent> OnStateChanged;
        public event Action<RollbackEvent> OnRollback;
        public event Action<int, int> OnSnapshotApplied;

        public ClientPredictionModule(
            ClientPredictionModuleBufferOptions bufferOptions = null,
            bool? enableRollback = null)
        {
            _configuredBufferOptions = bufferOptions;
            _rollbackOverride = enableRollback;
        }

        public void Initialize(IClientPredictionConfig config)
        {
            if (_initialized)
                throw new InvalidOperationException("ClientPredictionModule already initialized");

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _localPlayerId = config.LocalPlayerId;
            _bufferOptions = _configuredBufferOptions
                ?? ClientPredictionModuleBufferOptions.CreateDefault(
                    config.MaxInputBufferSize,
                    config.MaxPredictionFrames);
            _inputBuffer = _bufferOptions.CreateInputBuffer(_localPlayerId);
            _enableRollback = (_rollbackOverride ?? config.EnableRollback)
                && _bufferOptions.Has(ClientPredictionModuleBufferFeatures.EntitySnapshotHistory);
            _currentFrame = 0;
            _confirmedFrame = -1;
            _initialized = true;
        }

        public void RegisterEntity(IPredictableEntity entity)
        {
            if (!_initialized)
                throw new InvalidOperationException("ClientPredictionModule not initialized");

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_entityStates.ContainsKey(entity.EntityId))
            {
                return; // 已注册
            }

            // 创建实体预测状态
            var state = new EntityPredictionState(
                entity,
                _bufferOptions.CreateEntitySnapshotStore(entity.EntityId),
                _enableRollback);

            // 注册处理器
            var handlers = entity.GetPredictionHandlers();
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    state.RegisterHandler(handler);
                }
            }

            // 订阅实体状态变化
            state.OnSlotChanged += HandleEntitySlotChanged;
            state.OnRollback += HandleEntityRollback;

            _entityStates[entity.EntityId] = state;
            _predictableEntities.Add(entity);
        }

        public void UnregisterEntity(int entityId)
        {
            if (_entityStates.TryGetValue(entityId, out var state))
            {
                state.OnSlotChanged -= HandleEntitySlotChanged;
                state.OnRollback -= HandleEntityRollback;
                _entityStates.Remove(entityId);
            }

            _predictableEntities.RemoveAll(e => e.EntityId == entityId);
        }

        public void SubmitInput(IInputCommand input)
        {
            if (!_initialized || input == null)
                return;

            _inputBuffer?.Store(_currentFrame, input);
        }

        public void Tick(int frame)
        {
            if (!_initialized)
                return;

            _currentFrame = frame;

            // 获取当前帧的输入
            if (_inputBuffer != null && _inputBuffer.TryGet(frame, out var input))
            {
                // 为本地玩家控制的实体执行预测
                foreach (var kvp in _entityStates)
                {
                    var entityState = kvp.Value;

                    // 只有本地玩家的实体才执行输入预测
                    if (entityState.IsLocalPlayer)
                    {
                        entityState.Predict(input, frame);
                    }
                }
            }

            // 为所有实体快照当前状态
            foreach (var kvp in _entityStates)
            {
                kvp.Value.CaptureSnapshot(frame);
            }

            // 发布待处理的状态变化
            PublishPendingStateChanges();

            // 推进帧
            _confirmedFrame = _currentFrame;
        }

        public void ApplyServerSnapshot(int serverFrame, ServerEntitySnapshot[] snapshots)
        {
            if (!_initialized || snapshots == null)
                return;

            var previousFrame = _currentFrame;

            foreach (var snapshot in snapshots)
            {
                if (_entityStates.TryGetValue(snapshot.EntityId, out var entityState))
                {
                    var hadRollback = entityState.ApplyServerState(serverFrame, snapshot);

                    if (hadRollback)
                    {
                        // 发布回滚事件
                        var rollbackEvent = new RollbackEvent
                        {
                            EntityId = snapshot.EntityId,
                            FromFrame = previousFrame,
                            ToFrame = serverFrame,
                            Reason = RollbackReason.ServerCorrection
                        };
                        OnRollback?.Invoke(rollbackEvent);
                    }

                    OnSnapshotApplied?.Invoke(snapshot.EntityId, serverFrame);
                }
            }

            // 更新当前帧
            _currentFrame = serverFrame;
        }

        public AbilityKit.Ability.StateSync.Prediction.StateSlots GetPredictedSlots(int entityId)
        {
            if (_entityStates.TryGetValue(entityId, out var state))
            {
                return state.CurrentSlots;
            }
            return null;
        }

        public IEntityPredictionState GetEntityState(int entityId)
        {
            _entityStates.TryGetValue(entityId, out var state);
            return state;
        }

        public IBufferCapacityControl GetEntitySnapshotCapacityControl(int entityId)
        {
            return _entityStates.TryGetValue(entityId, out var state)
                ? (state as EntityPredictionState)?.SnapshotCapacityControl
                : null;
        }

        private void HandleEntitySlotChanged(string slotName, object oldValue, object newValue)
        {
            // 槽位变化已通过实体状态收集
        }

        private void HandleEntityRollback(int fromFrame, int toFrame)
        {
            // 已在 ApplyServerSnapshot 中处理
        }

        private void PublishPendingStateChanges()
        {
            foreach (var kvp in _entityStates)
            {
                var changes = kvp.Value.GetPendingStateChanges();
                foreach (var change in changes)
                {
                    OnStateChanged?.Invoke(change);
                }
                kvp.Value.ClearPendingStateChanges();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var kvp in _entityStates)
            {
                kvp.Value.OnSlotChanged -= HandleEntitySlotChanged;
                kvp.Value.OnRollback -= HandleEntityRollback;
            }

            _entityStates.Clear();
            _predictableEntities.Clear();
            _inputBuffer?.Clear();

            OnStateChanged = null;
            OnRollback = null;
            OnSnapshotApplied = null;
        }
    }
}
