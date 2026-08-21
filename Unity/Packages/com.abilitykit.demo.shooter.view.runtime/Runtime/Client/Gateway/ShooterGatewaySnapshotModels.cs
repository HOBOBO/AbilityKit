#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public readonly struct ShooterGatewaySnapshot
    {
        public readonly ulong WorldId;
        public readonly int Frame;
        public readonly double Timestamp;
        public readonly long ServerTicks;
        public readonly long EventWatermark;
        public readonly bool IsFullSnapshot;
        public readonly IReadOnlyList<ShooterGatewayActorSnapshot> Actors;
        public readonly int PayloadOpCode;
        public readonly byte[] PayloadBytes;
        public readonly ShooterPackedSnapshotPayload? PackedSnapshot;
        public readonly ShooterPureStateSnapshotPayload? PureStateSnapshot;

        public ShooterGatewaySnapshot(ulong worldId, int frame, double timestamp, bool isFullSnapshot, IReadOnlyList<ShooterGatewayActorSnapshot> actors, int payloadOpCode = 0, ShooterPackedSnapshotPayload? packedSnapshot = null)
            : this(worldId, frame, timestamp, 0L, 0L, isFullSnapshot, actors, payloadOpCode, packedSnapshot, null)
        {
        }

        public ShooterGatewaySnapshot(ulong worldId, int frame, double timestamp, long serverTicks, bool isFullSnapshot, IReadOnlyList<ShooterGatewayActorSnapshot> actors, int payloadOpCode = 0, ShooterPackedSnapshotPayload? packedSnapshot = null, ShooterPureStateSnapshotPayload? pureStateSnapshot = null, byte[]? payloadBytes = null)
            : this(worldId, frame, timestamp, serverTicks, 0L, isFullSnapshot, actors, payloadOpCode, packedSnapshot, pureStateSnapshot, payloadBytes)
        {
        }

        public ShooterGatewaySnapshot(ulong worldId, int frame, double timestamp, long serverTicks, long eventWatermark, bool isFullSnapshot, IReadOnlyList<ShooterGatewayActorSnapshot> actors, int payloadOpCode = 0, ShooterPackedSnapshotPayload? packedSnapshot = null, ShooterPureStateSnapshotPayload? pureStateSnapshot = null, byte[]? payloadBytes = null)
        {
            WorldId = worldId;
            Frame = frame;
            Timestamp = timestamp;
            ServerTicks = serverTicks;
            EventWatermark = eventWatermark;
            IsFullSnapshot = isFullSnapshot;
            Actors = actors ?? Array.Empty<ShooterGatewayActorSnapshot>();
            PayloadOpCode = payloadOpCode;
            PayloadBytes = payloadBytes ?? Array.Empty<byte>();
            PackedSnapshot = packedSnapshot;
            PureStateSnapshot = pureStateSnapshot;
        }
    }

    public readonly struct ShooterGatewayActorSnapshot
    {
        public readonly int ActorId;
        public readonly float X;
        public readonly float Y;
        public readonly float Rotation;
        public readonly float VelocityX;
        public readonly float VelocityY;
        public readonly float Hp;
        public readonly float HpMax;
        public readonly int TeamId;

        public ShooterGatewayActorSnapshot(int actorId, float x, float y, float rotation, float velocityX, float velocityY, float hp, float hpMax, int teamId)
        {
            ActorId = actorId;
            X = x;
            Y = y;
            Rotation = rotation;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Hp = hp;
            HpMax = hpMax;
            TeamId = teamId;
        }
    }

    public static class ShooterGatewaySnapshotMapper
    {
        public static ShooterGatewaySnapshot ToGatewaySnapshot(in WireStateSyncSnapshotPush push)
        {
            return ToGatewaySnapshot(in push, new ShooterPureStateSyncDecodeBuffer());
        }

        internal static ShooterGatewaySnapshot ToGatewaySnapshot(
            in WireStateSyncSnapshotPush push,
            ShooterPureStateSyncDecodeBuffer pureStateDecodeBuffer)
        {
            var packedSnapshot = TryDecodePackedSnapshot(push.PayloadOpCode, push.Payload);
            var pureStateSnapshot = TryDecodePureStateSnapshot(
                push.PayloadOpCode,
                push.Payload,
                pureStateDecodeBuffer);
            var source = push.Actors;
            if (source == null || source.Count == 0)
            {
                return new ShooterGatewaySnapshot(
                    push.WorldId,
                    push.Frame,
                    push.Timestamp,
                    push.ServerTicks,
                    push.EventWatermark,
                    push.IsFullSnapshot,
                    Array.Empty<ShooterGatewayActorSnapshot>(),
                    push.PayloadOpCode,
                    packedSnapshot,
                    pureStateSnapshot,
                    push.Payload);
            }

            var actors = new ShooterGatewayActorSnapshot[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                var actor = source[i];
                actors[i] = new ShooterGatewayActorSnapshot(
                    actor.ActorId,
                    actor.X,
                    actor.Z,
                    actor.Rotation,
                    actor.VelocityX,
                    actor.VelocityZ,
                    actor.Hp,
                    actor.HpMax,
                    actor.TeamId);
            }

            return new ShooterGatewaySnapshot(
                push.WorldId,
                push.Frame,
                push.Timestamp,
                push.ServerTicks,
                push.EventWatermark,
                push.IsFullSnapshot,
                actors,
                push.PayloadOpCode,
                packedSnapshot,
                pureStateSnapshot,
                push.Payload);
        }

        private static ShooterPackedSnapshotPayload? TryDecodePackedSnapshot(int payloadOpCode, byte[]? payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            if (payloadOpCode != ShooterOpCodes.Snapshot.PackedState && payloadOpCode != ShooterOpCodes.Snapshot.PackedStateDelta)
            {
                return null;
            }

            return ShooterPackedSnapshotCodec.Deserialize(payload);
        }

        private static ShooterPureStateSnapshotPayload? TryDecodePureStateSnapshot(
            int payloadOpCode,
            byte[]? payload,
            ShooterPureStateSyncDecodeBuffer decodeBuffer)
        {
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            if (payloadOpCode != ShooterOpCodes.Snapshot.PureState && payloadOpCode != ShooterOpCodes.Snapshot.PureStateDelta)
            {
                return null;
            }

            return decodeBuffer.Decode(payload.AsSpan());
        }
    }
}
