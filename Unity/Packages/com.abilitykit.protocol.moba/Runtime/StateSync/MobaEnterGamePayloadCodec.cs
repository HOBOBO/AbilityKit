using AbilityKit.Core.Mathematics;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public partial struct MobaEnterGamePayload
    {
        public MobaEnterGamePayload(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3 ToVec3() => new Vec3(X, Y, Z);
    }

    public static class MobaEnterGamePayloadCodec
    {
        public const int PayloadOpCode = 1;

        public static byte[] Serialize(in Vec3 pos)
        {
            var p = new MobaEnterGamePayload { X = pos.X, Y = pos.Y, Z = pos.Z };
            return MemoryPackSerializer.Serialize(p);
        }

        public static bool TryDeserializePosition(int opCode, byte[] payload, out Vec3 pos)
        {
            if (opCode != PayloadOpCode || payload == null || payload.Length == 0)
            {
                pos = default;
                return false;
            }

            try
            {
                var p = MemoryPackSerializer.Deserialize<MobaEnterGamePayload>(payload);
                pos = p.ToVec3();
                return true;
            }
            catch
            {
                pos = default;
                return false;
            }
        }
    }
}
