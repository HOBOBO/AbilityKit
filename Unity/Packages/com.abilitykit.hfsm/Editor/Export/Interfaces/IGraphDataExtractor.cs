// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>
    /// 数据提取器接口 - 定义如何从图描述器提取数据到可序列化 DTO
    /// 包外可以通过实现此接口来自定义数据提取逻辑
    /// </summary>
    public interface IGraphDataExtractor
    {
        /// <summary>
        /// 提取器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 从图描述器提取数据
        /// </summary>
        /// <param name="graph">图描述器</param>
        /// <param name="options">导出选项</param>
        /// <returns>导出的图数据</returns>
        ExportGraphData Extract(Graph.Descriptor.IGraphDescriptor graph, ExportOptions options);
    }
}
