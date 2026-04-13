using AbilityKit.Core.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Demo.Moba.Services
{
    public static class SkillInputCodec
    {
        public static byte[] Serialize(in SkillInputEvent evt)
        {
            return BinaryObjectCodec.Encode(evt);
        }

        public static SkillInputEvent Deserialize(byte[] payload)
        {
            return BinaryObjectCodec.Decode<SkillInputEvent>(payload);
        }
    }
}
