using System;
using AbilityKit.Core.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaActorTransformSnapshotCodec
    {
        // Payload:
        // int count
        // repeated:
        // int actorId
        // float x,y,z
        public static byte[] Serialize((int actorId, float x, float y, float z)[] entries)
        {
            entries ??= Array.Empty<(int, float, float, float)>();

            var items = new Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                items[i] = new Entry(entries[i].actorId, entries[i].x, entries[i].y, entries[i].z);
            }

            var payload = new SnapshotPayload(items);
            return BinaryObjectCodec.Encode(payload);
        }

        public static (int actorId, float x, float y, float z)[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 4) return Array.Empty<(int, float, float, float)>();

            var p = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return Array.Empty<(int, float, float, float)>();

            var arr = new (int actorId, float x, float y, float z)[p.Entries.Length];
            for (int i = 0; i < p.Entries.Length; i++)
            {
                var e = p.Entries[i];
                arr[i] = (e.ActorId, e.X, e.Y, e.Z);
            }

            return arr;
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
            [BinaryMember(0)] public readonly int ActorId;
            [BinaryMember(1)] public readonly float X;
            [BinaryMember(2)] public readonly float Y;
            [BinaryMember(3)] public readonly float Z;

            public Entry(int actorId, float x, float y, float z)
            {
                ActorId = actorId;
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
}
