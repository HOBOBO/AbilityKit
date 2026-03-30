using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 二进制 DTO 反序列化器接口
    /// </summary>
    public interface IMobaConfigDtoBytesDeserializer : IConfigDeserializer
    {
        /// <summary>
        /// 反序列化 DTO 数组（MOBA 专用方法）
        /// </summary>
        Array DeserializeDtoArray(byte[] bytes, Type dtoType);
    }
}
