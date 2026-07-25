using MemoryPack;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Moba.CreateWorld
{
    public static class EnterMobaGameCodec
    {
        public static byte[] SerializeReq(in EnterMobaGameReq req)
        {
            return MemoryPackSerializer.Serialize(req);
        }

        public static EnterMobaGameReq DeserializeReq(byte[] bytes)
        {
            return MemoryPackSerializer.Deserialize<EnterMobaGameReq>(bytes);
        }

        public static byte[] SerializeRes(in EnterMobaGameRes res)
        {
            return MemoryPackSerializer.Serialize(res);
        }

        public static EnterMobaGameRes DeserializeRes(byte[] bytes)
        {
            return MemoryPackSerializer.Deserialize<EnterMobaGameRes>(bytes);
        }
    }
}
