using AbilityKit.Core.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaStateHashSnapshotCodec
    {
        public const int Version = 1;

        public static byte[] Serialize(int frame, uint hash)
        {
            var payload = new SnapshotPayload(Version, frame, hash);
            return BinaryObjectCodec.Encode(payload);
        }

        public static SnapshotPayload Deserialize(byte[] payload)
        {
            return BinaryObjectCodec.Decode<SnapshotPayload>(payload);
        }

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly int Frame;
            [BinaryMember(2)] public readonly uint Hash;

            public SnapshotPayload(int version, int frame, uint hash)
            {
                Version = version;
                Frame = frame;
                Hash = hash;
            }
        }
    }
}
