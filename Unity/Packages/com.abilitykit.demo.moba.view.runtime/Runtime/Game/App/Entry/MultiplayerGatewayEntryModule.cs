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
        private ConnectionManager _connection;
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
            _connection != null ? _connection.State : ConnectionState.Disconnected;
        public MultiplayerRecoveryState RecoveryState { get; private set; }

        public MultiplayerGatewayEntryModule(BattleGatewayConfigSO config)
        {
            _config = config;
        }

        public string Id => "game.entry.multiplayer-gateway";

        public void OnAttach(in GameEntryModuleContext ctx)
        {
            if (_config == null)
            {
                return;
            }

            ValidateConfig(_config);

            _lifetime = new CancellationTokenSource();
            _ioDispatcher = new DedicatedThreadDispatcher("LobbyGatewayNetworkThread");
            var callbackDispatcher = UnityMainThreadDispatcher.CaptureCurrent();
            var options = new ConnectionOptions
            {
                FrameCodec = LengthPrefixedFrameCodec.Instance,
                KickPushOpCode = 9000,
                EnableReconnect = true,
                ReconnectInitialDelay = TimeSpan.FromSeconds(1),
                ReconnectMaxDelay = TimeSpan.FromSeconds(15),
                ReconnectBackoffMultiplier = 2d,
                ReconnectMaxAttempts = AbilityKit.Network.Runtime.Sync.ReconnectBackoffPolicy.MaxAttempts
            };

            _connection = new ConnectionManager(
                () => new TcpTransport(),
                options,
                callbackDispatcher,
                _ioDispatcher);
            _store = new ClientRoomStore();
            _client = new GatewayRoomClient(
                _connection,
                new GatewayRoomOpCodes(_config.CreateRoomOpCode, _config.JoinRoomOpCode));
            _session = new GatewayMultiplayerRoomSession(_client, _store);
            _snapshotProvider = new ClientRoomSnapshotProvider(_store);
            _assetLoader = new MultiplayerBattleAssetLoader(ResourcesBattleAssetLoadService.Default);
            _controller = new MultiplayerRoomFlowController(_session, _snapshotProvider, _assetLoader);
            _pushSynchronizer = new ClientRoomPushSynchronizer(
                _client,
                _store,
                RefreshCurrentRoomAsync);

            _connection.ServerPushReceived += HandleServerPush;
            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.ReconnectScheduled += HandleReconnectScheduled;
            _connection.ReconnectAttemptStarted += HandleReconnectAttemptStarted;
            _connection.ReconnectExhausted += HandleReconnectExhausted;
            _controller.StateChanged += HandleRoomFlowStateChanged;
            ctx.Root.TryGetRef(out _selection);
            if (_selection != null)
            {
                _selection.Changed += HandleEntrySelectionChanged;
            }

            ctx.Root.WithRef(_config);
            ctx.Root.WithRef(_store);
            ctx.Root.WithRef<IGatewayRoomClient>(_client);
            ctx.Root.WithRef<IMultiplayerRoomSession>(_session);
            ctx.Root.WithRef(_session);
            ctx.Root.WithRef<IRoomSnapshotProvider>(_snapshotProvider);
            ctx.Root.WithRef(_controller);
            ctx.Root.WithRef<IMultiplayerGatewayRuntime>(this);
            ApplyEntrySelection();
        }

        public void Tick(in GameEntryModuleContext ctx, float deltaTime)
        {
            _connection?.Tick(deltaTime);
        }

        public void OnDetach(in GameEntryModuleContext ctx)
        {
            _lifetime?.Cancel();
            if (_selection != null)
            {
                _selection.Changed -= HandleEntrySelectionChanged;
            }

            if (_connection != null)
            {
                _connection.ServerPushReceived -= HandleServerPush;
                _connection.Connected -= HandleConnected;
                _connection.Disconnected -= HandleDisconnected;
                _connection.ReconnectScheduled -= HandleReconnectScheduled;
                _connection.ReconnectAttemptStarted -= HandleReconnectAttemptStarted;
                _connection.ReconnectExhausted -= HandleReconnectExhausted;
            }

            if (_controller != null)
            {
                _controller.StateChanged -= HandleRoomFlowStateChanged;
            }

            if (ctx.Root.IsValid)
            {
                ctx.Root.RemoveComponent(typeof(IMultiplayerGatewayRuntime));
                ctx.Root.RemoveComponent(typeof(MultiplayerRoomFlowController));
                ctx.Root.RemoveComponent(typeof(IRoomSnapshotProvider));
                ctx.Root.RemoveComponent(typeof(GatewayMultiplayerRoomSession));
                ctx.Root.RemoveComponent(typeof(IMultiplayerRoomSession));
                ctx.Root.RemoveComponent(typeof(IGatewayRoomClient));
                ctx.Root.RemoveComponent(typeof(ClientRoomStore));
                ctx.Root.RemoveComponent(typeof(BattleGatewayConfigSO));
            }

            _controller?.Dispose();
            _snapshotProvider?.Dispose();
            _connection?.Dispose();
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
            _connection = null;
            _ioDispatcher = null;
            _lifetime = null;
            _connectedOnce = false;
            _restoreAfterReconnect = false;
            RecoveryState = MultiplayerRecoveryState.None;
        }

        public void ResetReconnect()
        {
            RecoveryState = MultiplayerRecoveryState.None;
            _connection?.ResetReconnect();
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
            if (_connection == null)
            {
                return;
            }

            if (IsRemoteActive)
            {
                if (_connection.State == ConnectionState.Disconnected)
                {
                    _connection.Open(_config.Host, _config.Port);
                }

                return;
            }

            _controller?.Cancel();
            _store?.Reset();
            if (_connection.State != ConnectionState.Disconnected)
            {
                _connection.Close();
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

        private static void ValidateConfig(BattleGatewayConfigSO config)
        {
            if (string.IsNullOrWhiteSpace(config.Host))
            {
                throw new InvalidOperationException("Lobby Gateway Host is required.");
            }

            if (config.Port <= 0 || config.Port > 65535)
            {
                throw new InvalidOperationException("Lobby Gateway Port must be between 1 and 65535.");
            }
        }
    }
}
