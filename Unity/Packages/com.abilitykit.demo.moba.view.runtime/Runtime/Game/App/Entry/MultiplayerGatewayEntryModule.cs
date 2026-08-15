using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.World.ECS;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.View.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game
{
    public interface IMultiplayerGatewayRuntime
    {
        bool IsRemoteActive { get; }
        ConnectionState ConnectionState { get; }
        MultiplayerRecoveryState RecoveryState { get; }
        NetworkSessionRecoveryDecision RecoveryDecision { get; }
        NetworkSessionRecoveryDiagnostics RecoveryDiagnostics { get; }
        void ResetReconnect();
    }

    public enum MultiplayerRecoveryState
    {
        None = 0,
        ReconnectScheduled = 1,
        ReconnectAttempt = 2,
        ReconnectExhausted = 3,
        RestoringRoom = 4,
        RestoringLoadingBarrier = 5,
        RestoringBattleSnapshot = 6,
        Recovered = 7
    }

    /// <summary>
    /// 将连接层恢复信号协调为大厅和房间层可执行动作。
    /// 该运行时不持有网络客户端，具体房间恢复操作由接入方委托提供。
    /// </summary>
    internal sealed class MultiplayerGatewayRecoveryRuntime :
        INetworkSessionRecoverySignalSink
    {
        private readonly Func<CancellationToken, Task<MultiplayerRoomRestoreResult>> _restoreRoom;
        private readonly Func<string> _correlationContext;
        private readonly Action<Exception> _failure;
        private readonly NetworkSessionRecoveryRuntime<bool> _runtime;
        private string _lastCorrelationContext = string.Empty;

        internal MultiplayerGatewayRecoveryRuntime(
            Func<CancellationToken, Task<MultiplayerRoomRestoreResult>> restoreRoom,
            Func<string> correlationContext = null,
            Action<Exception> failure = null,
            NetworkSessionRecoveryOptions options = null)
        {
            _restoreRoom = restoreRoom ?? throw new ArgumentNullException(nameof(restoreRoom));
            _correlationContext = correlationContext ?? (() => string.Empty);
            _failure = failure;
            var actions = new NetworkSessionRecoveryActionRouter<bool>(
                    new NetworkSessionRecoveryActionRouterOptions<bool>
                    {
                        UnhandledActionPolicy = NetworkSessionRecoveryUnhandledActionPolicy.ReturnUnhandled,
                        HandlerFailurePolicy = NetworkSessionRecoveryHandlerFailurePolicy.CaptureAndReturn,
                        CancellationPolicy = NetworkSessionRecoveryCancellationPolicy.ReturnCancelled
                    })
                .Register(NetworkSessionRecoveryAction.WaitForReconnect, ExecuteWaitForReconnectAsync)
                .Register(NetworkSessionRecoveryAction.RebuildSession, ExecuteRebuildSessionAsync);
            _runtime = new NetworkSessionRecoveryRuntime<bool>(
                actions,
                options ?? CreateDefaultOptions(),
                new NetworkSessionRecoveryRuntimeOptions
                {
                    ExecutionMode = NetworkSessionRecoveryExecutionMode.Automatic,
                    CancelSupersededExecution = true,
                    CancelExecutionOnReset = true,
                    SuppressStaleExecutionCompletion = true
                });
        }

        internal MultiplayerRecoveryState State { get; private set; }
        internal NetworkSessionRecoveryDecision Decision => _runtime.CurrentDecision;
        internal NetworkSessionRecoveryDiagnostics Diagnostics => _runtime.GetRecoveryDiagnostics();
        internal NetworkSessionRecoveryRuntimeDiagnostics RuntimeDiagnostics =>
            _runtime.GetRuntimeDiagnostics();

        /// <summary>最近一次已发布动作的执行任务，供生命周期收口和测试等待。</summary>
        internal Task PendingExecution => _runtime.PendingExecution;

        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            var published = _runtime.TryReport(in signal, out decision);
            if (!published) return false;
            if (!string.IsNullOrWhiteSpace(signal.CorrelationContext))
            {
                _lastCorrelationContext = signal.CorrelationContext;
            }
            return true;
        }

        internal void ObserveRoomFlowState(MultiplayerRoomFlowState state)
        {
            if (State != MultiplayerRecoveryState.RestoringRoom &&
                State != MultiplayerRecoveryState.RestoringLoadingBarrier &&
                State != MultiplayerRecoveryState.RestoringBattleSnapshot)
            {
                return;
            }

            if (state == MultiplayerRoomFlowState.InLobby ||
                state == MultiplayerRoomFlowState.InBattle)
            {
                CompleteRecovery(MultiplayerRecoveryState.Recovered, "room-flow-restored");
            }
        }

        internal void Reset()
        {
            _runtime.Reset();
            _lastCorrelationContext = string.Empty;
            State = MultiplayerRecoveryState.None;
        }

        private static NetworkSessionRecoveryOptions CreateDefaultOptions()
        {
            var policy = new NetworkSessionRecoveryRulePolicy();
            var restoreRoom = new NetworkSessionRecoveryDirective(
                NetworkSessionRecoveryAction.RebuildSession,
                priority: 20,
                terminatesCurrentSession: false,
                reason: "连接恢复后重建权威房间会话。");
            policy.SetRule(NetworkSessionRecoverySignalKind.ConnectionRestored, in restoreRoom);
            return new NetworkSessionRecoveryOptions
            {
                AllowEqualPriorityReplacement = true,
                Policy = policy
            };
        }

        private Task<bool> ExecuteWaitForReconnectAsync(
            NetworkSessionRecoveryExecutionContext execution,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = execution.Decision.Signal.Kind switch
            {
                NetworkSessionRecoverySignalKind.ReconnectAttemptStarted =>
                    MultiplayerRecoveryState.ReconnectAttempt,
                NetworkSessionRecoverySignalKind.ReconnectScheduled =>
                    MultiplayerRecoveryState.ReconnectScheduled,
                NetworkSessionRecoverySignalKind.ConnectionError =>
                    MultiplayerRecoveryState.ReconnectScheduled,
                _ => State
            };
            return Task.FromResult(true);
        }

        private async Task<bool> ExecuteRebuildSessionAsync(
            NetworkSessionRecoveryExecutionContext execution,
            CancellationToken cancellationToken)
        {
            var signal = execution.Decision.Signal;
            if (signal.Kind == NetworkSessionRecoverySignalKind.ReconnectExhausted)
            {
                State = MultiplayerRecoveryState.ReconnectExhausted;
                return false;
            }

            if (signal.Kind != NetworkSessionRecoverySignalKind.ConnectionRestored)
            {
                return false;
            }

            State = MultiplayerRecoveryState.RestoringRoom;
            try
            {
                var result = await _restoreRoom(cancellationToken);
                // 业务恢复实现即使没有主动观察取消，也不能在旧代次完成后继续回写状态。
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.HasActiveRoom)
                {
                    CompleteRecovery(
                        MultiplayerRecoveryState.None,
                        $"room-restore-completed:{result.Status}");
                    return false;
                }

                switch (result.Phase)
                {
                    case MultiplayerRoomPhase.Loading:
                    case MultiplayerRoomPhase.Starting:
                        State = MultiplayerRecoveryState.RestoringLoadingBarrier;
                        break;
                    case MultiplayerRoomPhase.InBattle:
                        CompleteRecovery(
                            MultiplayerRecoveryState.Recovered,
                            "battle-room-restored");
                        break;
                    default:
                        CompleteRecovery(
                            MultiplayerRecoveryState.Recovered,
                            "lobby-room-restored");
                        break;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                try { _failure?.Invoke(exception); } catch { }
                ReportRestoreFailure(exception);
                return false;
            }
        }

        private void CompleteRecovery(MultiplayerRecoveryState state, string detail)
        {
            State = state;
            // 完成信号会推进框架代次并取消旧动作，避免 Room Flow 与请求返回顺序不同造成状态覆盖。
            _runtime.CompleteRecovery(
                ResolveCorrelationContext(),
                detail);
        }

        private void ReportRestoreFailure(Exception exception)
        {
            var exhausted = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.ReconnectExhausted,
                SyncHealthSeverity.Error,
                exception: exception,
                correlationContext: ResolveCorrelationContext(),
                detail: "room-restore-failed");
            TryReport(in exhausted, out _);
        }

        private string ResolveCorrelationContext()
        {
            if (!string.IsNullOrWhiteSpace(_lastCorrelationContext))
            {
                return _lastCorrelationContext;
            }

            try { return _correlationContext() ?? string.Empty; }
            catch { return string.Empty; }
        }

    }

    public sealed class MultiplayerGatewayEntryModule :
        IGameEntryModule,
        IGameModuleTick<GameEntryModuleContext>,
        IMultiplayerGatewayRuntime
    {
        private readonly BattleGatewayConfigSO _config;
        private readonly DemoMultiplayerLaunchRequest _launchRequest;
        private NetworkSdkClient _sdkClient;
        private DedicatedThreadDispatcher _ioDispatcher;
        private CancellationTokenSource _lifetime;
        private ClientRoomStore _store;
        private GatewayRoomClient _client;
        private GatewayMultiplayerRoomSession _session;
        private ClientRoomSnapshotProvider _snapshotProvider;
        private MultiplayerRoomFlowController _controller;
        private MultiplayerBattleAssetLoader _assetLoader;
        private ClientRoomPushSynchronizer _pushSynchronizer;
        private LobbyBattleEntrySelection _selection;
        private MultiplayerGatewayRecoveryRuntime _recovery;
        private NetworkSdkClientRecoveryBinding _recoveryBinding;
        private MultiplayerGatewayRootServices _rootServices;
        private bool _recoverySignalsEnabled;

        public bool IsRemoteActive => _selection?.IsRemoteSelected == true;
        public ConnectionState ConnectionState =>
            _sdkClient != null ? _sdkClient.State : ConnectionState.Disconnected;
        public MultiplayerRecoveryState RecoveryState =>
            _recovery?.State ?? MultiplayerRecoveryState.None;
        public NetworkSessionRecoveryDecision RecoveryDecision =>
            _recovery?.Decision ?? default;
        public NetworkSessionRecoveryDiagnostics RecoveryDiagnostics =>
            _recovery?.Diagnostics ?? default;

        public MultiplayerGatewayEntryModule(
            BattleGatewayConfigSO config,
            DemoMultiplayerLaunchRequest launchRequest = null)
        {
            _config = config;
            _launchRequest = launchRequest;
        }

        public string Id => "game.entry.multiplayer-gateway";

        public void OnAttach(in GameEntryModuleContext ctx)
        {
            if (_config == null)
            {
                return;
            }

            ValidateConfig(_config, _launchRequest);

            _lifetime = new CancellationTokenSource();
            _ioDispatcher = new DedicatedThreadDispatcher("LobbyGatewayNetworkThread");
            var callbackDispatcher = UnityMainThreadDispatcher.CaptureCurrent();
            _sdkClient = new NetworkSdkBuilder()
                .UseTransportFactory(() => new TcpTransport())
                .ConfigureConnection(options =>
                {
                    options.FrameCodec = LengthPrefixedFrameCodec.Instance;
                    options.EnableKickHandling = true;
                    options.KickPushOpCode = RoomGatewayOpCodes.SessionKicked;
                    options.EnableReconnect = true;
                    options.ReconnectInitialDelay = TimeSpan.FromSeconds(1);
                    options.ReconnectMaxDelay = TimeSpan.FromSeconds(15);
                    options.ReconnectBackoffMultiplier = 2d;
                    options.ReconnectMaxAttempts =
                        AbilityKit.Network.Runtime.Sync.ReconnectBackoffPolicy.MaxAttempts;
                })
                .UseDispatchers(callbackDispatcher, _ioDispatcher)
                .Build();
            _store = new ClientRoomStore();
            _client = new GatewayRoomClient(
                _sdkClient,
                GatewayRoomOpCodes.Default);
#if UNITY_5_3_OR_NEWER
            _session = new GatewayMultiplayerRoomSession(
                _client,
                _store,
                reliableEventCheckpointStore:
                    MobaUnityReliableEventCheckpointStores.CreateBufferedFile(),
                ownsReliableEventCheckpointStore: true);
#else
            _session = new GatewayMultiplayerRoomSession(_client, _store);
#endif
            _snapshotProvider = new ClientRoomSnapshotProvider(_store);
            _assetLoader = new MultiplayerBattleAssetLoader(
                ResourcesBattleAssetLoadService.Default,
                dependencyProvider: ResourcesBattleAssetDependencyProvider.Default,
                mainThreadDispatcher: callbackDispatcher);
            _controller = new MultiplayerRoomFlowController(_session, _snapshotProvider, _assetLoader);
            _pushSynchronizer = new ClientRoomPushSynchronizer(
                _client,
                _store,
                RefreshCurrentRoomAsync);
            _recovery = new MultiplayerGatewayRecoveryRuntime(
                RestoreRoomAfterReconnectAsync,
                () => _controller?.CurrentRoomId ?? string.Empty,
                exception => Log.Exception(
                    exception,
                    "[MultiplayerGatewayEntryModule] Room recovery action failed."));

            _sdkClient.ServerPushReceived += HandleServerPush;
            _sdkClient.Disconnected += HandleDisconnected;
            _recoveryBinding = _sdkClient.BindRecoverySignals(
                _recovery,
                new NetworkSdkClientRecoveryBindingOptions
                {
                    CorrelationContextProvider =
                        () => _controller?.CurrentRoomId ?? string.Empty,
                    ReportingFailure = exception => Log.Exception(
                        exception,
                        "[MultiplayerGatewayEntryModule] Failed to report a recovery signal.")
                });
            _recoverySignalsEnabled = true;
            _controller.StateChanged += HandleRoomFlowStateChanged;
            ctx.Root.TryGetRef(out _selection);
            if (_selection != null)
            {
                _selection.Changed += HandleEntrySelectionChanged;
            }

            _rootServices = new MultiplayerGatewayRootServices(
                _config,
                _launchRequest,
                _store,
                _client,
                _session,
                _session,
                _snapshotProvider,
                _controller,
                _pushSynchronizer,
                _assetLoader,
                this);
            _rootServices.Publish(ctx.Root);
            ApplyEntrySelection();
        }

        public void Tick(in GameEntryModuleContext ctx, float deltaTime)
        {
            _sdkClient?.Tick(deltaTime);
        }

        public void OnDetach(in GameEntryModuleContext ctx)
        {
            _lifetime?.Cancel();
            SetRecoverySignalsEnabled(enabled: false, reset: true);
            _recoveryBinding?.Dispose();
            if (_selection != null)
            {
                _selection.Changed -= HandleEntrySelectionChanged;
            }

            if (_sdkClient != null)
            {
                _sdkClient.ServerPushReceived -= HandleServerPush;
                _sdkClient.Disconnected -= HandleDisconnected;
            }

            if (_controller != null)
            {
                _controller.StateChanged -= HandleRoomFlowStateChanged;
            }

            _rootServices?.Withdraw();

            _controller?.Dispose();
            _snapshotProvider?.Dispose();
            _session?.Dispose();
            _client?.Dispose();
            _sdkClient?.Dispose();
            _ioDispatcher?.Dispose();
            _lifetime?.Dispose();

            _rootServices = null;
            _recoveryBinding = null;
            _recovery = null;
            _pushSynchronizer = null;
            _selection = null;
            _controller = null;
            _assetLoader = null;
            _snapshotProvider = null;
            _session = null;
            _client = null;
            _store = null;
            _sdkClient = null;
            _ioDispatcher = null;
            _lifetime = null;
            _recoverySignalsEnabled = false;
        }

        public void ResetReconnect()
        {
            _recovery?.Reset();
            if (_recoveryBinding != null)
            {
                _recoveryBinding.Enabled = true;
                _recoverySignalsEnabled = true;
            }
            _sdkClient?.ResetReconnect();
        }

        private void HandleDisconnected()
        {
            // 断线仅提交当前位置，不删除检查点；后续重连仍需从该位置恢复。
            try
            {
                _session?.FlushReliableEventCheckpointsAsync(
                    ReliableEventCheckpointFlushTrigger.Disconnect).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Exception(
                    ex,
                    "[MultiplayerGatewayEntryModule] Failed to flush reliable-event checkpoint after disconnect.");
            }
        }

        private void HandleRoomFlowStateChanged(MultiplayerRoomFlowState state)
        {
            _recovery?.ObserveRoomFlowState(state);
        }

        private async Task<MultiplayerRoomRestoreResult> RestoreRoomAfterReconnectAsync(
            CancellationToken cancellationToken)
        {
            var controller = _controller;
            var spec = controller?.CurrentLaunchSpec;
            var lifetime = _lifetime;
            if (controller == null || spec == null || lifetime == null)
            {
                return new MultiplayerRoomRestoreResult(
                    string.Empty,
                    0UL,
                    0u,
                    MultiplayerRoomPhase.Lobby,
                    MultiplayerRoomRestoreNextStep.None,
                    MultiplayerRoomEntryKind.Reconnect,
                    canStart: false,
                    "No active room session requires recovery.",
                    MultiplayerRoomRestoreStatus.NoActiveRoom,
                    MultiplayerRoomRestoreErrorCode.NoAccountRoomMapping);
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetime.Token);
            var fallbackPlayerId = _config.RestoreFallbackPlayerId == 0u
                ? 1u
                : _config.RestoreFallbackPlayerId;
            return await controller.RestoreAsync(
                spec,
                fallbackPlayerId,
                linked.Token);
        }

        private void HandleEntrySelectionChanged()
        {
            ApplyEntrySelection();
        }

        private void ApplyEntrySelection()
        {
            if (_sdkClient == null)
            {
                return;
            }

            if (IsRemoteActive)
            {
                SetRecoverySignalsEnabled(enabled: true, reset: false);
                if (_sdkClient.State == ConnectionState.Disconnected)
                {
                    _sdkClient.Open(EffectiveHost(), EffectivePort());
                }

                return;
            }

            SetRecoverySignalsEnabled(enabled: false, reset: true);
            _controller?.Cancel();
            _store?.Reset();
            if (_sdkClient.State != ConnectionState.Disconnected)
            {
                _sdkClient.Close();
            }
        }

        private void SetRecoverySignalsEnabled(bool enabled, bool reset)
        {
            var binding = _recoveryBinding;
            if (binding == null) return;

            if (reset)
            {
                binding.Reset();
                _recovery?.Reset();
            }

            if (_recoverySignalsEnabled == enabled) return;
            binding.Enabled = enabled;
            _recoverySignalsEnabled = enabled;
        }

        private async void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            var synchronizer = _pushSynchronizer;
            var lifetime = _lifetime;
            if (synchronizer == null || lifetime == null)
            {
                return;
            }

            try
            {
                await synchronizer.HandleServerPushAsync(
                    opCode,
                    payload,
                    lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MultiplayerGatewayEntryModule] Failed to process Room push.");
            }
        }

        private Task RefreshCurrentRoomAsync(CancellationToken cancellationToken)
        {
            var roomId = _store?.Current?.RoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new InvalidOperationException("Cannot refresh Room snapshot before joining a room.");
            }

            return _session.RefreshSnapshotAsync(roomId, cancellationToken);
        }

        private static void ValidateConfig(
            BattleGatewayConfigSO config,
            DemoMultiplayerLaunchRequest launchRequest)
        {
            if (!config.TryValidateFormalLobby(out var error))
            {
                throw new InvalidOperationException(error);
            }

            var host = launchRequest != null && !string.IsNullOrWhiteSpace(launchRequest.Host)
                ? launchRequest.Host
                : config.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("Effective Gateway host is required.");
            }

            var port = launchRequest != null && launchRequest.Port > 0
                ? launchRequest.Port
                : config.Port;
            if (port <= 0 || port > 65535)
            {
                throw new InvalidOperationException("Effective Gateway port must be between 1 and 65535.");
            }
        }

        private string EffectiveHost()
        {
            return _launchRequest != null && !string.IsNullOrWhiteSpace(_launchRequest.Host)
                ? _launchRequest.Host
                : _config.Host;
        }

        private int EffectivePort()
        {
            return _launchRequest != null && _launchRequest.Port > 0
                ? _launchRequest.Port
                : _config.Port;
        }
    }
}
