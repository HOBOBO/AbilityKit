using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Moba.StateSync;
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
            // FIXED (2026-07-20): Use WireRoomGatewayBinary + WireStateSyncSnapshotPush (MemoryPack)
            // instead of MobaWorldSnapshotCodec (BinaryObjectCodec). The server encodes via
            // WireRoomGatewayBinary.Serialize(WireStateSyncSnapshotPush) in StateSyncObserverGrain,
            // so the matching deserializer is WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>.
            // The old MobaWorldSnapshotCodec path used an incompatible BinaryObjectCodec and a
            // different struct shape (5 fields, long Timestamp) — it would silently produce
            // default/empty snapshots instead of throwing, masking the real data.
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
            return ToGatewaySnapshot(in wire);
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

            var commandSequence = unchecked((ulong)Interlocked.Increment(ref _nextBattleInputCommandSequence));
            var req = new WireSubmitBattleInputReq
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
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _sendRequestAsync(_opCodes.SubmitBattleInput, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(respPayload);
            return new GatewayBattleInputResult(
                wire.AcceptedFrame,
                wire.Success,
                wire.CurrentFrame,
                wire.Status,
                wire.Message,
                wire.ShouldResync,
                wire.ServerTicks,
                commandSequence);
        }

        public static GatewayStateSyncSnapshot ToGatewaySnapshot(in WireStateSyncSnapshotPush push)
        {
            var source = push.Actors;
            var actors = source == null || source.Count == 0
                ? Array.Empty<GatewayStateSyncActorSnapshot>()
                : new GatewayStateSyncActorSnapshot[source.Count];

            for (int i = 0; i < actors.Length; i++)
            {
                var actor = source[i];
                actors[i] = new GatewayStateSyncActorSnapshot(
                    actor.ActorId,
                    actor.X,
                    actor.Y,
                    actor.Z,
                    actor.Rotation,
                    actor.VelocityX,
                    actor.VelocityZ,
                    actor.Hp,
                    actor.HpMax,
                    actor.TeamId,
                    actor.Kind,
                    actor.Code,
                    actor.OwnerNetId);
            }

            var removedSource = push.RemovedActorIds;
            var removedActorIds = removedSource == null || removedSource.Count == 0
                ? Array.Empty<int>()
                : removedSource.ToArray();

            return new GatewayStateSyncSnapshot(
                push.WorldId,
                push.Frame,
                push.Timestamp,
                push.IsFullSnapshot,
                actors,
                push.SchemaVersion,
                removedActorIds,
                push.EventWatermark,
                push.EventEpoch);
        }
    }
}
