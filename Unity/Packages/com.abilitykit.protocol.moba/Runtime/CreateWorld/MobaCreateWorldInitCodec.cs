using MemoryPack;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Moba.CreateWorld
{
    public static class MobaCreateWorldInitCodec
    {
        public static byte[] Serialize(in MobaCreateWorldInitPayload payload)
        {
            return MemoryPackSerializer.Serialize(payload);
        }

        public static bool TryDeserialize(byte[] bytes, out MobaCreateWorldInitPayload payload)
        {
            return TryDeserialize(bytes, out payload, out _);
        }

        public static bool TryDeserialize(byte[] bytes, out MobaCreateWorldInitPayload payload, out string error)
        {
            if (bytes == null || bytes.Length == 0)
            {
                payload = default;
                error = "payload is empty";
                return false;
            }

            try
            {
                payload = MemoryPackSerializer.Deserialize<MobaCreateWorldInitPayload>(bytes);
                error = null;
                return true;
            }
            catch (System.Exception ex)
            {
                payload = default;
                error = ex.Message;
                return false;
            }
        }
    }
}
