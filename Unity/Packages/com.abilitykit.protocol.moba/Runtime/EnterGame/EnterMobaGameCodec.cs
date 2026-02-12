using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class EnterMobaGameCodec
    {
        public static byte[] SerializeReq(in EnterMobaGameReq req)
        {
            return WireSerializer.Serialize(in req);
        }

        public static EnterMobaGameReq DeserializeReq(byte[] bytes)
        {
            return WireSerializer.Deserialize<EnterMobaGameReq>(bytes);
        }

        public static byte[] SerializeRes(in EnterMobaGameRes res)
        {
            return WireSerializer.Serialize(in res);
        }

        public static EnterMobaGameRes DeserializeRes(byte[] bytes)
        {
            return WireSerializer.Deserialize<EnterMobaGameRes>(bytes);
        }
    }
}
