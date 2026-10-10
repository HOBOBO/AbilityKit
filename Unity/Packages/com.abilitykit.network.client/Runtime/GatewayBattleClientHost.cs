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
    public sealed class GatewayBattleClientEnterOptions
    {
        public GatewayRoomConnectionOptions RoomConnection { get; set; } = new GatewayRoomConnectionOptions();
        public GatewayRoomFlowOptions RoomFlow { get; set; } = new GatewayRoomFlowOptions();
        public GatewayBattleAttachOptions Battle { get; set; } = new GatewayBattleAttachOptions();
    }

    public sealed class GatewayRoomConnectionOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public RoomGatewayLaunchSpec LaunchSpec { get; set; }
        public Func<ITransport>? TransportFactory { get; set; }
        public IDispatcher? Dispatcher { get; set; }
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    public sealed class GatewayRoomFlowOptions
    {
        public string? JoinRoomId { get; set; }
        public Action<RoomGatewayWireSessionClient>? ConfigureClient { get; set; }
        public uint PlayerId { get; set; } = 1;
        public bool WaitForBattleStart { get; set; } = true;
        public Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? AfterJoinAndBeforeReady { get; set; }
        public Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? AfterReadyAndBeforeBattleStart { get; set; }
        public bool JoinFallbackToCreate { get; set; }
    }

    public sealed class GatewayBattleAttachOptions
    {
        public bool Attach { get; set; } = true;
        public Action<NetworkBattleConfig, GatewaySessionResult>? Configure { get; set; }
        public Func<ITransport>? TransportFactory { get; set; }
        public IDispatcher? Dispatcher { get; set; }
        public IDispatcher? IoDispatcher { get; set; }
    }

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
        private readonly IDispatcher? _battleIoDispatcher;
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
            IDispatcher? battleDispatcher = null,
            IDispatcher? battleIoDispatcher = null)
            : this(
                roomSession?.SdkClient ?? throw new ArgumentNullException(nameof(roomSession)),
                roomSession,
                roomSession.Result,
                host,
                port,
                battleTransportFactory,
                battleDispatcher,
                battleIoDispatcher)
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
            IDispatcher? battleDispatcher = null,
            IDispatcher? battleIoDispatcher = null)
            : this(roomConnection, null, session, host, port, battleTransportFactory, battleDispatcher, battleIoDispatcher)
        {
        }

        private GatewayBattleClientHost(
            NetworkSdkClient roomConnection,
            GatewayMultiplayerSession? roomSession,
            in GatewaySessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory,
            IDispatcher? battleDispatcher,
            IDispatcher? battleIoDispatcher)
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
            _battleIoDispatcher = battleIoDispatcher ?? battleDispatcher;
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
            bool joinFallbackToCreate = false,
            IDispatcher? battleIoDispatcher = null)
        {
            return await EnterAsync(new GatewayBattleClientEnterOptions
            {
                RoomConnection = new GatewayRoomConnectionOptions
                {
                    Host = host, Port = port, AccountId = accountId, LaunchSpec = launchSpec,
                    TransportFactory = roomTransportFactory, Dispatcher = roomDispatcher,
                    Timeout = timeout, CancellationToken = cancellationToken
                },
                RoomFlow = new GatewayRoomFlowOptions
                {
                    JoinRoomId = joinRoomId, ConfigureClient = configureRoomClient, PlayerId = playerId,
                    WaitForBattleStart = waitForBattleStart,
                    AfterJoinAndBeforeReady = afterJoinAndBeforeReady,
                    AfterReadyAndBeforeBattleStart = afterReadyAndBeforeBattleStart,
                    JoinFallbackToCreate = joinFallbackToCreate
                },
                Battle = new GatewayBattleAttachOptions
                {
                    Attach = attachBattle, Configure = configureBattle,
                    TransportFactory = battleTransportFactory, Dispatcher = battleDispatcher,
                    IoDispatcher = battleIoDispatcher
                }
            }).ConfigureAwait(false);
        }

        public static async Task<GatewayBattleClientHost> EnterAsync(GatewayBattleClientEnterOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var room = options.RoomConnection ?? throw new ArgumentException("Room connection options are required.", nameof(options));
            var flow = options.RoomFlow ?? throw new ArgumentException("Room flow options are required.", nameof(options));
            var battle = options.Battle ?? throw new ArgumentException("Battle options are required.", nameof(options));
            if (battle.Attach && battle.Configure == null)
                throw new ArgumentException("Battle configuration is required when attach is enabled.", nameof(options));

            var session = await GatewayMultiplayerSession.CreateAsync(
                room.Host, room.Port, room.AccountId, room.LaunchSpec,
                room.TransportFactory, room.Dispatcher, room.Timeout, room.CancellationToken,
                flow.JoinRoomId, flow.ConfigureClient, flow.PlayerId, flow.WaitForBattleStart,
                flow.AfterJoinAndBeforeReady, flow.AfterReadyAndBeforeBattleStart,
                subscribeStateSync: !battle.Attach,
                joinFallbackToCreate: flow.JoinFallbackToCreate).ConfigureAwait(false);

            var host2 = new GatewayBattleClientHost(
                session, room.Host, room.Port, battle.TransportFactory, battle.Dispatcher, battle.IoDispatcher);
            try
            {
                if (battle.Attach)
                {
                    host2.AttachBattle(battle.Configure!);
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
            _battle = CreateBattleTransport(options, _battleDispatcher, _battleIoDispatcher, connect);
            return _battle;
        }

        /// <summary>
        /// Creates a battle plane for a staged flow. The caller owns the returned transport and
        /// may connect it after registering push consumers.
        /// </summary>
        public static NetworkTransport CreateBattleTransport(
            NetworkTransportOptions options,
            IDispatcher? callbackDispatcher = null,
            IDispatcher? ioDispatcher = null,
            bool connect = true)
        {
            var callbacks = callbackDispatcher ?? InlineDispatcher.Instance;
            var transport = new NetworkTransport(options, callbacks, ioDispatcher ?? callbacks);
            if (!connect) return transport;
            try
            {
                transport.Connect();
                return transport;
            }
            catch
            {
                transport.Dispose();
                throw;
            }
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
