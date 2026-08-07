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
        private bool _connectedOnce;
        private bool _restoreAfterReconnect;

        public bool IsRemoteActive => _selection?.IsRemoteSelected == true;
        public ConnectionState ConnectionState =>
            _sdkClient != null ? _sdkClient.State : ConnectionState.Disconnected;
        public MultiplayerRecoveryState RecoveryState { get; private set; }

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
            _session = new GatewayMultiplayerRoomSession(_client, _store);
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

            _sdkClient.ServerPushReceived += HandleServerPush;
            _sdkClient.Connected += HandleConnected;
            _sdkClient.Disconnected += HandleDisconnected;
            _sdkClient.ReconnectScheduled += HandleReconnectScheduled;
            _sdkClient.ReconnectAttemptStarted += HandleReconnectAttemptStarted;
            _sdkClient.ReconnectExhausted += HandleReconnectExhausted;
            _controller.StateChanged += HandleRoomFlowStateChanged;
            ctx.Root.TryGetRef(out _selection);
            if (_selection != null)
            {
                _selection.Changed += HandleEntrySelectionChanged;
            }

            ctx.Root.WithRef(_config);
            if (_launchRequest != null)
            {
                ctx.Root.WithRef(_launchRequest);
            }
            ctx.Root.WithRef(_store);
            ctx.Root.WithRef<IGatewayRoomClient>(_client);
            ctx.Root.WithRef<IMultiplayerRoomSession>(_session);
            ctx.Root.WithRef(_session);
            ctx.Root.WithRef<IRoomSnapshotProvider>(_snapshotProvider);
            ctx.Root.WithRef(_controller);
            ctx.Root.WithRef(_pushSynchronizer);
            ctx.Root.WithRef<IBattleAssetLeaseTransferSource>(_assetLoader);
            ctx.Root.WithRef<IMultiplayerGatewayRuntime>(this);
            ApplyEntrySelection();
        }

        public void Tick(in GameEntryModuleContext ctx, float deltaTime)
        {
            _sdkClient?.Tick(deltaTime);
        }

        public void OnDetach(in GameEntryModuleContext ctx)
        {
            _lifetime?.Cancel();
            if (_selection != null)
            {
                _selection.Changed -= HandleEntrySelectionChanged;
            }

            if (_sdkClient != null)
            {
                _sdkClient.ServerPushReceived -= HandleServerPush;
                _sdkClient.Connected -= HandleConnected;
                _sdkClient.Disconnected -= HandleDisconnected;
                _sdkClient.ReconnectScheduled -= HandleReconnectScheduled;
                _sdkClient.ReconnectAttemptStarted -= HandleReconnectAttemptStarted;
                _sdkClient.ReconnectExhausted -= HandleReconnectExhausted;
            }

            if (_controller != null)
            {
                _controller.StateChanged -= HandleRoomFlowStateChanged;
            }

            if (ctx.Root.IsValid)
            {
                ctx.Root.RemoveComponent(typeof(IMultiplayerGatewayRuntime));
                ctx.Root.RemoveComponent(typeof(IBattleAssetLeaseTransferSource));
                ctx.Root.RemoveComponent(typeof(ClientRoomPushSynchronizer));
                ctx.Root.RemoveComponent(typeof(MultiplayerRoomFlowController));
                ctx.Root.RemoveComponent(typeof(IRoomSnapshotProvider));
                ctx.Root.RemoveComponent(typeof(GatewayMultiplayerRoomSession));
                ctx.Root.RemoveComponent(typeof(IMultiplayerRoomSession));
                ctx.Root.RemoveComponent(typeof(IGatewayRoomClient));
                ctx.Root.RemoveComponent(typeof(ClientRoomStore));
                ctx.Root.RemoveComponent(typeof(BattleGatewayConfigSO));
                ctx.Root.RemoveComponent(typeof(DemoMultiplayerLaunchRequest));
            }

            _controller?.Dispose();
            _snapshotProvider?.Dispose();
            _session?.Dispose();
            _client?.Dispose();
            _sdkClient?.Dispose();
            _ioDispatcher?.Dispose();
            _lifetime?.Dispose();

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
            _connectedOnce = false;
            _restoreAfterReconnect = false;
            RecoveryState = MultiplayerRecoveryState.None;
        }

        public void ResetReconnect()
        {
            RecoveryState = MultiplayerRecoveryState.None;
            _sdkClient?.ResetReconnect();
        }

        private void HandleConnected()
        {
            if (!_connectedOnce)
            {
                _connectedOnce = true;
                RecoveryState = MultiplayerRecoveryState.None;
                return;
            }

            if (_restoreAfterReconnect)
            {
                _restoreAfterReconnect = false;
                RestoreRoomAfterReconnectAsync();
            }
        }

        private void HandleDisconnected()
        {
            if (_connectedOnce && !string.IsNullOrWhiteSpace(_controller?.CurrentRoomId))
            {
                _restoreAfterReconnect = true;
            }
        }

        private void HandleReconnectScheduled(int attemptNumber, float delaySeconds)
        {
            if (_connectedOnce && !string.IsNullOrWhiteSpace(_controller?.CurrentRoomId))
            {
                _restoreAfterReconnect = true;
            }

            RecoveryState = MultiplayerRecoveryState.ReconnectScheduled;
        }

        private void HandleReconnectAttemptStarted(int attemptNumber)
        {
            RecoveryState = MultiplayerRecoveryState.ReconnectAttempt;
        }

        private void HandleReconnectExhausted(int attempts)
        {
            RecoveryState = MultiplayerRecoveryState.ReconnectExhausted;
        }

        private void HandleRoomFlowStateChanged(MultiplayerRoomFlowState state)
        {
            if (RecoveryState != MultiplayerRecoveryState.RestoringRoom &&
                RecoveryState != MultiplayerRecoveryState.RestoringLoadingBarrier &&
                RecoveryState != MultiplayerRecoveryState.RestoringBattleSnapshot)
            {
                return;
            }

            if (state == MultiplayerRoomFlowState.InLobby ||
                state == MultiplayerRoomFlowState.InBattle)
            {
                RecoveryState = MultiplayerRecoveryState.Recovered;
            }
        }

        private async void RestoreRoomAfterReconnectAsync()
        {
            var controller = _controller;
            var spec = controller?.CurrentLaunchSpec;
            var lifetime = _lifetime;
            if (controller == null || spec == null || lifetime == null) return;

            try
            {
                RecoveryState = MultiplayerRecoveryState.RestoringRoom;
                var fallbackPlayerId = _config.RestoreFallbackPlayerId == 0u
                    ? 1u
                    : _config.RestoreFallbackPlayerId;
                var result = await controller.RestoreAsync(
                    spec,
                    fallbackPlayerId,
                    lifetime.Token);
                RecoveryState = !result.HasActiveRoom
                    ? MultiplayerRecoveryState.None
                    : result.Phase switch
                {
                    MultiplayerRoomPhase.Loading => MultiplayerRecoveryState.RestoringLoadingBarrier,
                    MultiplayerRoomPhase.Starting => MultiplayerRecoveryState.RestoringLoadingBarrier,
                    MultiplayerRoomPhase.InBattle => MultiplayerRecoveryState.Recovered,
                    _ => MultiplayerRecoveryState.Recovered
                };
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (RecoveryState == MultiplayerRecoveryState.RestoringRoom)
                {
                    RecoveryState = MultiplayerRecoveryState.None;
                }

                Log.Exception(ex, "[MultiplayerGatewayEntryModule] Room restore after reconnect failed.");
            }
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
                if (_sdkClient.State == ConnectionState.Disconnected)
                {
                    _sdkClient.Open(EffectiveHost(), EffectivePort());
                }

                return;
            }

            _controller?.Cancel();
            _store?.Reset();
            if (_sdkClient.State != ConnectionState.Disconnected)
            {
                _sdkClient.Close();
            }
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
