using System;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class EnterMobaGameCodec
    {
        public static byte[] SerializeReq(in EnterMobaGameReq req)
        {
            return BinaryObjectCodec.Encode(req);
        }

        public static EnterMobaGameReq DeserializeReq(byte[] bytes)
        {
            return BinaryObjectCodec.Decode<EnterMobaGameReq>(bytes);
        }

        public static byte[] SerializeRes(in EnterMobaGameRes res)
        {
            return BinaryObjectCodec.Encode(res);
        }

        public static EnterMobaGameRes DeserializeRes(byte[] bytes)
        {
            return BinaryObjectCodec.Decode<EnterMobaGameRes>(bytes);
        }
    }
}
