#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Client
{
    /// <summary>
    /// Two-connection multiplayer battle client host. Owns the room control-plane connection
    /// (a <see cref="GatewayMultiplayerSession"/>: connect → login → create/join → ready →
    /// [loading] → [battle-start wait]) plus the battle data-plane connection (a
    /// <see cref="NetworkTransport"/> on its own transport via <see cref="NetworkBattleConfig"/>),
    /// with the push-binding discipline built in: when a battle plane is attached, the room-side
    /// state-sync subscribe is skipped entirely (the gateway push binding is single-slot
    /// last-writer-wins per observer key, so the battle connection's RenewSession→SubscribeStateSync
    /// handshake must be the only subscriber — otherwise pushes route to the wrong connection).
    /// <para>
    /// The host is one-shot like the facade: a reconnect means building a new host (or driving
    /// <c>Room.RoomClient</c>/staged restore yourself). Dispose releases battle first, then room.
    /// No LeaveRoom is sent on dispose — the server reclaims the room via connection-drop detection.
    /// </para>
    /// </summary>
    public sealed class GatewayBattleClientHost : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly Func<ITransport>? _battleTransportFactory;
        private readonly IDispatcher? _battleDispatcher;
        private readonly GatewayMultiplayerSession? _roomSession;
        private readonly NetworkSdkClient _roomConnection;
        private readonly GatewaySessionResult _session;
        private NetworkTransport? _battle;
        private bool _disposed;

        /// <summary>The facade room session; non-null only when the host was built from one.</summary>
        public GatewayMultiplayerSession? RoomSession => _roomSession;

        /// <summary>The room control-plane connection (always available, both construction paths).</summary>
        public NetworkSdkClient RoomConnection => _roomConnection;

        /// <summary>Room-flow identity (token/roomId/battleId/numericRoomId) the battle plane is bound to.</summary>
        public GatewaySessionResult Session => _session;

        /// <summary>The battle data plane; null until <see cref="AttachBattle"/>.</summary>
        public NetworkTransport? Battle => _battle;

        /// <summary>
        /// Wraps an already-established facade room session. Most callers want <see cref="EnterAsync"/>.
        /// </summary>
        public GatewayBattleClientHost(
            GatewayMultiplayerSession roomSession,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory = null,
            IDispatcher? battleDispatcher = null)
            : this(
                roomSession?.SdkClient ?? throw new ArgumentNullException(nameof(roomSession)),
                roomSession,
                roomSession.Result,
                host,
                port,
                battleTransportFactory,
                battleDispatcher)
        {
        }

        /// <summary>
        /// Primitives path: for consumers whose room control plane is NOT the facade (custom staged
        /// flows, injected connections). The host takes over Tick/Dispose of the room connection and
        /// assembles the battle plane from the supplied session identity.
        /// </summary>
        public GatewayBattleClientHost(
            NetworkSdkClient roomConnection,
            in GatewaySessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory = null,
            IDispatcher? battleDispatcher = null)
            : this(roomConnection, null, session, host, port, battleTransportFactory, battleDispatcher)
        {
        }

        private GatewayBattleClientHost(
            NetworkSdkClient roomConnection,
            GatewayMultiplayerSession? roomSession,
            in GatewaySessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory,
            IDispatcher? battleDispatcher)
        {
            _roomConnection = roomConnection ?? throw new ArgumentNullException(nameof(roomConnection));
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            _roomSession = roomSession;
            _session = session;
            _host = host;
            _port = port;
            _battleTransportFactory = battleTransportFactory;
            _battleDispatcher = battleDispatcher;
        }

        /// <summary>
        /// Full entry: room connection + guest login + room flow (via
        /// <see cref="GatewayMultiplayerSession.CreateAsync"/>) + battle data-plane attach.
        /// With <paramref name="attachBattle"/>=true the room-side state-sync subscribe is skipped
        /// (the battle plane owns it), so <c>Room.Result.Subscribed</c> is false by design.
        /// </summary>
        public static async Task<GatewayBattleClientHost> EnterAsync(
            string host,
            int port,
            string accountId,
            RoomGatewayLaunchSpec launchSpec,
            Action<NetworkBattleConfig, GatewaySessionResult>? configureBattle,
            bool attachBattle = true,
            Func<ITransport>? battleTransportFactory = null,
            IDispatcher? battleDispatcher = null,
            Func<ITransport>? roomTransportFactory = null,
            IDispatcher? roomDispatcher = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default,
            string? joinRoomId = null,
            Action<RoomGatewayWireSessionClient>? configureRoomClient = null,
            uint playerId = 1,
            bool waitForBattleStart = true,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterJoinAndBeforeReady = null,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterReadyAndBeforeBattleStart = null,
            bool joinFallbackToCreate = false)
        {
            if (attachBattle && configureBattle == null)
            {
                throw new ArgumentNullException(nameof(configureBattle), "configureBattle is required when attachBattle is true.");
            }

            var session = await GatewayMultiplayerSession.CreateAsync(
                host, port, accountId, launchSpec,
                roomTransportFactory, roomDispatcher, timeout, cancellationToken,
                joinRoomId, configureRoomClient, playerId, waitForBattleStart,
                afterJoinAndBeforeReady, afterReadyAndBeforeBattleStart,
                subscribeStateSync: !attachBattle,
                joinFallbackToCreate: joinFallbackToCreate).ConfigureAwait(false);

            var host2 = new GatewayBattleClientHost(session, host, port, battleTransportFactory, battleDispatcher);
            try
            {
                if (attachBattle)
                {
                    host2.AttachBattle(configureBattle!);
                }
                return host2;
            }
            catch
            {
                host2.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Builds the battle options: gateway address + session identity + the standard room-gateway
        /// protocol preset are pre-filled; <paramref name="configureBattle"/> receives the config and
        /// the room-flow result (battleId/roomId/numericRoomId are usually needed by the game
        /// callbacks). Do not call <c>Build()</c> inside the callback — the host builds and connects
        /// the transport. Pass <paramref name="connect"/>=false to defer <c>Connect()</c> — needed when
        /// push consumers must subscribe before the engine's connect handshake emits its first pushes.
        /// </summary>
        public NetworkTransport AttachBattle(Action<NetworkBattleConfig, GatewaySessionResult> configureBattle, bool connect = true)
        {
            ThrowIfDisposed();
            if (_battle != null) throw new InvalidOperationException("Battle data plane is already attached.");
            if (configureBattle == null) throw new ArgumentNullException(nameof(configureBattle));

            var options = BuildBattleOptions(_session, _host, _port, _battleTransportFactory, configureBattle);
            _battle = new NetworkTransport(options, _battleDispatcher);
            if (connect)
            {
                _battle.Connect();
            }
            return _battle;
        }

        /// <summary>
        /// The options-assembly step of <see cref="AttachBattle"/>, separated for testability:
        /// pre-fills gateway/session/protocol-preset, then applies the game callback.
        /// </summary>
        public static NetworkTransportOptions BuildBattleOptions(
            in GatewaySessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory,
            Action<NetworkBattleConfig, GatewaySessionResult> configureBattle)
        {
            if (configureBattle == null) throw new ArgumentNullException(nameof(configureBattle));

            var config = new NetworkBattleConfig().WithGateway(host, port);
            if (battleTransportFactory != null)
            {
                config.WithTransportFactory(battleTransportFactory);
            }
            else
            {
                config.WithTcpTransport();
            }

            config
                .WithSession(session.SessionToken, session.BattleId, session.RoomId)
                .UseRoomGatewayProtocol(session.BattleId, session.RoomId);

            configureBattle.Invoke(config, session);
            return config.Build();
        }

        /// <summary>
        /// Pumps both connections. Feed REAL wall-clock elapsed time — heartbeat/reconnect liveness
        /// timers accumulate the supplied delta (fast-forwarding hosts must not pass game-time deltas).
        /// </summary>
        public void Tick(float realDeltaTime)
        {
            ThrowIfDisposed();
            if (_roomSession != null)
            {
                _roomSession.Tick(realDeltaTime);
            }
            else
            {
                _roomConnection.Tick(realDeltaTime);
            }
            _battle?.Tick(realDeltaTime);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _battle?.Dispose();
            _battle = null;
            if (_roomSession != null)
            {
                _roomSession.Dispose();
            }
            else
            {
                _roomConnection.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GatewayBattleClientHost));
        }
    }
}
