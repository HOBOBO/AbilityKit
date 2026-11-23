using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GatewayStateSyncSnapshotMapper
    {
        public static GatewayStateSyncSnapshot Map(in WireStateSyncSnapshotPush push)
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
                cueBaseline);
        }
    }
}
