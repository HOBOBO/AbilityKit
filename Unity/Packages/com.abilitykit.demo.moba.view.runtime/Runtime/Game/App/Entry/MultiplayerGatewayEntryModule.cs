using System;
using System.Diagnostics;
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
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game
{
    public interface IMultiplayerGatewayDiagnostics
    {
        bool IsRemoteActive { get; }
        ConnectionState ConnectionState { get; }
        MultiplayerRecoveryState RecoveryState { get; }
        NetworkSessionRecoveryDecision RecoveryDecision { get; }
        NetworkSessionRecoveryDiagnostics RecoveryDiagnostics { get; }
        SessionLifecycleDiagnosticsSnapshot LifecycleDiagnostics { get; }
    }

    public interface IMultiplayerGatewayRecoveryControl
    {
        void ResetReconnect();
    }

    public interface IMultiplayerGatewayRuntime :
        IMultiplayerGatewayDiagnostics,
        IMultiplayerGatewayRecoveryControl
    {
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
        private readonly SessionLifecycleDiagnosticsRecorder _lifecycleDiagnostics;
        private string _lastCorrelationContext = string.Empty;
        private int _retryCount;

        internal MultiplayerGatewayRecoveryRuntime(
            Func<CancellationToken, Task<MultiplayerRoomRestoreResult>> restoreRoom,
            Func<string> correlationContext = null,
            Action<Exception> failure = null,
            NetworkSessionRecoveryOptions options = null,
            SessionLifecycleDiagnosticsRecorder lifecycleDiagnostics = null)
        {
            _restoreRoom = restoreRoom ?? throw new ArgumentNullException(nameof(restoreRoom));
            _correlationContext = correlationContext ?? (() => string.Empty);
            _failure = failure;
            _lifecycleDiagnostics = lifecycleDiagnostics;
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
            if (signal.Kind == NetworkSessionRecoverySignalKind.ReconnectAttemptStarted)
            {
                _retryCount++;
                _lifecycleDiagnostics?.SetRetryCount(_retryCount);
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
            _retryCount = 0;
            _lifecycleDiagnostics?.SetRetryCount(0);
            SetState(MultiplayerRecoveryState.None);
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
            SetState(execution.Decision.Signal.Kind switch
            {
                NetworkSessionRecoverySignalKind.ReconnectAttemptStarted =>
                    MultiplayerRecoveryState.ReconnectAttempt,
                NetworkSessionRecoverySignalKind.ReconnectScheduled =>
                    MultiplayerRecoveryState.ReconnectScheduled,
                NetworkSessionRecoverySignalKind.ConnectionError =>
                    MultiplayerRecoveryState.ReconnectScheduled,
                _ => State
            });
            return Task.FromResult(true);
        }

        private async Task<bool> ExecuteRebuildSessionAsync(
            NetworkSessionRecoveryExecutionContext execution,
            CancellationToken cancellationToken)
        {
            var signal = execution.Decision.Signal;
            if (signal.Kind == NetworkSessionRecoverySignalKind.ReconnectExhausted)
            {
                SetState(MultiplayerRecoveryState.ReconnectExhausted);
                return false;
            }

            if (signal.Kind != NetworkSessionRecoverySignalKind.ConnectionRestored)
            {
                return false;
            }

            SetState(MultiplayerRecoveryState.RestoringRoom);
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
                        SetState(MultiplayerRecoveryState.RestoringLoadingBarrier);
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
            SetState(state);
            // 完成信号会推进框架代次并取消旧动作，避免 Room Flow 与请求返回顺序不同造成状态覆盖。
            _runtime.CompleteRecovery(
                ResolveCorrelationContext(),
                detail);
        }

        private void SetState(MultiplayerRecoveryState state)
        {
            State = state;
            if (_lifecycleDiagnostics == null ||
                _lifecycleDiagnostics.Snapshot.State == SessionLifecycleDiagnosticState.Stopping)
            {
                return;
            }
            _lifecycleDiagnostics.Transition(state switch
            {
                MultiplayerRecoveryState.None => SessionLifecycleDiagnosticState.Running,
                MultiplayerRecoveryState.Recovered => SessionLifecycleDiagnosticState.Running,
                MultiplayerRecoveryState.ReconnectExhausted => SessionLifecycleDiagnosticState.Faulted,
                _ => SessionLifecycleDiagnosticState.Recovering,
            });
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
        private readonly GatewayPushOperationRuntime _pushOperations =
            new GatewayPushOperationRuntime();
        private readonly MultiplayerGatewayEntryRuntime _entryRuntime =
            new MultiplayerGatewayEntryRuntime();
        private readonly SessionLifecycleDiagnosticsRecorder _lifecycleDiagnostics =
            new SessionLifecycleDiagnosticsRecorder();
        private MultiplayerGatewayEntryResources _resources;

        public bool IsRemoteActive => _resources?.Selection?.IsRemoteSelected == true;
        public ConnectionState ConnectionState =>
            _resources?.SdkClient != null
                ? _resources.SdkClient.State
                : ConnectionState.Disconnected;
        public MultiplayerRecoveryState RecoveryState =>
            _resources?.Recovery?.State ?? MultiplayerRecoveryState.None;
        public NetworkSessionRecoveryDecision RecoveryDecision =>
            _resources?.Recovery?.Decision ?? default;
        public NetworkSessionRecoveryDiagnostics RecoveryDiagnostics =>
            _resources?.Recovery?.Diagnostics ?? default;
        public SessionLifecycleDiagnosticsSnapshot LifecycleDiagnostics =>
            _lifecycleDiagnostics.Snapshot;

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
            var root = ctx.Root;
            _entryRuntime.Attach(attachment =>
            {
                var resources = new MultiplayerGatewayEntryResources();
                _resources = resources;
                _lifecycleDiagnostics.BeginGeneration(
                    _entryRuntime.AttachmentGeneration,
                    SessionLifecycleDiagnosticState.Starting);
                attachment.Register(() =>
                {
                    if (ReferenceEquals(_resources, resources)) _resources = null;
                });

                resources.IoDispatcher =
                    new DedicatedThreadDispatcher("LobbyGatewayNetworkThread");
                attachment.Register(resources.IoDispatcher.Dispose);
                var callbackDispatcher = UnityMainThreadDispatcher.CaptureCurrent();
                var sdkBuilder = new NetworkSdkBuilder()
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
                    .UseDispatchers(callbackDispatcher, resources.IoDispatcher);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                RoomProtocolDecoderModule.Register(NetworkTrafficMonitor.Default.Decoders);
                AbilityKit.Protocol.Moba.MobaProtocolDecoderModule.Register(
                    NetworkTrafficMonitor.Default.Decoders);
                sdkBuilder.ObserveTraffic(NetworkTrafficMonitor.Default, options =>
                {
                    options.ConnectionId = "moba-room-primary";
                    options.Role = "room";
                    options.CatalogId = "abilitykit.room";
                    options.TransportName = "tcp";
                    options.MaximumPayloadPreviewBytes = 65536;
                });
#endif
                resources.SdkClient = sdkBuilder.Build();
                attachment.Register(resources.SdkClient.Dispose);
                resources.Store = new ClientRoomStore();
                resources.Client = new GatewayRoomClient(
                    resources.SdkClient,
                    GatewayRoomOpCodes.Default);
                attachment.Register(resources.Client.Dispose);
#if UNITY_5_3_OR_NEWER
                resources.Session = new GatewayMultiplayerRoomSession(
                    resources.Client,
                    resources.Store,
                    reliableEventCheckpointStore:
                        MobaUnityReliableEventCheckpointStores.CreateBufferedFile(),
                    ownsReliableEventCheckpointStore: true);
#else
                resources.Session = new GatewayMultiplayerRoomSession(
                    resources.Client,
                    resources.Store);
#endif
                attachment.Register(resources.Session.Dispose);
                resources.SnapshotProvider =
                    new ClientRoomSnapshotProvider(resources.Store);
                attachment.Register(resources.SnapshotProvider.Dispose);
                resources.AssetLoader = new MultiplayerBattleAssetLoader(
                    ResourcesBattleAssetLoadService.Default,
                    dependencyProvider: ResourcesBattleAssetDependencyProvider.Default,
                    mainThreadDispatcher: callbackDispatcher);
                resources.Controller = new MultiplayerRoomFlowController(
                    resources.Session,
                    resources.SnapshotProvider,
                    resources.AssetLoader);
                attachment.Register(resources.Controller.Dispose);
                resources.PushSynchronizer = new ClientRoomPushSynchronizer(
                    resources.Client,
                    resources.Store,
                    RefreshCurrentRoomAsync);
                _pushOperations.Attach(
                    (opCode, payload, cancellationToken) =>
                        resources.PushSynchronizer.HandleServerPushAsync(
                            opCode,
                            payload,
                            cancellationToken),
                    exception => Log.Exception(
                        exception,
                        "[MultiplayerGatewayEntryModule] Failed to process Room push."));
                attachment.Register(_pushOperations.Detach);
                resources.Recovery = new MultiplayerGatewayRecoveryRuntime(
                    RestoreRoomAfterReconnectAsync,
                    () => resources.Controller.CurrentRoomId ?? string.Empty,
                    exception => Log.Exception(
                        exception,
                        "[MultiplayerGatewayEntryModule] Room recovery action failed."),
                    lifecycleDiagnostics: _lifecycleDiagnostics);
                attachment.Register(async () =>
                {
                    var pendingRecovery = resources.Recovery.PendingExecution;
                    resources.Recovery.Reset();
                    try
                    {
                        await pendingRecovery.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });

                resources.SdkClient.ServerPushReceived += HandleServerPush;
                resources.SdkClient.Disconnected += HandleDisconnected;
                attachment.Register(() =>
                {
                    resources.SdkClient.ServerPushReceived -= HandleServerPush;
                    resources.SdkClient.Disconnected -= HandleDisconnected;
                });
                resources.RecoveryBinding = resources.SdkClient.BindRecoverySignals(
                    resources.Recovery,
                    new NetworkSdkClientRecoveryBindingOptions
                    {
                        CorrelationContextProvider =
                            () => resources.Controller.CurrentRoomId ?? string.Empty,
                        ReportingFailure = exception => Log.Exception(
                            exception,
                            "[MultiplayerGatewayEntryModule] Failed to report a recovery signal.")
                    });
                attachment.Register(resources.RecoveryBinding.Dispose);
                resources.RecoverySignalsEnabled = true;
                resources.Controller.StateChanged += HandleRoomFlowStateChanged;
                attachment.Register(() =>
                    resources.Controller.StateChanged -= HandleRoomFlowStateChanged);
                root.TryGetRef(out resources.Selection);
                if (resources.Selection != null)
                {
                    resources.Selection.Changed += HandleEntrySelectionChanged;
                    attachment.Register(() =>
                        resources.Selection.Changed -= HandleEntrySelectionChanged);
                }

                resources.RootServices = new MultiplayerGatewayRootServices(
                    _config,
                    _launchRequest,
                    resources.Store,
                    resources.Client,
                    resources.Client,
                    resources.Client,
                    resources.Session,
                    resources.Session,
                    resources.SnapshotProvider,
                    resources.Controller,
                    resources.PushSynchronizer,
                    resources.AssetLoader,
                    this,
                    this);
                resources.RootServices.Publish(root);
                attachment.Register(resources.RootServices.Withdraw);
                attachment.Register(() =>
                    SetRecoverySignalsEnabled(enabled: false, reset: true));
                ApplyEntrySelection();
                _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Running);
            });
        }

        public void Tick(in GameEntryModuleContext ctx, float deltaTime)
        {
            _resources?.SdkClient?.Tick(deltaTime);
        }

        public void OnDetach(in GameEntryModuleContext ctx)
        {
            _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Stopping);
            var diagnosticGeneration =
                _lifecycleDiagnostics.BeginPendingOperation("gateway-entry-detach");
            var stopwatch = Stopwatch.StartNew();
            var teardown = _entryRuntime.Detach();
            _ = ObserveTeardownAsync(teardown, diagnosticGeneration, stopwatch);
        }

        private async Task ObserveTeardownAsync(
            Task teardown,
            int diagnosticGeneration,
            Stopwatch stopwatch)
        {
            Exception failure = null;
            try
            {
                await teardown.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
                Log.Exception(
                    exception,
                    "[MultiplayerGatewayEntryModule] Gateway teardown failed.");
            }
            finally
            {
                stopwatch.Stop();
                _lifecycleDiagnostics.CompletePendingOperation(
                    diagnosticGeneration,
                    stopwatch.Elapsed,
                    failure,
                    failure == null
                        ? SessionLifecycleDiagnosticState.Stopped
                        : SessionLifecycleDiagnosticState.Faulted);
            }
        }

        public void ResetReconnect()
        {
            var resources = _resources;
            resources?.Recovery?.Reset();
            if (resources?.RecoveryBinding != null)
            {
                resources.RecoveryBinding.Enabled = true;
                resources.RecoverySignalsEnabled = true;
            }
            resources?.SdkClient?.ResetReconnect();
        }

        private void HandleDisconnected()
        {
            // 断线仅提交当前位置，不删除检查点；后续重连仍需从该位置恢复。
            try
            {
                _resources?.Session?.FlushReliableEventCheckpointsAsync(
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
            _resources?.Recovery?.ObserveRoomFlowState(state);
        }

        private async Task<MultiplayerRoomRestoreResult> RestoreRoomAfterReconnectAsync(
            CancellationToken cancellationToken)
        {
            var resources = _resources;
            var controller = resources?.Controller;
            var spec = controller?.CurrentLaunchSpec;
            var attachmentGeneration = _entryRuntime.AttachmentGeneration;
            var lifetime = _entryRuntime.LifetimeToken;
            if (resources == null ||
                controller == null ||
                spec == null ||
                !_entryRuntime.IsCurrent(attachmentGeneration))
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
                lifetime);
            var fallbackPlayerId = _config.RestoreFallbackPlayerId == 0u
                ? 1u
                : _config.RestoreFallbackPlayerId;
            var result = await controller.RestoreAsync(
                spec,
                fallbackPlayerId,
                linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!_entryRuntime.IsCurrent(attachmentGeneration) ||
                !ReferenceEquals(_resources, resources))
            {
                throw new OperationCanceledException(linked.Token);
            }

            return result;
        }

        private void HandleEntrySelectionChanged()
        {
            ApplyEntrySelection();
        }

        private void ApplyEntrySelection()
        {
            var resources = _resources;
            if (resources?.SdkClient == null)
            {
                return;
            }

            if (IsRemoteActive)
            {
                SetRecoverySignalsEnabled(enabled: true, reset: false);
                if (resources.SdkClient.State == ConnectionState.Disconnected)
                {
                    resources.SdkClient.Open(EffectiveHost(), EffectivePort());
                }

                return;
            }

            SetRecoverySignalsEnabled(enabled: false, reset: true);
            resources.Controller?.Cancel();
            resources.Store?.Reset();
            if (resources.SdkClient.State != ConnectionState.Disconnected)
            {
                resources.SdkClient.Close();
            }
        }

        private void SetRecoverySignalsEnabled(bool enabled, bool reset)
        {
            var resources = _resources;
            var binding = resources?.RecoveryBinding;
            if (binding == null) return;

            if (reset)
            {
                binding.Reset();
                resources.Recovery?.Reset();
            }

            if (resources.RecoverySignalsEnabled == enabled) return;
            binding.Enabled = enabled;
            resources.RecoverySignalsEnabled = enabled;
        }

        private void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            _pushOperations.TryStart(opCode, payload);
        }

        private Task RefreshCurrentRoomAsync(CancellationToken cancellationToken)
        {
            var resources = _resources;
            var roomId = resources?.Store?.Current?.RoomId;
            if (string.IsNullOrWhiteSpace(roomId) || resources?.Session == null)
            {
                throw new InvalidOperationException("Cannot refresh Room snapshot before joining a room.");
            }

            return resources.Session.RefreshSnapshotAsync(roomId, cancellationToken);
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

        private sealed class MultiplayerGatewayEntryResources
        {
            public NetworkSdkClient SdkClient;
            public DedicatedThreadDispatcher IoDispatcher;
            public ClientRoomStore Store;
            public GatewayRoomClient Client;
            public GatewayMultiplayerRoomSession Session;
            public ClientRoomSnapshotProvider SnapshotProvider;
            public MultiplayerRoomFlowController Controller;
            public MultiplayerBattleAssetLoader AssetLoader;
            public ClientRoomPushSynchronizer PushSynchronizer;
            public LobbyBattleEntrySelection Selection;
            public MultiplayerGatewayRecoveryRuntime Recovery;
            public NetworkSdkClientRecoveryBinding RecoveryBinding;
            public MultiplayerGatewayRootServices RootServices;
            public bool RecoverySignalsEnabled;
        }
    }
}
