using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Core.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
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
