using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置组接口，定义一组配置表的加载和反序列化方式
    /// </summary>
    public interface IConfigGroup
    {
        /// <summary>
        /// 组名称，用于调试和日志
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 该组使用的配置加载器
        /// </summary>
        IConfigGroupLoader Loader { get; }

        /// <summary>
        /// 该组使用的反序列化器
        /// </summary>
        IConfigGroupDeserializer Deserializer { get; }

        /// <summary>
        /// 该组包含的配置表
        /// </summary>
        IReadOnlyList<ConfigTableDefinition> Tables { get; }
    }

    /// <summary>
    /// 配置组加载器，负责从数据源获取原始配置数据
    /// </summary>
    public interface IConfigGroupLoader
    {
        /// <summary>
        /// 尝试加载指定表名的配置数据
        /// </summary>
        /// <param name="tableName">表名（不含扩展名）</param>
        /// <param name="bytes">字节数据（如果有）</param>
        /// <param name="text">文本数据（如果有）</param>
        /// <returns>是否成功加载</returns>
        bool TryLoad(string tableName, out byte[] bytes, out string text);

        /// <summary>
        /// 获取该加载器的资源目录
        /// </summary>
        string ResourcesDir { get; }
    }

    /// <summary>
    /// 配置组反序列化器，负责将原始数据转换为 DTO 数组
    /// </summary>
    public interface IConfigGroupDeserializer
    {
        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        Array DeserializeFromBytes(byte[] bytes, Type dtoType);

        /// <summary>
        /// 从文本反序列化
        /// </summary>
        Array DeserializeFromText(string text, Type dtoType);

        /// <summary>
        /// 是否支持该类型
        /// </summary>
        bool CanHandle(Type dtoType);
    }

    /// <summary>
    /// 配置组反序列化器基类，提供通用实现
    /// </summary>
    public abstract class ConfigGroupDeserializerBase : IConfigGroupDeserializer
    {
        public abstract Array DeserializeFromBytes(byte[] bytes, Type dtoType);
        public abstract Array DeserializeFromText(string text, Type dtoType);
        public abstract bool CanHandle(Type dtoType);

        protected static NotSupportedException CreateNotSupportedException(Type dtoType, string deserializerName)
        {
            return new NotSupportedException(
                $"[{deserializerName}] Deserialization not supported for dtoType: {dtoType.FullName}. " +
                $"Please migrate the table or extend {deserializerName}.");
        }
    }

    /// <summary>
    /// 配置组提供者接口，用于 DI 注入自定义配置组
    /// </summary>
    public interface IConfigGroupProvider
    {
        /// <summary>
        /// 获取所有配置组
        /// </summary>
        IReadOnlyList<IConfigGroup> GetGroups();
    }
}
