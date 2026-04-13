using System;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct EnterMobaGamePayload
    {
        [BinaryMember(0)] public readonly float X;
        [BinaryMember(1)] public readonly float Y;
        [BinaryMember(2)] public readonly float Z;

        public EnterMobaGamePayload(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public EnterMobaGamePayload(in Vec3 pos)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
        }

        public Vec3 ToVec3() => new Vec3(X, Y, Z);
    }

    public static class EnterMobaGamePayloadCodec
    {
        public const int PayloadOpCode = 1;

        public static byte[] Serialize(in Vec3 pos)
        {
            var p = new EnterMobaGamePayload(in pos);
            return BinaryObjectCodec.Encode(p);
        }

        public static bool TryDeserializePosition(int opCode, byte[] payload, out Vec3 pos)
        {
            if (payload == null || payload.Length == 0)
            {
                pos = default;
                return false;
            }

            if (opCode != PayloadOpCode)
            {
                pos = default;
                return false;
            }

            try
            {
                var p = BinaryObjectCodec.Decode<EnterMobaGamePayload>(payload);
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
