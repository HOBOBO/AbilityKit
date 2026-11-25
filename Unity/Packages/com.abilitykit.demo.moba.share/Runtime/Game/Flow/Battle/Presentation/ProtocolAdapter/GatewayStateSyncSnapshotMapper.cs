using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GatewayStateSyncSnapshotMapper
    {
        public static GatewayStateSyncSnapshot Map(in WireStateSyncSnapshotPush push)
            => MapCore(in push, null);

        /// <summary>
        /// 将订阅身份匹配的证明转为类型化值；其他会话的证明不影响正常权威状态导入。
        /// </summary>
        public static GatewayStateSyncSnapshot MapScoped(in WireStateSyncSnapshotPush push,
            in WireStateSyncMovementPayload scoped, System.Guid inputGeneration, ulong worldId, uint playerId)
        {
            var baseline = push;
            baseline.SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion;
            baseline.Payload = scoped.FixedBaselinePayload;
            var proof = scoped.Proof;
            GatewayStateSyncMovementProof? movementProof = null;
            if (inputGeneration != System.Guid.Empty && proof.InputGeneration == inputGeneration &&
                worldId != 0 && proof.WorldId == worldId && push.WorldId == worldId &&
                playerId != 0 && proof.PlayerId == playerId)
            {
                movementProof = new GatewayStateSyncMovementProof(proof.InputGeneration, proof.PlayerId, proof.WorldId,
                    proof.StreamGeneration, proof.SnapshotId, proof.BaseSnapshotId, proof.Tick, proof.TerminalThrough,
                    proof.ActorId, proof.EntityVersion, proof.X, proof.Y, proof.Z,
                    proof.RotationX, proof.RotationY, proof.RotationZ, proof.RotationW,
                    proof.MoveX, proof.MoveZ, proof.MoveSpeed, proof.CanMove, proof.HasIsolatedLocomotion);
            }
            return MapCore(in baseline, movementProof);
        }

        private static GatewayStateSyncSnapshot MapCore(in WireStateSyncSnapshotPush push,
            GatewayStateSyncMovementProof? movementProof)
        {
            var source = push.Actors;
            var actors = source == null || source.Count == 0
                ? System.Array.Empty<GatewayStateSyncActorSnapshot>()
                : new GatewayStateSyncActorSnapshot[source.Count];

            for (var i = 0; i < actors.Length; i++)
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
                ? System.Array.Empty<int>()
                : new int[removedSource.Count];
            for (var i = 0; i < removedActorIds.Length; i++)
            {
                removedActorIds[i] = removedSource[i];
            }

            var fixedBaseline = push.SchemaVersion == FixedBaselineStateSyncProtocol.SchemaVersion;
            var schemaVersion = push.SchemaVersion;
            var generation = System.Guid.Empty;
            long snapshotId = 0;
            long baseSnapshotId = 0;
            byte[] payload;
            if (fixedBaseline)
            {
                if (!FixedBaselineStateSyncProtocol.TryDecode(push.Payload, out generation, out snapshotId,
                        out baseSnapshotId, out schemaVersion, out payload))
                    generation = System.Guid.Empty;
            }
            else
            {
                payload = push.Payload == null ? null : (byte[])push.Payload.Clone();
            }

            var payloadOpCode = push.PayloadOpCode;
            byte[] cueBaseline = null;
            if (payloadOpCode == MobaStateSyncCuePayloadCodec.OpCode &&
                MobaStateSyncCuePayloadCodec.TryDecode(payload, out var projectionOpCode, out var projectionSchemaVersion,
                    out var projection, out var cues))
            {
                payloadOpCode = projectionOpCode;
                schemaVersion = projectionSchemaVersion;
                payload = projection;
                cueBaseline = cues;
            }

            byte[] indicators = null, shields = null;
            if (payloadOpCode == MobaStateSyncOverheadPayloadCodec.OpCode)
            {
                var overhead = MobaStateSyncOverheadPayloadCodec.Decode(payload);
                payloadOpCode = overhead.ProjectionOpCode;
                schemaVersion = overhead.ProjectionSchemaVersion;
                payload = overhead.Projection;
                indicators = overhead.Indicators; shields = overhead.Shields;
            }

            return new GatewayStateSyncSnapshot(
                push.WorldId,
                push.Frame,
                push.Timestamp,
                push.IsFullSnapshot,
                actors,
                schemaVersion,
                removedActorIds,
                push.EventWatermark,
                push.EventEpoch,
                payloadOpCode,
                payload,
                push.ServerTicks,
                fixedBaseline,
                generation,
                snapshotId,
                baseSnapshotId,
                cueBaseline, indicators, shields, movementProof);
        }
    }
}
