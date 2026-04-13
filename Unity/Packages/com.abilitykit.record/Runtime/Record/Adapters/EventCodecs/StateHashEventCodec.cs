using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Common.Record.Core;

namespace AbilityKit.Core.Common.Record.Adapters.EventCodecs
{
    public static class StateHashEventCodec
    {
        public static byte[] Encode(int version, WorldStateHash hash)
        {
            var payload = new Payload(version, hash.Value);
            return BinaryObjectCodec.Encode(payload);
        }

        public static void Decode(byte[] payload, out int version, out WorldStateHash hash)
        {
            var p = BinaryObjectCodec.Decode<Payload>(payload);
            version = p.Version;
            hash = new WorldStateHash(p.Hash);
        }

        public static void Write(IEventTrackWriter writer, FrameIndex frame, int version, WorldStateHash hash)
        {
            if (writer == null) return;
            writer.Append(frame, RecordEventTypes.StateHashSample, Encode(version, hash));
        }

        public static bool TryRead(in RecordEvent e, out int version, out WorldStateHash hash)
        {
            if (e.EventType != RecordEventTypes.StateHashSample)
            {
                version = 0;
                hash = default;
                return false;
            }

            Decode(e.Payload, out version, out hash);
            return true;
        }

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly uint Hash;

            public Payload(int version, uint hash)
            {
                Version = version;
                Hash = hash;
            }
        }
    }
}
