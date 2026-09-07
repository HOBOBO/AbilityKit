// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>
    /// JSON 序列化器接口 - 定义如何序列化对象为 JSON
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
        /// 从 JSON 字符串反序列化对象
        /// </summary>
        T Deserialize<T>(string json) where T : class;
    }
}
