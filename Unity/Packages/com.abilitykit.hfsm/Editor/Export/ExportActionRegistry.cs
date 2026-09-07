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
    /// Adapts the Next runtime export and the legacy exporter registry into one
    /// deterministic menu model while preserving their distinct semantics.
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
                    "Next Runtime Definition",
                    "Validated deterministic runtime definition.",
                    exportNextDefinition)
            };

            foreach (var exporter in ExtensionRegistry.GetExporterInfos()
                         .OrderBy(info => info.Name, StringComparer.Ordinal))
            {
                var exporterName = exporter.Name;
                actions.Add(new ExportAction(
                    "hfsm.export.legacy." + exporterName.ToLowerInvariant(),
                    "Legacy Archive/" + exporterName,
                    exporter.Description,
                    () => exportLegacy(exporterName)));
            }

            return actions.AsReadOnly();
        }
    }
}
