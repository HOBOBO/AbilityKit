using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigDtoBytesDeserializer
    {
        Array DeserializeDtoArray(byte[] bytes, Type dtoType);
    }
}
