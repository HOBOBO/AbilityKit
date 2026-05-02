using System;

namespace AbilityKit.Protocol.Serialization
{
    /// <summary>
    /// 文本序列化器接口
    /// </summary>
    public interface ITextSerializer
    {
        /// <summary>
        /// 序列化对象为字符串
        /// </summary>
        string Serialize<T>(T value, bool prettyPrint = false);

        /// <summary>
        /// 反序列化字符串为对象
        /// </summary>
        T Deserialize<T>(string text);
    }
}
