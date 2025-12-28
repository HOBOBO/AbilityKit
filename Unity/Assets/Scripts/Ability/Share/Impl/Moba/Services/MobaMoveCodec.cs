using System;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaMoveCodec
    {
        // Payload: float x, float z
        public static byte[] Serialize(float x, float z)
        {
            var payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, payload, 4, 4);
            return payload;
        }

        public static void Deserialize(byte[] payload, out float x, out float z)
        {
            if (payload == null || payload.Length < 8)
            {
                x = 0f;
                z = 0f;
                return;
            }

            x = BitConverter.ToSingle(payload, 0);
            z = BitConverter.ToSingle(payload, 4);
        }
    }
}
