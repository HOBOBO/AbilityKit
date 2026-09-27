using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;

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

            return new GatewayStateSyncSnapshot(
                push.WorldId,
                push.Frame,
                push.Timestamp,
                push.IsFullSnapshot,
                actors,
                push.SchemaVersion,
                removedActorIds,
                push.EventWatermark,
                push.EventEpoch,
                push.PayloadOpCode,
                push.Payload == null ? null : (byte[])push.Payload.Clone(),
                push.ServerTicks);
        }
    }
}
