#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    /// <summary>
    /// Result of a successful <see cref="GatewayMultiplayerSession.CreateAsync"/> call.
    /// </summary>
    public readonly struct GatewaySessionResult
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly string BattleId;
        public readonly ulong NumericRoomId;
        public readonly uint PlayerId;
        public readonly RoomGatewayGetSnapshotResult RoomSnapshot;
        public bool Started => RoomSnapshot.Success;
        public bool Subscribed { get; }
        public GatewaySessionResult(string sessionToken, string roomId, string battleId, ulong numericRoomId, uint playerId, RoomGatewayGetSnapshotResult roomSnapshot, bool subscribed)
        { SessionToken = sessionToken; RoomId = roomId; BattleId = battleId; NumericRoomId = numericRoomId; PlayerId = playerId; RoomSnapshot = roomSnapshot; Subscribed = subscribed; }
    }

    /// <summary>
    /// High-level multiplayer session: connect → guest login → create/join → ready → start → subscribe.
    /// New projects use ~10 lines instead of reimplementing ~200 lines of flow assembly.
    /// </summary>
    public sealed class GatewayMultiplayerSession : IDisposable
    {
        private readonly NetworkSdkClient _sdkClient;
        private readonly RoomGatewayWireSessionClient _roomClient;
        private bool _disposed;
        public NetworkSdkClient SdkClient => _sdkClient;
        public RoomGatewayWireSessionClient RoomClient => _roomClient;
        public GatewaySessionResult Result { get; }

        private GatewayMultiplayerSession(NetworkSdkClient sdkClient, RoomGatewayWireSessionClient roomClient, GatewaySessionResult result)
        { _sdkClient = sdkClient; _roomClient = roomClient; Result = result; }

        public static async Task<GatewayMultiplayerSession> CreateAsync(
            string host, int port, string accountId, RoomGatewayLaunchSpec launchSpec,
            Func<ITransport>? transportFactory = null, IDispatcher? dispatcher = null,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default,
            string? joinRoomId = null, Action<RoomGatewayWireSessionClient>? configureRoomClient = null,
            uint playerId = 1, bool waitForBattleStart = true,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterJoinAndBeforeReady = null,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterReadyAndBeforeBattleStart = null,
            bool subscribeStateSync = true, bool joinFallbackToCreate = false)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("accountId is required.", nameof(accountId));
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            var effectiveTransport = transportFactory ?? (() => new TcpTransport());

            var builder = new NetworkSdkBuilder().UseTransportFactory(effectiveTransport);
            if (dispatcher != null) builder.UseDispatchers(dispatcher);
            var sdkClient = builder.Build();
            sdkClient.Open(host, port);
            await WaitForConnectedAsync(sdkClient, effectiveTimeout, cancellationToken).ConfigureAwait(false);

            var loginReq = new WireRoomGuestLoginReq { GuestId = accountId };
            var loginRespBytes = await sdkClient.SendRawRequestAsync(
                RoomGatewayOpCodes.GuestLogin, WireRoomGatewayBinary.Serialize(in loginReq),
                effectiveTimeout, cancellationToken).ConfigureAwait(false);
            var loginResult = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(loginRespBytes);
            if (!loginResult.Success) { sdkClient.Dispose(); throw new InvalidOperationException($"Guest login failed: {loginResult.Message}"); }

            var roomClient = sdkClient.CreateRoomClient();
            configureRoomClient?.Invoke(roomClient);

            var flow = new RoomGatewaySessionFlow(roomClient);
            try
            {
                var result = await RunRoomFlowAsync(
                    flow, loginResult.SessionToken, launchSpec, joinRoomId, waitForBattleStart, playerId,
                    effectiveTimeout, cancellationToken, afterJoinAndBeforeReady, afterReadyAndBeforeBattleStart,
                    subscribeStateSync, joinFallbackToCreate).ConfigureAwait(false);
                return new GatewayMultiplayerSession(sdkClient, roomClient, result);
            }
            catch
            {
                sdkClient.Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime) { ThrowIfDisposed(); _sdkClient.Tick(deltaTime); }

        /// <summary>
        /// The room-flow orchestration core: create/join → [optional after-join hook: hero-pick/loadout] →
        /// ready → [optional after-ready hook: begin loading + report assets loaded] → [optional battle-start
        /// wait] → subscribe state sync. Public so external callers (and tests) can drive it with any
        /// <see cref="IRoomGatewaySessionClientBase"/>-backed <see cref="RoomGatewaySessionFlow"/>
        /// — the network/login lives in <see cref="CreateAsync"/>; this seam is transport-free and injectable.
        /// Throws on failure (the caller disposes its transport).
        /// </summary>
        public static async Task<GatewaySessionResult> RunRoomFlowAsync(
            RoomGatewaySessionFlow flow,
            string sessionToken,
            RoomGatewayLaunchSpec launchSpec,
            string? joinRoomId,
            bool waitForBattleStart,
            uint playerId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterJoinAndBeforeReady = null,
            Func<RoomGatewaySessionFlow, string, string, TimeSpan?, CancellationToken, Task>? afterReadyAndBeforeBattleStart = null,
            bool subscribeStateSync = true,
            bool joinFallbackToCreate = false)
        {
            string roomId = string.Empty;
            ulong numericRoomId = 0;
            var joined = false;

            if (joinRoomId != null)
            {
                RoomGatewayJoinResult joinResult;
                try
                {
                    joinResult = await flow.JoinRoomAsync(sessionToken, launchSpec.Region, launchSpec.ServerId, joinRoomId, timeout, cancellationToken).ConfigureAwait(false);
                }
                // joinFallbackToCreate covers both failure shapes: a Success=false result and a
                // thrown gateway/wire error (e.g. 409 "Room not initialized" surfaces as an
                // exception from the wire client, not a result). Cancellation still aborts.
                catch (Exception ex) when (joinFallbackToCreate && ex is not OperationCanceledException)
                {
                    joinResult = default;
                }
                if (!joinResult.Success)
                {
                    if (!joinFallbackToCreate) throw new InvalidOperationException($"Join room failed: {joinResult.Message}");
                }
                else
                {
                    roomId = joinResult.RoomId ?? joinRoomId;
                    numericRoomId = joinResult.NumericRoomId;
                    joined = true;
                }
            }

            if (!joined)
            {
                roomId = await flow.CreateRoomAsync(sessionToken, launchSpec, timeout, cancellationToken).ConfigureAwait(false);
                var joinResult = await flow.JoinRoomAsync(sessionToken, launchSpec.Region, launchSpec.ServerId, roomId, timeout, cancellationToken).ConfigureAwait(false);
                if (joinResult.Success) numericRoomId = joinResult.NumericRoomId;
            }

            // Extensibility hook: hero-pick / staged loading for flows that need them (e.g. MOBA).
            if (afterJoinAndBeforeReady != null)
            {
                await afterJoinAndBeforeReady(flow, sessionToken, roomId, timeout, cancellationToken).ConfigureAwait(false);
            }

            await flow.SetReadyAsync(sessionToken, roomId, ready: true, timeout, cancellationToken).ConfigureAwait(false);

            // Loading-stage hook: drives BeginLoading/ReportAssetsLoaded for rooms whose battle only
            // commits after a loading phase (e.g. server-authoritative snapshot worlds).
            if (afterReadyAndBeforeBattleStart != null)
            {
                await afterReadyAndBeforeBattleStart(flow, sessionToken, roomId, timeout, cancellationToken).ConfigureAwait(false);
            }

            RoomGatewayGetSnapshotResult battleSnapshot = default;
            var battleId = roomId;
            if (waitForBattleStart)
            {
                battleSnapshot = await flow.WaitForBattleStartAsync(sessionToken, roomId, TimeSpan.FromSeconds(1), timeout, cancellationToken).ConfigureAwait(false);
                battleId = battleSnapshot.Snapshot?.BattleId ?? roomId;
            }

            // subscribeStateSync=false: the caller's battle data plane will own the subscription
            // (gateway push binding is single-slot last-writer-wins per observer key — a room-side
            // subscribe here would hold the binding until the battle plane takes it over, and any
            // later room-side re-subscribe would steal it back and black out the battle stream).
            var subscribed = false;
            if (subscribeStateSync)
            {
                var subResult = await flow.SubscribeStateSyncAsync(sessionToken, battleId, roomId, timeout, cancellationToken).ConfigureAwait(false);
                subscribed = subResult.Success;
            }

            var result = new GatewaySessionResult(sessionToken, roomId, battleId, numericRoomId, playerId, battleSnapshot, subscribed);
            var started = !waitForBattleStart || result.Started;
            var subscribedOk = !subscribeStateSync || result.Subscribed;
            if (!started || !subscribedOk)
            {
                throw new InvalidOperationException($"Room flow incomplete. Started={result.Started}, Subscribed={result.Subscribed}");
            }
            return result;
        }

        public void Dispose() { if (_disposed) return; _disposed = true; _roomClient.Dispose(); _sdkClient.Dispose(); }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(GatewayMultiplayerSession)); }

        private static async Task WaitForConnectedAsync(NetworkSdkClient client, TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            while (!client.IsConnected) { cts.Token.ThrowIfCancellationRequested(); await Task.Delay(50, cts.Token).ConfigureAwait(false); }
        }
    }
}
