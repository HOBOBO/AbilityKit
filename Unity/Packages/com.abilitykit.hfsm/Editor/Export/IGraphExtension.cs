// ============================================================================
// HFSM Extension Registry - 扩展点注册系统
// 允许包外代码注册自定义的导出器、数据提取器等扩展点
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Graph.Descriptor;


namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>
    /// 图形扩展接口 - 提供额外的图形处理能力
    /// </summary>
    public interface IGraphExtension
    {
        /// <summary>
        /// 扩展名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 优先级（数值越小越先执行）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 在导出前调用
        /// </summary>
        void OnBeforeExport(IGraphDescriptor graph, ExportOptions options);

        /// <summary>
        /// 在导出后调用
        /// </summary>
        void OnAfterExport(IGraphDescriptor graph, ExportOptions options, ExportResult result);
    }
}
