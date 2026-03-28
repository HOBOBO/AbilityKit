using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo
{
    /// <summary>
    /// Luban 字节码反序列化器（已弃用，改用 JSON 格式）
    /// </summary>
    public sealed class LubanMobaConfigDtoBytesDeserializer : IMobaConfigDtoBytesDeserializer
    {
        public Array DeserializeDtoArray(byte[] bytes, Type dtoType)
        {
            throw new NotSupportedException(
                "Luban bytes deserialization is no longer supported. " +
                "Please use JSON format (IMobaConfigDtoDeserializer) instead.");
        }
    }
}
