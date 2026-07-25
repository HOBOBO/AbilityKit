using MemoryPack;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Console.Services
{
    /// <summary>
    /// Console skill input codec backed by the official MOBA protocol DTO.
    /// </summary>
    public static class SkillInputCodec
    {
        public static byte[] Serialize(in SkillInputEvent evt)
        {
            return MemoryPackSerializer.Serialize(evt);
        }

        public static SkillInputEvent Deserialize(byte[] payload)
        {
            return MemoryPackSerializer.Deserialize<SkillInputEvent>(payload);
        }
    }
}
