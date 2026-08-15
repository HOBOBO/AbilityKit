using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Moba.GatewayTimeSync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient :
        IGatewayRoomClient,
        IDemoRoomDirectoryClient,
        IRoomGatewayRequestTransport,
        IRoomGatewayPushSource,
        IDisposable
    {
        private readonly Func<uint, ArraySegment<byte>, TimeSpan?, CancellationToken,
            Task<ArraySegment<byte>>> _sendRequestAsync;
        private readonly Action<Action<uint, ArraySegment<byte>>> _subscribeServerPush;
        private readonly Action<Action<uint, ArraySegment<byte>>> _unsubscribeServerPush;
        private readonly IDisposable _ownedRequestClient;
        private readonly GatewayRoomOpCodes _opCodes;
        private readonly RoomGatewayWireSessionClient _roomSessionClient;
        private long _nextBattleInputCommandSequence;
        private bool _disposed;

        public GatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            var requestClient = new RequestClient(connection);
            _sendRequestAsync = requestClient.SendRequestAsync;
            _subscribeServerPush = handler => connection.ServerPushReceived += handler;
            _unsubscribeServerPush = handler => connection.ServerPushReceived -= handler;
            _ownedRequestClient = requestClient;
            _opCodes = opCodes;
            _roomSessionClient = new RoomGatewayWireSessionClient(
                this,
                this,
                ToWireOpCodes(in opCodes));
        }

        public GatewayRoomClient(NetworkSdkClient sdkClient, GatewayRoomOpCodes opCodes)
        {
            if (sdkClient == null) throw new ArgumentNullException(nameof(sdkClient));

            _sendRequestAsync = sdkClient.SendRawRequestAsync;
            _subscribeServerPush = handler => sdkClient.ServerPushReceived += handler;
            _unsubscribeServerPush = handler => sdkClient.ServerPushReceived -= handler;
            _ownedRequestClient = null;
            _opCodes = opCodes;
            _roomSessionClient = new RoomGatewayWireSessionClient(
                this,
                this,
                ToWireOpCodes(in opCodes));
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add => _subscribeServerPush(value);
            remove => _unsubscribeServerPush(value);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _sendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return SendRawRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public async Task<GatewayTimeSyncResult> TimeSyncAsync(
            uint timeSyncOpCode,
            long clientSendTicks,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var req = new WireTimeSyncReq(clientSendTicks);
            var payload = WireTimeSyncBinary.Serialize(in req);
            var resp = await _sendRequestAsync(timeSyncOpCode, payload, timeout, cancellationToken);
            var wire = WireTimeSyncBinary.DeserializeTimeSyncRes(resp);
            return new GatewayTimeSyncResult(wire.ClientSendTicks, wire.ServerNowTicks, wire.ServerTickFrequency);
        }

        public async Task<string> GuestLoginAsync(
            uint guestLoginOpCode,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var req = new WireRoomGuestLoginReq
            {
                GuestId = Guid.NewGuid().ToString("N")
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var resp = await _sendRequestAsync(guestLoginOpCode, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(resp);
            return wire.Success ? wire.SessionToken ?? string.Empty : string.Empty;
        }

        public async Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var payload = DemoRoomGatewayDirectoryCodec.SerializeQuery(in query);
            var resp = await _sendRequestAsync(
                RoomGatewayOpCodes.ListRooms,
                payload,
                timeout,
                cancellationToken);
            return DemoRoomGatewayDirectoryCodec.DeserializeResult(resp);
        }

        private static RoomGatewayWireOpCodes ToWireOpCodes(in GatewayRoomOpCodes opCodes)
        {
            return new RoomGatewayWireOpCodes(
                opCodes.CreateRoom,
                opCodes.JoinRoom,
                opCodes.LeaveRoom,
                opCodes.SetReady,
                opCodes.StartBattle,
                opCodes.SubscribeStateSync,
                opCodes.RestoreRoom,
                opCodes.PickHero,
                opCodes.BeginLoading,
                opCodes.ReportLoadingProgress,
                opCodes.ReportAssetsLoaded,
                opCodes.CancelLoading,
                opCodes.GetSnapshot,
                opCodes.RoomStateChanged);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _roomSessionClient.Dispose();
            _ownedRequestClient?.Dispose();
        }
    }
}
