using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigDtoDeserializer
    {
        Array DeserializeDtoArray(string text, Type dtoType);
    }
}
