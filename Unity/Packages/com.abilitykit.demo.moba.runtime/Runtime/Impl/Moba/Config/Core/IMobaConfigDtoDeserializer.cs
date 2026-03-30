using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// MOBA DTO 反序列化器接口（扩展通用 IConfigDeserializer）
    /// </summary>
    public interface IMobaConfigDtoDeserializer : IConfigDeserializer
    {
        /// <summary>
        /// 反序列化 DTO 数组（MOBA 专用方法）
        /// </summary>
        Array DeserializeDtoArray(string text, Type dtoType);
    }
}
