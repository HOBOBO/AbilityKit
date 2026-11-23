using MemoryPack;
using System;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services
{
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

        public static bool TryDeserialize(byte[] payload, out SkillInputEvent evt, out string error)
        {
            evt = default;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "payload is null or empty";
                return false;
            }

            try
            {
                evt = Deserialize(payload);
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

