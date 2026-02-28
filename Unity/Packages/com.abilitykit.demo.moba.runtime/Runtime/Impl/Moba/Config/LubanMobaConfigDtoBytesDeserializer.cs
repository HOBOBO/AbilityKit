using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class LubanMobaConfigDtoBytesDeserializer : IMobaConfigDtoBytesDeserializer
    {
        public Array DeserializeDtoArray(byte[] bytes, Type dtoType)
        {
            throw new NotImplementedException(
                "Luban bytes deserializer is not wired yet. Provide a Luban-generated deserialization implementation and register it into DI as IMobaConfigDtoBytesDeserializer.");
        }
    }
}
