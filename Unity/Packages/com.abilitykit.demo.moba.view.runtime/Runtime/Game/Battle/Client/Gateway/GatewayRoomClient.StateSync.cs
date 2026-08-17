using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient
    {
        public async Task<GatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            string sessionToken,
            string battleId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));

            var result = await _roomSessionClient.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(
                    sessionToken,
                    battleId,
                    roomId ?? string.Empty),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayStateSyncSubscriptionResult(result.Success);
        }

        public GatewayStateSyncSnapshot DeserializeStateSyncSnapshotPush(ArraySegment<byte> payload)
        {
            return _wireClient.DeserializeStateSyncSnapshotPush(payload);
        }

        public bool IsStateSyncSnapshotPush(uint opCode)
        {
            return opCode == _opCodes.SnapshotPushed || opCode == _opCodes.DeltaSnapshotPushed;
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
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));
            if (worldId == 0) throw new ArgumentOutOfRangeException(nameof(worldId));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (playerId == 0) throw new ArgumentOutOfRangeException(nameof(playerId));

            return await _wireClient.SubmitBattleInputAsync(
                sessionToken,
                battleId,
                worldId,
                frame,
                playerId,
                inputOpCode,
                inputPayload,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public static GatewayStateSyncSnapshot ToGatewaySnapshot(in WireStateSyncSnapshotPush push)
        {
            return GatewayRoomResponseMapper.ToGatewaySnapshot(in push);
        }
    }
}
