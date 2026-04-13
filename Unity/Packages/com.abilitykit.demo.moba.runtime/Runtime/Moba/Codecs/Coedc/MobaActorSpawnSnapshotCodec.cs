using System;
using AbilityKit.Core.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    public enum SpawnEntityKind
    {
        Character = 1,
        Projectile = 2,
    }

    public static class MobaActorSpawnSnapshotCodec
    {
        public static byte[] Serialize(Entry[] entries)
        {
            var list = entries ?? Array.Empty<Entry>();
            var payload = new SnapshotPayload(list);
            return BinaryObjectCodec.Encode(payload);
        }

        public static Entry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return Array.Empty<Entry>();
            var p = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<Entry>();
        }

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly Entry[] Entries;

            public SnapshotPayload(Entry[] entries)
            {
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int NetId;
            [BinaryMember(1)] public readonly int Kind;
            [BinaryMember(2)] public readonly int Code;
            [BinaryMember(3)] public readonly int OwnerNetId;
            [BinaryMember(4)] public readonly float X;
            [BinaryMember(5)] public readonly float Y;
            [BinaryMember(6)] public readonly float Z;

            public Entry(int netId, int kind, int code, int ownerNetId, float x, float y, float z)
            {
                NetId = netId;
                Kind = kind;
                Code = code;
                OwnerNetId = ownerNetId;
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
}
