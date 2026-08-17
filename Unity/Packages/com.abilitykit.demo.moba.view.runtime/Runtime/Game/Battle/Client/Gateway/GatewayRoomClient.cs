using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient :
        IGatewayRoomClient,
        IDemoRoomDirectoryClient,
        IRoomGatewayRequestTransport,
        IRoomGatewayPushSource,
        IDisposable
    {
        private readonly GatewayRoomTransportAdapter _transport;
        private readonly GatewayRoomWireProtocolClient _wireClient;
        private readonly GatewayRoomOpCodes _opCodes;
        private readonly RoomGatewayWireSessionClient _roomSessionClient;
        private bool _disposed;

        public GatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
            : this(new GatewayRoomTransportAdapter(connection), opCodes)
        {
        }

        public GatewayRoomClient(NetworkSdkClient sdkClient, GatewayRoomOpCodes opCodes)
            : this(new GatewayRoomTransportAdapter(sdkClient), opCodes)
        {
        }

        internal GatewayRoomClient(
            GatewayRoomTransportAdapter transport,
            GatewayRoomOpCodes opCodes)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _opCodes = opCodes;
            _wireClient = new GatewayRoomWireProtocolClient(
                transport,
                opCodes,
                new BattleInputCommandSequence());
            _roomSessionClient = new RoomGatewayWireSessionClient(
                transport,
                transport,
                ToWireOpCodes(in opCodes));
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add => _transport.ServerPushReceived += value;
            remove => _transport.ServerPushReceived -= value;
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _transport.SendRequestAsync(
                opCode,
                payload,
                timeout,
                cancellationToken);
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return SendRawRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public Task<GatewayTimeSyncResult> TimeSyncAsync(
            uint timeSyncOpCode,
            long clientSendTicks,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _wireClient.TimeSyncAsync(
                timeSyncOpCode,
                clientSendTicks,
                timeout,
                cancellationToken);
        }

        public Task<string> GuestLoginAsync(
            uint guestLoginOpCode,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _wireClient.GuestLoginAsync(
                guestLoginOpCode,
                timeout,
                cancellationToken);
        }

        public Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _wireClient.ListRoomsAsync(
                query,
                timeout,
                cancellationToken);
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
            _transport.Dispose();
        }
    }

}
