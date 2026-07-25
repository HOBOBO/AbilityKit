using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    [MemoryPackable]
    public partial struct MobaMovePayload
    {
        [MemoryPackOrder(0)] public float X;
        [MemoryPackOrder(1)] public float Z;

        public MobaMovePayload(float x, float z)
        {
            X = x;
            Z = z;
        }
    }

    public static class MobaMoveCodec
    {
        public static byte[] Serialize(float x, float z)
        {
            var payload = new MobaMovePayload { X = x, Z = z };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static void Deserialize(byte[] payload, out float x, out float z)
        {
            if (payload == null || payload.Length == 0)
            {
                x = 0f;
                z = 0f;
                return;
            }

            var p = MemoryPackSerializer.Deserialize<MobaMovePayload>(payload);
            x = p.X;
            z = p.Z;
        }

        public static bool TryDeserialize(byte[] payload, out float x, out float z, out string error)
        {
            x = 0f;
            z = 0f;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "payload is null or empty";
                return false;
            }

            try
            {
                var p = MemoryPackSerializer.Deserialize<MobaMovePayload>(payload);
                x = p.X;
                z = p.Z;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }
}
