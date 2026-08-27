#nullable enable

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Client;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientNetworkLauncher : IDisposable
    {
        private readonly IConnection _connection;
        private readonly NetworkSdkClientKey _sdkClientKey;
        private readonly NetworkSdkClientLease _sdkClientLease;
        private readonly NetworkSdkClient _sdkClient;
        private readonly ShooterRoomGatewayConnection _gatewayConnection;
        private GatewayBattleClientHost? _battleHost;
        private NetworkTransport? _battleTransport;
        private ShooterBattleDataPlane? _battleData;
        private ShooterClientSession? _battleSession;
        private NetworkSdkClientRecoveryBinding? _sessionRecoveryBinding;
        private bool _disposed;

        public ShooterClientNetworkLauncher(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _sdkClientKey = new NetworkSdkClientKey(
                "abilitykit.shooter",
                "room",
                Guid.NewGuid().ToString("N"));
            _sdkClientLease = NetworkSdkClientHub.Default.Acquire(
                _sdkClientKey,
                new NetworkSdkBuilder()
                    .UseOwnedConnectionFactory(() => _connection));
            _sdkClient = _sdkClientLease.Client;
            _gatewayConnection = new ShooterRoomGatewayConnection(_sdkClient);
        }

        public static ShooterClientNetworkLauncher Create(IShooterClientConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            return new ShooterClientNetworkLauncher(connectionFactory.CreateConnection());
        }

        public IConnection Connection => _connection;

        public ShooterRoomGatewayConnection GatewayConnection => _gatewayConnection;

        /// <summary>Battle data-plane push/reconnect surface, fed by the battle <see cref="NetworkTransport"/>.</summary>
        public ShooterBattleDataPlane? BattleData => _battleData;

        public bool IsConnected => _sdkClient.IsConnected;

        public void Open(ShooterClientNetworkEndpoint endpoint)
        {
            Open(endpoint.Host, endpoint.Port);
        }

        public void Open(string host, int port)
        {
            ThrowIfDisposed();
            if (_sessionRecoveryBinding != null) _sessionRecoveryBinding.Enabled = true;
            _sdkClient.Open(host, port);
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_sessionRecoveryBinding != null) _sessionRecoveryBinding.Enabled = false;
                FlushReliableEventCheckpointsAsync(
                    ReliableEventCheckpointFlushTrigger.Disconnect).GetAwaiter().GetResult();
            }
            finally
            {
                _sdkClient.Close();
            }
        }

        /// <summary>等待当前战斗会话的可靠事件检查点完成持久化。</summary>
        public Task FlushReliableEventCheckpointsAsync(CancellationToken cancellationToken = default)
        {
            return _battleSession?.FlushReliableEventCheckpointsAsync(cancellationToken)
                ?? Task.CompletedTask;
        }

        /// <summary>按指定生命周期原因等待当前检查点完成持久化。</summary>
        public Task<ReliableEventCheckpointFlushResult> FlushReliableEventCheckpointsAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            return _battleSession?.FlushReliableEventCheckpointsAsync(trigger, cancellationToken)
                ?? Task.FromResult(new ReliableEventCheckpointFlushResult(
                    0,
                    trigger,
                    ReliableEventCheckpointFlushStatus.Skipped,
                    TimeSpan.Zero,
                    null));
        }

        /// <summary>获取当前战斗会话的检查点生命周期诊断。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics ReliableEventCheckpointLifecycleDiagnostics =>
            _battleSession?.ReliableEventCheckpointLifecycleDiagnostics ?? default;

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            // Once the battle host exists it pumps both connections (room + battle); before that,
            // pump the room connection directly. Then run queued battle callbacks (push apply) on
            // this (main) thread — single-threaded with session.Tick, so ApplyGatewayPush can't race it.
            if (_battleHost != null)
            {
                _battleHost.Tick(deltaTime);
            }
            else
            {
                _sdkClient.Tick(deltaTime);
            }
            _battleData?.Drain();
        }

        public Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return CreateReadyStartAndSubscribeAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RestoreRoomAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RestoreRoomAsync(
                endpoint,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RestoreRoomAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RestoreRoomAsync(
                host,
                port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public async Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await RestoreRoomAsync(
                host,
                port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            OpenIfNeeded(host, port);

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection, flow => BuildBattleTransport(host, port, flow));
            var launched = await launcher.RestoreRoomAsync(
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);

            AttachBattleSessionRecovery(launched.Session);
            _battleData?.AttachBattle(launched.Battle);
            _gatewayConnection.AttachBattle(launched.Battle);
            _battleTransport?.Connect();
            return new ShooterClientNetworkRestoreResult(_connection, _gatewayConnection, launched);
        }

        public Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return CreateReadyStartAndSubscribeAsync(
                endpoint,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return CreateReadyStartAndSubscribeAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return CreateReadyStartAndSubscribeAsync(
                host,
                port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public async Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await CreateReadyStartAndSubscribeAsync(
                host,
                port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            OpenIfNeeded(host, port);

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection, flow => BuildBattleTransport(host, port, flow));
            var launched = await launcher.CreateReadyStartAndSubscribeAsync(
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);

            AttachBattleSessionRecovery(launched.Session);
            _battleData?.AttachBattle(launched.Battle);
            _gatewayConnection.AttachBattle(launched.Battle);
            _battleTransport?.Connect();
            return new ShooterClientNetworkLaunchResult(_connection, _gatewayConnection, launched);
        }

        public Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return JoinReadyStartAndSubscribeAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return JoinReadyStartAndSubscribeAsync(
                endpoint,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            ShooterClientNetworkEndpoint endpoint,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return JoinReadyStartAndSubscribeAsync(
                endpoint.Host,
                endpoint.Port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken);
        }

        public Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return JoinReadyStartAndSubscribeAsync(
                host,
                port,
                runtime,
                ShooterPresentationSessionContext.CreateFromFacade(presentation),
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                tickRate,
                timeout,
                cancellationToken);
        }

        public async Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await JoinReadyStartAndSubscribeAsync(
                host,
                port,
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                ShooterClientSyncAssemblyOptions.Default,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            string host,
            int port,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterStartGamePayload startGame,
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            ShooterClientSyncAssemblyOptions syncAssemblyOptions,
            int tickRate = ShooterGameplay.DefaultTickRate,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            OpenIfNeeded(host, port);

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection, flow => BuildBattleTransport(host, port, flow));
            var launched = await launcher.JoinReadyStartAndSubscribeAsync(
                runtime,
                presentationSession,
                startGame,
                sessionToken,
                roomId,
                launchSpec,
                playerId,
                syncAssemblyOptions,
                tickRate,
                timeout,
                cancellationToken).ConfigureAwait(false);

            AttachBattleSessionRecovery(launched.Session);
            _battleData?.AttachBattle(launched.Battle);
            _gatewayConnection.AttachBattle(launched.Battle);
            _battleTransport?.Connect();
            return new ShooterClientNetworkLaunchResult(_connection, _gatewayConnection, launched);
        }

        /// <summary>
        /// Builds the battle data-plane on its OWN connection (two-connection / MOBA topology) via the
        /// shared <see cref="GatewayBattleClientHost"/> (primitives path: shooter's room control plane is
        /// its own staged stack, not the facade). The host assembles gateway/session/protocol-preset;
        /// the configure callback adds the shooter-specific input preset, reliable-event cursor, and
        /// raw-downlink mode. Connect is deferred so the <see cref="ShooterBattleDataPlane"/> can
        /// subscribe before the engine's connect handshake emits its first pushes.
        /// Called from the gateway launcher's battle-client factory once the room flow yields the battle ids.
        /// </summary>
        private IShooterRoomGatewayClient BuildBattleTransport(string host, int port, ShooterRoomGatewayFlowResult flow)
        {
            var session = new GatewaySessionResult(
                flow.SessionToken,
                flow.RoomId,
                flow.BattleId,
                flow.NumericRoomId,
                flow.PlayerId,
                roomSnapshot: default,
                subscribed: false);

            _battleHost = new GatewayBattleClientHost(
                _sdkClient,
                in session,
                host,
                port,
                battleTransportFactory: () => new TcpTransport(),
                battleDispatcher: InlineDispatcher.Instance);

            _battleTransport = _battleHost.AttachBattle((config, s) =>
            {
                config
                    // Standard room-gateway input uplink preset. Engine retry disabled (no retry policy):
                    // shooter keeps its own RejectedTooFarFuture retry as the sole retry. ShouldResync is
                    // carried as a pure data field for the client's MarkGatewayInputResyncRequested path.
                    .UseRoomGatewayStateSyncInput(
                        s.BattleId,
                        playerIdToUInt: pid => uint.Parse(pid.Value, CultureInfo.InvariantCulture),
                        worldIdToUlong: w => ulong.Parse(w.Value, CultureInfo.InvariantCulture))
                    .WithReliableEventCursor(
                        () => _battleSession?.ReliableEventEpoch ?? string.Empty,
                        () => _battleSession?.LastReliableEventAck ?? 0L)
                    // Shooter is a raw downlink consumer: all pushes route through RawServerPushReceived
                    // into ShooterClientSession.ApplyGatewayPush; typed deserializers stay off.
                    .WithRawDownlinkOnly();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ShooterProtocolDecoderModule.Register(NetworkTrafficMonitor.Default.Decoders);
                config.ObserveTraffic(NetworkTrafficMonitor.Default, options =>
                {
                    options.ConnectionId = "shooter-battle-primary";
                    options.Role = "battle";
                    options.CatalogId = "abilitykit.shooter.battle";
                    options.TransportName = "tcp";
                    options.MaximumPayloadPreviewBytes = 65536;
                    options.FilterFactory = NetworkTrafficMonitor.Default.CreateSamplingFilter;
                });
#endif
            }, connect: false);

            // InlineDispatcher callback: PacketReceived (incl. RequestClient response matching) fires
            // immediately on the receive thread, so an awaited SendInputAsync can't deadlock on the main
            // thread. ApplyGatewayPush is queued in ShooterBattleDataPlane and drained on the main thread
            // during Tick, so it can't race session.Tick.
            _battleData = new ShooterBattleDataPlane(_battleTransport);
            _battleData.SnapshotPushDispatched += (op, payload, result) => _gatewayConnection.NotifyBattlePushDispatched(op, payload, result);
            return new ShooterBattleTransportGatewayClient(
                _battleTransport,
                playerIdFromUInt: u => new PlayerId(u.ToString(CultureInfo.InvariantCulture)),
                worldIdFromUlong: u => new WorldId(u.ToString(CultureInfo.InvariantCulture)));
        }

        private void OpenIfNeeded(string host, int port)
        {
            if (_sessionRecoveryBinding != null) _sessionRecoveryBinding.Enabled = true;
            _sdkClient.OpenIfDisconnected(host, port);
        }

        private void AttachBattleSessionRecovery(ShooterClientSession session)
        {
            _sessionRecoveryBinding?.Dispose();
            _battleSession = session ?? throw new ArgumentNullException(nameof(session));
            _sessionRecoveryBinding = _sdkClient.BindRecoverySignals(
                session,
                new NetworkSdkClientRecoveryBindingOptions
                {
                    FrameProvider = () => session.CurrentFrame,
                    CorrelationContextProvider = () => session.ReliableEventEpoch
                });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ShooterClientNetworkLauncher));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                FlushReliableEventCheckpointsAsync(
                    ReliableEventCheckpointFlushTrigger.Dispose).GetAwaiter().GetResult();
            }
            finally
            {
                _sessionRecoveryBinding?.Dispose();
                _sessionRecoveryBinding = null;
                _battleData?.Dispose();
                // The host disposes the battle transport and the room SDK client; if no battle was ever
                // attached, the room SDK client is still ours to release. Remove the released Hub entry
                // afterwards so diagnostics never retain a disposed sample client.
                try
                {
                    if (_battleHost != null)
                    {
                        _battleHost.Dispose();
                    }
                    else
                    {
                        _sdkClient.Dispose();
                    }
                    _gatewayConnection.Dispose();
                }
                finally
                {
                    _sdkClientLease.Dispose();
                    NetworkSdkClientHub.Default.Remove(_sdkClientKey);
                }
            }
        }
    }

    public sealed class ShooterClientNetworkRestoreResult : ShooterClientNetworkLaunchResult
    {
        public ShooterClientNetworkRestoreResult(
            IConnection connection,
            ShooterRoomGatewayConnection gatewayConnection,
            ShooterClientGatewayRestoreResult gatewayRestore)
            : base(connection, gatewayConnection, gatewayRestore)
        {
        }

        public ShooterClientGatewayRestoreResult GatewayRestore => (ShooterClientGatewayRestoreResult)GatewayLaunch;
    }

    public class ShooterClientNetworkLaunchResult
    {
        public ShooterClientNetworkLaunchResult(
            IConnection connection,
            ShooterRoomGatewayConnection gatewayConnection,
            ShooterClientGatewayLaunchResult gatewayLaunch)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            GatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
            GatewayLaunch = gatewayLaunch ?? throw new ArgumentNullException(nameof(gatewayLaunch));
        }

        public IConnection Connection { get; }

        public ShooterRoomGatewayConnection GatewayConnection { get; }

        public ShooterClientGatewayLaunchResult GatewayLaunch { get; }

        public ShooterClientSession Session => GatewayLaunch.Session;

        public ShooterClientBattleHandle Battle => GatewayLaunch.Battle;

        public ShooterRoomGatewayFlowResult Flow => GatewayLaunch.Flow;
    }
}
