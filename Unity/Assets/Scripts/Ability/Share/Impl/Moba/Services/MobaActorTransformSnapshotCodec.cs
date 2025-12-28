using System;
using System.IO;
using System.Text;

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

            using var ms = new MemoryStream(64 + entries.Length * 20);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                bw.Write(entries[i].actorId);
                bw.Write(entries[i].x);
                bw.Write(entries[i].y);
                bw.Write(entries[i].z);
            }

            bw.Flush();
            return ms.ToArray();
        }

        public static (int actorId, float x, float y, float z)[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 4) return Array.Empty<(int, float, float, float)>();

            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var count = br.ReadInt32();
            if (count <= 0) return Array.Empty<(int, float, float, float)>();

            var arr = new (int actorId, float x, float y, float z)[count];
            for (int i = 0; i < count; i++)
            {
                var id = br.ReadInt32();
                var x = br.ReadSingle();
                var y = br.ReadSingle();
                var z = br.ReadSingle();
                arr[i] = (id, x, y, z);
            }

            return arr;
        }
    }
}
