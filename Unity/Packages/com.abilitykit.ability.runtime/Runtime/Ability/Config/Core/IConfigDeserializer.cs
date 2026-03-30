using System;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置反序列化器接口，将原始数据反序列化为对象数组
    /// </summary>
    public interface IConfigDeserializer
    {
        /// <summary>
        /// 反序列化二进制数据
        /// </summary>
        Array DeserializeBytes(byte[] bytes, Type targetType);

        /// <summary>
        /// 反序列化文本数据
        /// </summary>
        Array DeserializeText(string text, Type targetType);

        /// <summary>
        /// 是否支持该类型
        /// </summary>
        bool CanHandle(Type targetType);
    }

    /// <summary>
    /// 配置反序列化器基类，提供通用实现
    /// </summary>
    public abstract class ConfigDeserializerBase : IConfigDeserializer
    {
        public abstract Array DeserializeBytes(byte[] bytes, Type targetType);
        public abstract Array DeserializeText(string text, Type targetType);
        public abstract bool CanHandle(Type targetType);

        protected static NotSupportedException CreateNotSupportedException(Type targetType, string deserializerName)
        {
            return new NotSupportedException(
                $"[{deserializerName}] Deserialization not supported for type: {targetType.FullName}. " +
                $"Please extend {deserializerName} or use a compatible deserializer.");
        }
    }
}
