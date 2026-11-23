using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 按实体协调本地槽位预测、历史记录与服务器校正；不重演待确认输入，不负责恢复完整世界。
    /// 调用、处理器和事件在调用线程同步执行；由宿主串行驱动，不应在回调中重入或修改正在遍历的实体集合。
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

        /// <summary>
        /// 初始化时从配置读入的玩家标识；不用于替代实体自己的本地控制标记。
        /// </summary>
        public int LocalPlayerId => _localPlayerId;
        /// <summary>
        /// 模块最近由推进或校正设置的本地游标，不保证单调递增。
        /// </summary>
        public int CurrentFrame => _currentFrame;
        /// <summary>
        /// 模块本地完成推进的游标；初始化为 -1，不表示服务器已接收或确认输入。
        /// </summary>
        public int ConfirmedFrame => _confirmedFrame;
        /// <summary>
        /// 仅比较当前游标是否大于本地确认游标，不表示存在尚未获服务器确认的指令。
        /// </summary>
        public bool HasUnconfirmedPrediction => _currentFrame > _confirmedFrame;
        /// <summary>
        /// 初始化后使用的缓冲选项；初始化前为空。
        /// </summary>
        public ClientPredictionModuleBufferOptions BufferOptions => _bufferOptions;
        /// <summary>
        /// 是否已创建输入缓冲；不代表输入已被消费或确认。
        /// </summary>
        public bool InputBufferEnabled => _inputBuffer != null;
        /// <summary>
        /// 返回当前缓冲的容量控制引用；缓冲未创建或不支持控制时返回空。
        /// </summary>
        public IBufferCapacityControl InputBufferCapacityControl =>
            _inputBuffer as IBufferCapacityControl;
        /// <summary>
        /// 是否配置实体历史功能；不保证某一实体在指定帧已有快照。
        /// </summary>
        public bool EntitySnapshotHistoryEnabled =>
            _bufferOptions != null
            && _bufferOptions.Has(ClientPredictionModuleBufferFeatures.EntitySnapshotHistory);
        /// <summary>
        /// 是否请求回滚且开启实体历史；实际回滚还要求可用的早期快照。
        /// </summary>
        public bool RollbackEnabled => _enableRollback;

        /// <summary>
        /// 推进时同步发布各实体的待处理槽位变化；同一实体按收集顺序，实体间无排序契约。
        /// 订阅方异常向调用方传播，可能中止发布和清理；事件值不是独立深拷贝。
        /// </summary>
        public event Action<StateChangeEvent> OnStateChanged;
        /// <summary>
        /// 服务器快照导致实体实际恢复旧槽位时同步发布；目标帧使用本次校正参数。
        /// </summary>
        public event Action<RollbackEvent> OnRollback;
        /// <summary>
        /// 按快照数组顺序，对已注册并成功应用的实体同步发布实体标识和校正帧。
        /// </summary>
        public event Action<int, int> OnSnapshotApplied;

        /// <summary>
        /// 创建未初始化模块；缓冲选项为空时从配置取容量，回滚覆盖值为空时沿用配置。
        /// 覆盖值为真也不能绕过实体历史开关。
        /// </summary>
        public ClientPredictionModule(
            ClientPredictionModuleBufferOptions bufferOptions = null,
            bool? enableRollback = null)
        {
            _configuredBufferOptions = bufferOptions;
            _rollbackOverride = enableRollback;
        }

        /// <summary>
        /// 读取配置并创建输入缓冲，将当前帧和本地确认帧分别设为 0 和 -1。
        /// 重复初始化或空配置会抛异常；缓冲工厂失败直接传播。
        /// </summary>
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

        /// <summary>
        /// 按实体标识登记，重复标识不替换，处理器按业务返回顺序注册。
        /// 未初始化或实体为空时抛异常；模块不负责释放业务实体。
        /// </summary>
        public void RegisterEntity(IPredictableEntity entity)
        {
            if (!_initialized)
                throw new InvalidOperationException("ClientPredictionModule not initialized");

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_entityStates.ContainsKey(entity.EntityId))
            {
                return;
            }

            var state = new EntityPredictionState(
                entity,
                _bufferOptions.CreateEntitySnapshotStore(entity.EntityId),
                _enableRollback);

            var handlers = entity.GetPredictionHandlers();
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    state.RegisterHandler(handler);
                }
            }

            state.OnSlotChanged += HandleEntitySlotChanged;
            state.OnRollback += HandleEntityRollback;

            _entityStates[entity.EntityId] = state;
            _predictableEntities.Add(entity);
        }

        /// <summary>
        /// 移除登记及模块事件订阅，未知标识无操作；不会释放实体或自动清除共享存储。
        /// </summary>
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

        /// <summary>
        /// 按模块当前帧将输入交给缓冲，不直接执行预测；未初始化、空输入或禁用缓冲时忽略。
        /// 默认缓冲保留输入引用；提交方应使指令在保留期内不可变。
        /// </summary>
        public void SubmitInput(IInputCommand input)
        {
            if (!_initialized || input == null)
                return;

            _inputBuffer?.Store(_currentFrame, input);
        }

        /// <summary>
        /// 未初始化时无操作；设置指定帧，若该帧有输入则只预测标记为本地控制的实体。
        /// 随后为所有登记实体尝试记录槽位快照、发布变化，最后更新本地确认游标；不执行服务器输入确认。
        /// </summary>
        public void Tick(int frame)
        {
            if (!_initialized)
                return;

            _currentFrame = frame;

            /*
             * 只读取当前帧的输入；快照校正不会回到这里重演历史。
             */
            if (_inputBuffer != null && _inputBuffer.TryGet(frame, out var input))
            {
                foreach (var kvp in _entityStates)
                {
                    var entityState = kvp.Value;

                    if (entityState.IsLocalPlayer)
                    {
                        entityState.Predict(input, frame);
                    }
                }
            }

            foreach (var kvp in _entityStates)
            {
                kvp.Value.CaptureSnapshot(frame);
            }

            PublishPendingStateChanges();

            /*
             * 这是本地推进完成标记，不是服务器输入确认。
             */
            _confirmedFrame = _currentFrame;
        }

        /// <summary>
        /// 按数组顺序校正已登记实体，使用参数指定的校正帧；未初始化或数组为空引用时忽略。
        /// 完成后设置当前帧而不修改本地确认帧；不筛选过期帧、不重演输入，异常可留下部分应用结果。
        /// </summary>
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

            _currentFrame = serverFrame;
        }

        /// <summary>
        /// 取得实体的实时可变槽位引用，不是历史副本；实体未登记时返回空。
        /// </summary>
        public AbilityKit.Ability.StateSync.Prediction.StateSlots GetPredictedSlots(int entityId)
        {
            if (_entityStates.TryGetValue(entityId, out var state))
            {
                return state.CurrentSlots;
            }
            return null;
        }

        /// <summary>
        /// 取得仍由模块跟踪的实体状态引用；实体未登记时返回空。
        /// </summary>
        public IEntityPredictionState GetEntityState(int entityId)
        {
            _entityStates.TryGetValue(entityId, out var state);
            return state;
        }

        /// <summary>
        /// 取得该实体历史存储的容量控制引用；实体未知、历史禁用或存储不支持时返回空。
        /// </summary>
        public IBufferCapacityControl GetEntitySnapshotCapacityControl(int entityId)
        {
            return _entityStates.TryGetValue(entityId, out var state)
                ? (state as EntityPredictionState)?.SnapshotCapacityControl
                : null;
        }

        private void HandleEntitySlotChanged(string slotName, object oldValue, object newValue)
        {
            /*
             * 模块在推进末尾统一发布实体已收集的变化。
             */
        }

        private void HandleEntityRollback(int fromFrame, int toFrame)
        {
            /*
             * 本模块的校正回滚事件由快照应用入口发布。
             */
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

        /// <summary>
        /// 首次调用清空登记、输入记录和模块订阅，后续调用无操作。
        /// 不会释放业务对象或自定义存储；调用方应在释放后停止使用模块。
        /// </summary>
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
