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
    /// 将 Next 运行时导出与旧导出器注册表适配为确定的统一菜单模型，同时保留各自语义。
    /// </summary>
    public static class ExportActionRegistry
    {
        public static IReadOnlyList<ExportAction> CreateActions(
            Action exportNextDefinition,
            Action<string> exportLegacy)
        {
            if (exportNextDefinition == null)
                throw new ArgumentNullException(nameof(exportNextDefinition));
            if (exportLegacy == null)
                throw new ArgumentNullException(nameof(exportLegacy));

            var actions = new List<ExportAction>
            {
                new ExportAction(
                    "hfsm.export.next-definition",
                    "Next Runtime 定义",
                    "已校验的确定性运行时定义。",
                    exportNextDefinition)
            };

            foreach (var exporter in ExtensionRegistry.GetExporterInfos()
                         .OrderBy(info => info.Name, StringComparer.Ordinal))
            {
                var exporterName = exporter.Name;
                actions.Add(new ExportAction(
                    "hfsm.export.legacy." + exporterName.ToLowerInvariant(),
                    "旧版归档/" + exporterName,
                    exporter.Description,
                    () => exportLegacy(exporterName)));
            }

            return actions.AsReadOnly();
        }
    }
}
