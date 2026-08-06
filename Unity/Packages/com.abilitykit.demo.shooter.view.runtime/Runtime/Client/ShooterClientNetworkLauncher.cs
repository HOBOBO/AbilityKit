#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientNetworkLauncher : IDisposable
    {
        private readonly IConnection _connection;
        private readonly NetworkSdkClient _sdkClient;
        private readonly ShooterRoomGatewayConnection _gatewayConnection;
        private bool _disposed;

        public ShooterClientNetworkLauncher(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _sdkClient = new NetworkSdkBuilder()
                .UseConnectionFactory(() => _connection)
                .Build();
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

        public bool IsConnected => _connection.IsConnected;

        public void Open(ShooterClientNetworkEndpoint endpoint)
        {
            Open(endpoint.Host, endpoint.Port);
        }

        public void Open(string host, int port)
        {
            ThrowIfDisposed();
            _sdkClient.Open(host, port);
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _sdkClient.Close();
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            _sdkClient.Tick(deltaTime);
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

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection);
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

            _gatewayConnection.AttachBattle(launched.Battle);
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

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection);
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

            _gatewayConnection.AttachBattle(launched.Battle);
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

            var launcher = new ShooterClientGatewayLauncher(_gatewayConnection);
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

            _gatewayConnection.AttachBattle(launched.Battle);
            return new ShooterClientNetworkLaunchResult(_connection, _gatewayConnection, launched);
        }

        private void OpenIfNeeded(string host, int port)
        {
            if (!_sdkClient.IsConnected)
            {
                _sdkClient.Open(host, port);
            }
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
            _gatewayConnection.Dispose();
            _sdkClient.Dispose();
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
