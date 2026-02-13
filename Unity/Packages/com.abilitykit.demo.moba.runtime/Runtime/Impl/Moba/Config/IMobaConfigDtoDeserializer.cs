using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public interface IMobaConfigDtoDeserializer
    {
        Array DeserializeDtoArray(string text, Type dtoType);
    }
}
