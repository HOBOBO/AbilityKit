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
    public sealed class MultiplayerClientSessionOptions
    {
        public MultiplayerRoomConnectionOptions RoomConnection { get; set; } = new MultiplayerRoomConnectionOptions();
        public MultiplayerRoomFlowOptions RoomFlow { get; set; } = new MultiplayerRoomFlowOptions();
        public MultiplayerBattleConnectionOptions Battle { get; set; } = new MultiplayerBattleConnectionOptions();
    }

    public sealed class MultiplayerRoomConnectionOptions
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

    public sealed class MultiplayerRoomFlowOptions
    {
        public string? JoinRoomId { get; set; }
        public Action<RoomGatewayWireSessionClient>? ConfigureClient { get; set; }
        public uint PlayerId { get; set; } = 1;
        public bool WaitForBattleStart { get; set; } = true;
        public Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? AfterJoinAndBeforeReady { get; set; }
        public Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? AfterReadyAndBeforeBattleStart { get; set; }
        public bool JoinFallbackToCreate { get; set; }
    }

    public sealed class MultiplayerBattleConnectionOptions
    {
        public bool Attach { get; set; } = true;
        public Action<NetworkBattleConfig, MultiplayerRoomSessionResult>? Configure { get; set; }
        public Func<ITransport>? TransportFactory { get; set; }
        public IDispatcher? Dispatcher { get; set; }
        public IDispatcher? IoDispatcher { get; set; }
    }

    /// <summary>
    /// Multiplayer client session. Owns the room control-plane connection
    /// (a <see cref="MultiplayerRoomSession"/>: connect → login → create/join → ready →
    /// [loading] → [battle-start wait]) plus the battle data-plane connection (a
    /// <see cref="NetworkTransport"/> on its own transport via <see cref="NetworkBattleConfig"/>),
    /// with the push-binding discipline built in: when a battle plane is attached, the room-side
    /// state-sync subscribe is skipped entirely (the room push binding is single-slot
    /// last-writer-wins per observer key, so the battle connection's RenewSession→SubscribeStateSync
    /// handshake must be the only subscriber — otherwise pushes route to the wrong connection).
    /// <para>
    /// The client is one-shot: a reconnect means building a new client (or driving
    /// <c>Room.RoomClient</c>/staged restore yourself). Dispose releases battle first, then room.
    /// No LeaveRoom is sent on dispose — the server reclaims the room via connection-drop detection.
    /// </para>
    /// </summary>
    public sealed class MultiplayerClientSession : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly Func<ITransport>? _battleTransportFactory;
        private readonly IDispatcher? _battleDispatcher;
        private readonly IDispatcher? _battleIoDispatcher;
        private readonly MultiplayerRoomSession? _roomSession;
        private readonly NetworkSdkClient _roomConnection;
        private readonly MultiplayerRoomSessionResult _session;
        private NetworkTransport? _battle;
        private bool _disposed;

        /// <summary>The room session; non-null when the client was built from one.</summary>
        public MultiplayerRoomSession? RoomSession => _roomSession;

        /// <summary>The room control-plane connection (always available, both construction paths).</summary>
        public NetworkSdkClient RoomConnection => _roomConnection;

        /// <summary>Room-flow identity (token/roomId/battleId/numericRoomId) the battle plane is bound to.</summary>
        public MultiplayerRoomSessionResult Session => _session;

        /// <summary>The battle data plane; null until <see cref="AttachBattle"/>.</summary>
        public NetworkTransport? Battle => _battle;

        /// <summary>
        /// Wraps an already-established room session. Most callers use <see cref="EnterAsync"/>.
        /// </summary>
        public MultiplayerClientSession(
            MultiplayerRoomSession roomSession,
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
        /// Staged path for custom room flows or injected connections. The client takes over Tick/Dispose
        /// of the room connection and
        /// assembles the battle plane from the supplied session identity.
        /// </summary>
        public MultiplayerClientSession(
            NetworkSdkClient roomConnection,
            in MultiplayerRoomSessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory = null,
            IDispatcher? battleDispatcher = null,
            IDispatcher? battleIoDispatcher = null)
            : this(roomConnection, null, session, host, port, battleTransportFactory, battleDispatcher, battleIoDispatcher)
        {
        }

        private MultiplayerClientSession(
            NetworkSdkClient roomConnection,
            MultiplayerRoomSession? roomSession,
            in MultiplayerRoomSessionResult session,
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
        /// <see cref="MultiplayerRoomSession.CreateAsync"/>) + battle data-plane attach.
        /// With <paramref name="attachBattle"/>=true the room-side state-sync subscribe is skipped
        /// (the battle plane owns it), so <c>Room.Result.Subscribed</c> is false by design.
        /// </summary>
        public static async Task<MultiplayerClientSession> EnterAsync(
            string host,
            int port,
            string accountId,
            RoomGatewayLaunchSpec launchSpec,
            Action<NetworkBattleConfig, MultiplayerRoomSessionResult>? configureBattle,
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
            return await EnterAsync(new MultiplayerClientSessionOptions
            {
                RoomConnection = new MultiplayerRoomConnectionOptions
                {
                    Host = host, Port = port, AccountId = accountId, LaunchSpec = launchSpec,
                    TransportFactory = roomTransportFactory, Dispatcher = roomDispatcher,
                    Timeout = timeout, CancellationToken = cancellationToken
                },
                RoomFlow = new MultiplayerRoomFlowOptions
                {
                    JoinRoomId = joinRoomId, ConfigureClient = configureRoomClient, PlayerId = playerId,
                    WaitForBattleStart = waitForBattleStart,
                    AfterJoinAndBeforeReady = afterJoinAndBeforeReady,
                    AfterReadyAndBeforeBattleStart = afterReadyAndBeforeBattleStart,
                    JoinFallbackToCreate = joinFallbackToCreate
                },
                Battle = new MultiplayerBattleConnectionOptions
                {
                    Attach = attachBattle, Configure = configureBattle,
                    TransportFactory = battleTransportFactory, Dispatcher = battleDispatcher,
                    IoDispatcher = battleIoDispatcher
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 完成房间流程并按选项挂接战斗连接；在交付会话前取消或挂接失败时释放已取得的资源。
        /// </summary>
        public static async Task<MultiplayerClientSession> EnterAsync(MultiplayerClientSessionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var room = options.RoomConnection ?? throw new ArgumentException("Room connection options are required.", nameof(options));
            var flow = options.RoomFlow ?? throw new ArgumentException("Room flow options are required.", nameof(options));
            var battle = options.Battle ?? throw new ArgumentException("Battle options are required.", nameof(options));
            var attachBattle = battle.Attach;
            var configureBattle = battle.Configure;
            if (attachBattle && configureBattle == null)
                throw new ArgumentException("Battle configuration is required when attach is enabled.", nameof(options));

            var host = room.Host;
            var port = room.Port;
            var cancellationToken = room.CancellationToken;
            var session = await MultiplayerRoomSession.CreateAsync(
                host, port, room.AccountId, room.LaunchSpec,
                room.TransportFactory, room.Dispatcher, room.Timeout, cancellationToken,
                flow.JoinRoomId, flow.ConfigureClient, flow.PlayerId, flow.WaitForBattleStart,
                flow.AfterJoinAndBeforeReady, flow.AfterReadyAndBeforeBattleStart,
                subscribeStateSync: !attachBattle,
                joinFallbackToCreate: flow.JoinFallbackToCreate).ConfigureAwait(false);

            MultiplayerClientSession? client = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                client = new MultiplayerClientSession(
                    session, host, port, battle.TransportFactory, battle.Dispatcher, battle.IoDispatcher);
                if (attachBattle)
                {
                    client.AttachBattle(configureBattle!);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return client;
            }
            catch (Exception entryError)
            {
                DisposeAfterFailure((IDisposable?)client ?? session, entryError);
                throw;
            }
        }

        /// <summary>
        /// 预填网关、会话身份和协议配置，再由回调补全配置；回调无需调用 Build。
        /// 成功后战斗连接由会话持有；本次挂接失败时保留房间连接，调用方可重试或释放会话。
        /// 将 <paramref name="connect"/> 设为 false 可在订阅推送后再连接。
        /// </summary>
        public NetworkTransport AttachBattle(Action<NetworkBattleConfig, MultiplayerRoomSessionResult> configureBattle, bool connect = true)
        {
            ThrowIfDisposed();
            if (_battle != null) throw new InvalidOperationException("Battle data plane is already attached.");
            if (configureBattle == null) throw new ArgumentNullException(nameof(configureBattle));

            var options = BuildBattleOptions(_session, _host, _port, _battleTransportFactory, configureBattle);
            _battle = CreateBattleTransport(options, _battleDispatcher, _battleIoDispatcher, connect);
            return _battle;
        }

        /// <summary>
        /// 为分步流程创建战斗连接，成功返回后由调用方持有。
        /// 若立即连接失败，本方法负责清理已创建的战斗连接。
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
            catch (Exception connectError)
            {
                DisposeAfterFailure(transport, connectError);
                throw;
            }
        }

        /// <summary>
        /// The options-assembly step of <see cref="AttachBattle"/>, separated for testability:
        /// pre-fills gateway/session/protocol-preset, then applies the game callback.
        /// </summary>
        public static NetworkTransportOptions BuildBattleOptions(
            in MultiplayerRoomSessionResult session,
            string host,
            int port,
            Func<ITransport>? battleTransportFactory,
            Action<NetworkBattleConfig, MultiplayerRoomSessionResult> configureBattle)
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

        /// <summary>
        /// 先尝试释放战斗连接，再尝试释放房间连接；重复调用不再释放。
        /// 释放不发送退房请求；会话不支持与其他操作并发调用。
        /// </summary>
        /// <exception cref="AggregateException">战斗和房间连接均释放失败，保留两项异常。</exception>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var battle = _battle;
            _battle = null;
            var room = (IDisposable?)_roomSession ?? _roomConnection;
            try
            {
                battle?.Dispose();
            }
            catch (Exception battleError)
            {
                DisposeAfterFailure(room, battleError);
                throw;
            }
            room.Dispose();
        }

        private static void DisposeAfterFailure(IDisposable resource, Exception originalError)
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(originalError, cleanupError);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MultiplayerClientSession));
        }
    }
}
