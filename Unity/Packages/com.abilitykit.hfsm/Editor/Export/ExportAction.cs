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
    /// Stable UI action exposed by the HFSM export menu. The action model keeps
    /// menu composition separate from exporter execution and allows registered
    /// legacy exporters to contribute without changing the editor window.
    /// </summary>
    public sealed class ExportAction
    {
        public ExportAction(
            string id,
            string label,
            string description,
            Action execute)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("必须提供稳定的导出操作 ID。", nameof(id));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("必须提供导出操作名称。", nameof(label));

            Id = id;
            Label = label;
            Description = description ?? string.Empty;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
        public Action Execute { get; }
    }
}
