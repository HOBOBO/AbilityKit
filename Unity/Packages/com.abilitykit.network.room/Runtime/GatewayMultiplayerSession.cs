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
            uint playerId = 1)
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
            var sessionToken = loginResult.SessionToken;
            string roomId;
            ulong numericRoomId = 0;

            if (joinRoomId != null)
            {
                var joinResult = await flow.JoinRoomAsync(sessionToken, launchSpec.Region, launchSpec.ServerId, joinRoomId, effectiveTimeout, cancellationToken).ConfigureAwait(false);
                if (!joinResult.Success) { sdkClient.Dispose(); throw new InvalidOperationException($"Join room failed: {joinResult.Message}"); }
                roomId = joinResult.RoomId ?? joinRoomId;
                numericRoomId = joinResult.NumericRoomId;
            }
            else
            {
                roomId = await flow.CreateRoomAsync(sessionToken, launchSpec, effectiveTimeout, cancellationToken).ConfigureAwait(false);
                var joinResult = await flow.JoinRoomAsync(sessionToken, launchSpec.Region, launchSpec.ServerId, roomId, effectiveTimeout, cancellationToken).ConfigureAwait(false);
                if (joinResult.Success) numericRoomId = joinResult.NumericRoomId;
            }

            await flow.SetReadyAsync(sessionToken, roomId, ready: true, effectiveTimeout, cancellationToken).ConfigureAwait(false);
            var battleSnapshot = await flow.WaitForBattleStartAsync(sessionToken, roomId, TimeSpan.FromSeconds(1), effectiveTimeout, cancellationToken).ConfigureAwait(false);
            var battleId = battleSnapshot.Snapshot?.BattleId ?? roomId;
            var subResult = await flow.SubscribeStateSyncAsync(sessionToken, battleId, roomId, effectiveTimeout, cancellationToken).ConfigureAwait(false);

            var result = new GatewaySessionResult(sessionToken, roomId, battleId, numericRoomId, playerId, battleSnapshot, subResult.Success);
            if (!result.Started || !result.Subscribed) { sdkClient.Dispose(); throw new InvalidOperationException($"Room flow incomplete. Started={result.Started}, Subscribed={result.Subscribed}"); }
            return new GatewayMultiplayerSession(sdkClient, roomClient, result);
        }

        public void Tick(float deltaTime) { ThrowIfDisposed(); _sdkClient.Tick(deltaTime); }
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
