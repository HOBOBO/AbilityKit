using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo
{
    public sealed class LubanMobaConfigDtoDeserializer : IMobaConfigDtoDeserializer
    {
        public Array DeserializeDtoArray(string text, Type dtoType)
        {
            throw new NotImplementedException("Luban deserializer is not wired yet. Provide a Luban-generated deserialization implementation and register it into DI as IMobaConfigDtoDeserializer.");
        }
    }
}
