using MemoryPack;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Recording.Core;

namespace AbilityKit.Core.Recording.Adapters.EventCodecs
{
    public static class StateHashEventCodec
    {
        public static byte[] Encode(int version, WorldStateHash hash)
        {
            var payload = new StateHashEventPayload(version, hash.Value);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static void Decode(byte[] payload, out int version, out WorldStateHash hash)
        {
            var p = MemoryPackSerializer.Deserialize<StateHashEventPayload>(payload);
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
    }

    [MemoryPackable]
    public readonly partial struct StateHashEventPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly uint Hash;

                public StateHashEventPayload(int version, uint hash)
        {
            Version = version;
            Hash = hash;
        }
    }
}
