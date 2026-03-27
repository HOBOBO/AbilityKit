using System;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// JSON 序列化器接口，支持可替换实现
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>
        /// 序列化器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        string Serialize<T>(T obj, bool prettyPrint = false) where T : class;

        /// <summary>
        /// 反序列化 JSON 字符串为对象
        /// </summary>
        T Deserialize<T>(string json) where T : class;
    }
}
