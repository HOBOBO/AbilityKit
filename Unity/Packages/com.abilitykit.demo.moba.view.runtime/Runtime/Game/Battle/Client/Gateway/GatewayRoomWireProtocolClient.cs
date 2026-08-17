using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Moba.GatewayTimeSync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle.Agent
{
    internal sealed class GatewayRoomWireProtocolClient
    {
        private readonly IRoomGatewayRequestTransport _transport;
        private readonly GatewayRoomOpCodes _opCodes;
        private readonly BattleInputCommandSequence _battleInputSequence;

        public GatewayRoomWireProtocolClient(
            IRoomGatewayRequestTransport transport,
            GatewayRoomOpCodes opCodes,
            BattleInputCommandSequence battleInputSequence)
        {
            _transport = transport
                ?? throw new ArgumentNullException(nameof(transport));
            _opCodes = opCodes;
            _battleInputSequence = battleInputSequence
                ?? throw new ArgumentNullException(nameof(battleInputSequence));
        }

        public async Task<GatewayTimeSyncResult> TimeSyncAsync(
            uint timeSyncOpCode,
            long clientSendTicks,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var request = new WireTimeSyncReq(clientSendTicks);
            var payload = WireTimeSyncBinary.Serialize(in request);
            var response = await _transport.SendRequestAsync(
                timeSyncOpCode,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wire = WireTimeSyncBinary.DeserializeTimeSyncRes(response);
            return new GatewayTimeSyncResult(
                wire.ClientSendTicks,
                wire.ServerNowTicks,
                wire.ServerTickFrequency);
        }

        public async Task<string> GuestLoginAsync(
            uint guestLoginOpCode,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var request = new WireRoomGuestLoginReq
            {
                GuestId = Guid.NewGuid().ToString("N")
            };
            var payload = WireRoomGatewayBinary.Serialize(in request);
            var response = await _transport.SendRequestAsync(
                guestLoginOpCode,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(
                response);
            return wire.Success
                ? wire.SessionToken ?? string.Empty
                : string.Empty;
        }

        public async Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var payload = DemoRoomGatewayDirectoryCodec.SerializeQuery(in query);
            var response = await _transport.SendRequestAsync(
                RoomGatewayOpCodes.ListRooms,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return DemoRoomGatewayDirectoryCodec.DeserializeResult(response);
        }

        public ClientRoomSnapshot DeserializeRoomStateChangedPush(
            ArraySegment<byte> payload)
        {
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomStateChangedPush>(
                payload);
            return GatewayRoomResponseMapper.ToClientSnapshot(wire.Snapshot);
        }

        public GatewayStateSyncSnapshot DeserializeStateSyncSnapshotPush(
            ArraySegment<byte> payload)
        {
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(
                payload);
            return GatewayRoomResponseMapper.ToGatewaySnapshot(in wire);
        }

        public async Task<GatewayBattleInputResult> SubmitBattleInputAsync(
            string sessionToken,
            string battleId,
            ulong worldId,
            int frame,
            uint playerId,
            int inputOpCode,
            byte[] inputPayload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var commandSequence = _battleInputSequence.Next();
            var request = new WireSubmitBattleInputReq
            {
                SessionToken = sessionToken,
                BattleId = battleId,
                WorldId = worldId,
                Frame = frame,
                PlayerId = playerId,
                InputOpCode = inputOpCode,
                Payload = inputPayload ?? Array.Empty<byte>(),
                CommandSequence = commandSequence
            };
            var payload = WireRoomGatewayBinary.Serialize(in request);
            var response = await _transport.SendRequestAsync(
                _opCodes.SubmitBattleInput,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(
                response);
            return GatewayRoomResponseMapper.ToBattleInputResult(
                in wire,
                commandSequence);
        }
    }
}
