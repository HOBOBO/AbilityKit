using MemoryPack;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.Core;

namespace AbilityKit.Core.Recording.Adapters.EventCodecs
{
    public static class WorldSnapshotEventCodec
    {
        public static byte[] Encode(in WorldStateSnapshot snapshot)
        {
            var payload = new WorldSnapshotEventPayload(snapshot.OpCode, snapshot.Payload);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static WorldStateSnapshot Decode(byte[] payload)
        {
            var p = MemoryPackSerializer.Deserialize<WorldSnapshotEventPayload>(payload);
            return new WorldStateSnapshot(p.OpCode, p.PayloadBytes);
        }

        public static void Write(IEventTrackWriter writer, FrameIndex frame, in WorldStateSnapshot snapshot)
        {
            if (writer == null) return;
            writer.Append(frame, RecordEventTypes.WorldSnapshot, Encode(in snapshot));
        }

        public static bool TryRead(in RecordEvent e, out WorldStateSnapshot snapshot)
        {
            if (e.EventType != RecordEventTypes.WorldSnapshot)
            {
                snapshot = default;
                return false;
            }

            snapshot = Decode(e.Payload);
            return true;
        }
    }

    public static class WorldDeltaEventCodec
    {
        public static byte[] Encode(in WorldStateSnapshot delta)
        {
            var payload = new WorldDeltaEventPayload(delta.OpCode, delta.Payload);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static WorldStateSnapshot Decode(byte[] payload)
        {
            var p = MemoryPackSerializer.Deserialize<WorldDeltaEventPayload>(payload);
            return new WorldStateSnapshot(p.OpCode, p.PayloadBytes);
        }

        public static void Write(IEventTrackWriter writer, FrameIndex frame, in WorldStateSnapshot delta)
        {
            if (writer == null) return;
            writer.Append(frame, RecordEventTypes.WorldDelta, Encode(in delta));
        }

        public static bool TryRead(in RecordEvent e, out WorldStateSnapshot delta)
        {
            if (e.EventType != RecordEventTypes.WorldDelta)
            {
                delta = default;
                return false;
            }

            delta = Decode(e.Payload);
            return true;
        }
    }

    [MemoryPackable]
    public readonly partial struct WorldSnapshotEventPayload
    {
        [MemoryPackOrder(0)] public readonly int OpCode;
        [MemoryPackOrder(1)] public readonly byte[] PayloadBytes;

        [MemoryPackConstructor]
        public WorldSnapshotEventPayload(int opCode, byte[] payloadBytes)
        {
            OpCode = opCode;
            PayloadBytes = payloadBytes;
        }
    }

    [MemoryPackable]
    public readonly partial struct WorldDeltaEventPayload
    {
        [MemoryPackOrder(0)] public readonly int OpCode;
        [MemoryPackOrder(1)] public readonly byte[] PayloadBytes;

        [MemoryPackConstructor]
        public WorldDeltaEventPayload(int opCode, byte[] payloadBytes)
        {
            OpCode = opCode;
            PayloadBytes = payloadBytes;
        }
    }
}
