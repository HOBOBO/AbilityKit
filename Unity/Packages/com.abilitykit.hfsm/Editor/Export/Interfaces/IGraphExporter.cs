// ============================================================================
// Export Interfaces - 导出系统接口层
// 定义导出器、数据提取器的抽象接口，允许包外扩展
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>
    /// 导出器接口 - 定义如何将图数据导出为特定格式
    /// 包外可以通过实现此接口来添加自定义导出格式
    /// </summary>
    public interface IGraphExporter
    {
        /// <summary>
        /// 导出器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 文件扩展名
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// 导出器描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 导出图数据为特定格式
        /// </summary>
        /// <param name="graph">图描述器</param>
        /// <param name="options">导出选项</param>
        /// <returns>导出结果</returns>
        ExportResult Export(Graph.Descriptor.IGraphDescriptor graph, ExportOptions options);
    }
}
