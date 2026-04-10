using System;
using AbilityKit.Core.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaMoveCodec
    {
        // Payload: float x, float z
        public static byte[] Serialize(float x, float z)
        {
            var payload = new MovePayload(x, z);
            return BinaryObjectCodec.Encode(payload);
        }

        public static void Deserialize(byte[] payload, out float x, out float z)
        {
            if (payload == null || payload.Length < 8)
            {
                x = 0f;
                z = 0f;
                return;
            }

            var p = BinaryObjectCodec.Decode<MovePayload>(payload);
            x = p.X;
            z = p.Z;
        }

        public readonly struct MovePayload
        {
            [BinaryMember(0)] public readonly float X;
            [BinaryMember(1)] public readonly float Z;

            public MovePayload(float x, float z)
            {
                X = x;
                Z = z;
            }
        }
    }
}
