using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 混合同步适配器（客户端预测模式）。
    ///
    /// **当前状态（2026-07-20 核校）：未完成的通用框架适配器。**
    ///
    /// 本类属于 coordinator 包的通用会话协调框架（与 <see cref="LocalSyncAdapter"/>、
    /// <see cref="RemoteSyncAdapter"/> 一起提供 SyncMode=Hybrid 时的统一抽象）。
    /// 类内 4 处 TODO（SubmitInput/Tick/Reconcile/GetAllEntityStates）尚未实现，
    /// 本地预测循环、对账比对与预测状态读取均为空壳。
    ///
    /// **重要：MOBA demo 的生产战斗路径不经过本类。** MOBA 的 RemoteDriven 预测通路
    /// 由 <c>com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule</c>
    /// （框架级 IHostRuntimeModule）+ view.runtime 的
    /// <c>RemoteDrivenRollbackRegistryFactory</c> / <c>RemoteDrivenStateHashFactory</c>
    /// / <c>RemoteDrivenPredictionContextBinder</c> 实现，不经 coordinator 的 ISyncAdapter 体系。
    /// Shooter demo 也走自己的 <c>ShooterClientPredictionRuntimeAdapter</c>。
    ///
    /// 因此这 4 处 TODO 是**通用框架未来补全工作**，不阻塞演示级联机或任何当前 demo。
    /// 补全前请先确认 coordinator 通用会话框架有实际接入方（目前没有 demo 用 SyncMode=Hybrid
    /// 走 SessionCoordinator 路径），避免投入产出比错配。
    ///
    /// 设计意图（补全时应遵循）：
    /// - 客户端在本地执行预测。
    /// - 输入发送到服务端。
    /// - 服务端校验并发送校正。
    /// - 客户端根据服务端状态校正预测结果。
    ///
    /// 适用场景（补全后）：
    /// - 带客户端预测的在线多人玩法。
    /// - 降低感知延迟。
    /// - 服务端仍保持权威。
    /// </summary>
    public sealed class HybridSyncAdapter : IPredictionSyncAdapter
    {
        private readonly IWorld _world;
        private readonly SessionConfig _config;
        private readonly SessionRuntimePolicy _runtimePolicy;
        private ISessionCoordinator _coordinator;
        private ILogicWorldDriverBridge _driverHost;

        private double _renderTime;
        private int _localPlayerId;
        private IRemoteBattleSyncTransport _transport;
        private bool _predictionEnabled;
        private NetworkEndpoint _endpoint;
        private long _roomId;
        private long _playerId;

        // 用于预测的输入缓冲区。
        private readonly Queue<PlayerInput> _inputBuffer = new();

        // 预测状态。
        private int _lastConfirmedFrame;
        private int _predictedFrame;
        private readonly List<SnapshotEntityState> _confirmedSnapshot = new();

        // 校正状态。
        private bool _needsReconciliation;
        private SnapshotEntityState[] _serverCorrection;

        // ============== ISyncAdapter 实现 ==============

        public Core.SyncMode Mode => Core.SyncMode.Hybrid;

        public int CurrentFrame => _driverHost?.CurrentFrame ?? _predictedFrame;

        public double LogicTimeSeconds => _driverHost?.LogicTimeSeconds ?? 0;

        public double RenderTimeSeconds => _renderTime;

        public int LocalPlayerId => _localPlayerId;

        // ============== IRemoteSyncAdapter 实现 ==============

        public bool IsConnected => _transport?.IsConnected == true;

        // ============== IPredictionSyncAdapter 实现 ==============

        public bool IsPredictionEnabled => _predictionEnabled;

        public int PredictionAheadFrames => _predictionEnabled ? _runtimePolicy.MaxPredictionAheadFrames : 0;

        // ============== 事件 ==============

        public event Action<int, double> OnFrameSync;
        public event Action<bool> OnConnectionChanged;
        public event Action<SnapshotEntityState[]> OnServerSnapshot;

        public HybridSyncAdapter(IWorld world, in SessionConfig config)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _config = config;
            _runtimePolicy = config.ResolveRuntimePolicy();
            _localPlayerId = config.LocalPlayerId;
            _renderTime = 0;
            BindTransport(ResolveTransport(world));
            _predictionEnabled = _runtimePolicy.EnableClientPrediction;
            _lastConfirmedFrame = 0;
            _predictedFrame = 0;
            _needsReconciliation = false;
        }

        public void Attach(ISessionCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void Attach(ISessionCoordinator coordinator, ILogicWorldDriverBridge driverHost)
        {
            _coordinator = coordinator;
            _driverHost = driverHost;
        }

        public void SetLogicWorldDriver(ILogicWorldDriverBridge driverHost)
        {
            _driverHost = driverHost;
        }

        // ============== IPredictionSyncAdapter 方法 ==============

        public void SetPredictionEnabled(bool enabled)
        {
            _predictionEnabled = enabled && _runtimePolicy.SupportsPrediction;
        }

        public void TriggerReconciliation(int confirmedFrame, SnapshotEntityState[] serverState)
        {
            _serverCorrection = serverState;
            _needsReconciliation = true;
            _lastConfirmedFrame = confirmedFrame;

            _confirmedSnapshot.Clear();
            if (serverState != null)
            {
                _confirmedSnapshot.AddRange(serverState);
            }
        }

        // ============== IRemoteSyncAdapter 方法 ==============

        public void Connect(NetworkEndpoint endpoint, long roomId, long playerId)
        {
            _endpoint = endpoint;
            _roomId = roomId;
            _playerId = playerId;
            _localPlayerId = (int)playerId;

            var connected = _transport.Connect(endpoint, roomId, playerId, _runtimePolicy.EffectiveSyncMode);
            if (!connected)
            {
                OnConnectionChanged?.Invoke(false);
            }
        }

        public void Disconnect()
        {
            _transport?.Disconnect();
            _inputBuffer.Clear();
        }

        // ============== 输入处理 ==============

        public void SubmitInput(PlayerInput input)
        {
            lock (_inputBuffer)
            {
                _inputBuffer.Enqueue(input);
            }

            // 本地预测。
            if (_predictionEnabled)
            {
                _predictedFrame++;
                // TODO: 将输入应用到本地预测状态。
            }

            // 发送到服务端。
            if (_transport?.IsConnected == true)
            {
                _transport.SubmitInput(input);
            }
        }

        // ============== 帧更新 ==============

        public void Tick(float deltaTime)
        {
            // 更新渲染时间。
            _renderTime += deltaTime;

            _transport?.Tick(deltaTime);

            if (_transport?.IsConnected != true)
                return;

            // 处理本地预测。
            if (_predictionEnabled)
            {
                // TODO: 为预测执行本地模拟。
            }

            // 检查是否需要校正。
            if (_needsReconciliation)
            {
                Reconcile();
            }
        }

        private void Reconcile()
        {
            if (_serverCorrection == null || _coordinator == null)
                return;

            // 查找并校正差异。
            foreach (var serverState in _serverCorrection)
            {
                if (serverState.EntityId == _localPlayerId)
                {
                    // 本地玩家状态，检查是否不同步。
                    // TODO: 与预测状态比较，并在需要时校正。
                    _lastConfirmedFrame = _predictedFrame;
                    break;
                }
            }

            _needsReconciliation = false;
            _serverCorrection = null;
        }

        /// <summary>
        /// 写入服务端确认（由网络处理器调用）。
        /// </summary>
        public void FeedServerConfirmation(int serverFrame, SnapshotEntityState[] states)
        {
            TriggerReconciliation(serverFrame, states);
            OnServerSnapshot?.Invoke(states);
            OnFrameSync?.Invoke(serverFrame, 0);
        }

        public SnapshotEntityState[] GetAllEntityStates()
        {
            if (_driverHost != null)
            {
                return _driverHost.GetAllEntityStates();
            }

            if (_needsReconciliation)
            {
                return _confirmedSnapshot.ToArray();
            }

            // TODO: 预测实现后返回预测快照。
            return _confirmedSnapshot.ToArray();
        }

        public void Dispose()
        {
            Disconnect();
            UnbindTransport();
            _coordinator = null;
            _driverHost = null;
            _inputBuffer.Clear();
            _confirmedSnapshot.Clear();
            _serverCorrection = null;

            OnConnectionChanged = null;
            OnFrameSync = null;
            OnServerSnapshot = null;
        }

        private static IRemoteBattleSyncTransport ResolveTransport(IWorld world)
        {
            if (world?.Services != null && world.Services.TryResolve<IRemoteBattleSyncTransport>(out var transport) && transport != null)
            {
                return transport;
            }

            return NullRemoteBattleSyncTransport.Instance;
        }

        private void BindTransport(IRemoteBattleSyncTransport transport)
        {
            _transport = transport ?? NullRemoteBattleSyncTransport.Instance;
            _transport.OnConnectionChanged += HandleConnectionChanged;
            _transport.OnServerSnapshot += HandleServerSnapshot;
            _transport.OnServerConfirmation += FeedServerConfirmation;
        }

        private void UnbindTransport()
        {
            if (_transport == null)
            {
                return;
            }

            _transport.OnConnectionChanged -= HandleConnectionChanged;
            _transport.OnServerSnapshot -= HandleServerSnapshot;
            _transport.OnServerConfirmation -= FeedServerConfirmation;
            _transport = NullRemoteBattleSyncTransport.Instance;
        }

        private void HandleConnectionChanged(bool connected)
        {
            OnConnectionChanged?.Invoke(connected);
        }

        private void HandleServerSnapshot(int serverFrame, SnapshotEntityState[] states)
        {
            TriggerReconciliation(serverFrame, states);
            OnServerSnapshot?.Invoke(states);
            OnFrameSync?.Invoke(serverFrame, 0);
        }
    }
}
