using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share;
using AbilityKit.Ability.Share.Common.Record.Core;

namespace AbilityKit.Ability.Share.Common.Record.Adapters.EventCodecs
{
    public static class WorldSnapshotEventCodec
    {
        public static byte[] Encode(in WorldStateSnapshot snapshot)
        {
            var payload = new Payload(snapshot.OpCode, snapshot.Payload);
            return BinaryObjectCodec.Encode(payload);
        }

        public static WorldStateSnapshot Decode(byte[] payload)
        {
            var p = BinaryObjectCodec.Decode<Payload>(payload);
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

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int OpCode;
            [BinaryMember(1)] public readonly byte[] PayloadBytes;

            public Payload(int opCode, byte[] payload)
            {
                OpCode = opCode;
                PayloadBytes = payload;
            }
        }
    }
}
