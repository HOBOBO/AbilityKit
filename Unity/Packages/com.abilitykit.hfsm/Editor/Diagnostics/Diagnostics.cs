using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Graph.Compilation;

namespace AbilityKit.HFSM.Editor.Diagnostics
{

    public static class Diagnostics
    {
        public static DiagnosticSnapshot Analyze(GraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var catalogAsset = EditorBindingCatalog.ConfiguredAsset;
            var export = catalogAsset != null
                ? DefinitionExporter.ExportUsingCatalogAsset(graph, catalogAsset)
                : DefinitionExporter.Export(graph);

            var issues = export.Issues.ToList();
            try
            {
                new StateMachineGraphCompiler().Compile(graph);
            }
            catch (GraphCompilationException exception)
            {
                foreach (var diagnostic in exception.Diagnostics)
                {
                    issues.Add(new DefinitionExportIssue(
                        "GRAPH_" + diagnostic.Code,
                        diagnostic.Severity == GraphDiagnosticSeverity.Error
                            ? LegacyImportSeverity.Error
                            : LegacyImportSeverity.Warning,
                        BuildGraphPath(diagnostic.ElementId, diagnostic.Code),
                        GetGraphDiagnosticMessage(diagnostic.Code)));
                }
            }

            if (issues.Count != export.Issues.Count)
            {
                export = new DefinitionExportResult(export.Definition, export.Json, issues);
            }

            return new DiagnosticSnapshot(
                export,
                catalogAsset != null ? catalogAsset.name : "程序集扫描");
        }

        private static string GetGraphDiagnosticMessage(string code)
        {
            switch (code)
            {
                case "NODE_NULL": return "状态机图中包含空节点。";
                case "NODE_ID_EMPTY": return "节点 ID 不能为空。";
                case "NODE_ID_DUPLICATE": return "节点 ID 不唯一。";
                case "EDGE_NULL": return "状态机图中包含空转换。";
                case "EDGE_ID_EMPTY": return "转换 ID 不能为空。";
                case "EDGE_ID_DUPLICATE": return "转换 ID 不唯一。";
                case "ROOT_MISSING": return "状态机图未指定根状态机。";
                case "ROOT_NOT_FOUND": return "根节点不存在。";
                case "ROOT_NOT_MACHINE": return "根节点必须是状态机。";
                case "ROOT_HAS_PARENT": return "根状态机不能包含父节点。";
                case "PARAMETER_NULL": return "状态机图中包含空参数。";
                case "PARAMETER_NAME_EMPTY": return "参数名称不能为空。";
                case "PARAMETER_NAME_DUPLICATE": return "参数名称不唯一。";
                case "CHILD_NOT_FOUND": return "子节点不存在。";
                case "CHILD_MULTIPLE_OWNERS": return "节点同时属于多个状态机。";
                case "PARENT_MISMATCH": return "节点记录的父状态机与实际所属状态机不一致。";
                case "RUNTIME_NAME_EMPTY": return "子节点的运行时名称不能为空。";
                case "RUNTIME_NAME_DUPLICATE": return "同一状态机内存在重复的运行时名称。";
                case "DEFAULT_NOT_CHILD": return "默认状态必须是当前状态机的直接子节点。";
                case "DEFAULT_MULTIPLE": return "一个状态机只能有一个默认状态。";
                case "DEFAULT_CONFLICT": return "显式默认状态与标记为默认的子状态冲突。";
                case "NODE_ORPHANED": return "节点不属于任何状态机。";
                case "HIERARCHY_CYCLE": return "状态机层级中存在循环引用。";
                case "EDGE_ORPHANED": return "转换不属于任何状态机。";
                case "EDGE_NOT_FOUND": return "状态机引用的转换不存在。";
                case "EDGE_MULTIPLE_OWNERS": return "转换同时属于多个状态机。";
                case "ANY_STATE_SOURCE_INVALID": return "任意状态转换必须以任意状态伪节点为来源。";
                case "EDGE_SOURCE_OUTSIDE_OWNER": return "转换来源必须是所属状态机的直接子节点。";
                case "EDGE_TARGET_OUTSIDE_OWNER": return "转换目标必须是所属状态机的直接子节点。";
                case "EDGE_TARGET_NOT_FOUND": return "转换目标不存在。";
                case "NODE_UNREACHABLE": return "无法从根状态机到达该节点。";
                case "CONDITION_CONFIG_INVALID": return "转换条件配置无效。";
                default: return "状态机图校验失败。";
            }
        }

        private static string BuildGraphPath(string elementId, string code)
        {
            if (string.IsNullOrEmpty(elementId))
                return "$.graph";

            return code.StartsWith("EDGE_", StringComparison.Ordinal) ||
                   code.StartsWith("ANY_STATE_", StringComparison.Ordinal) ||
                   code.StartsWith("CONDITION_", StringComparison.Ordinal)
                ? $"$.edges['{elementId}']"
                : $"$.nodes['{elementId}']";
        }

        public static DiagnosticTarget ResolveTarget(string path)
        {
            if (string.IsNullOrEmpty(path)) return default;

            var id = ExtractId(path, "$.edges['");
            if (!string.IsNullOrEmpty(id))
                return new DiagnosticTarget(DiagnosticTargetKind.Transition, id);

            id = ExtractId(path, "'].transitions['");
            if (!string.IsNullOrEmpty(id))
                return new DiagnosticTarget(DiagnosticTargetKind.Transition, id);

            id = ExtractId(path, "$.nodes['");
            if (!string.IsNullOrEmpty(id))
                return new DiagnosticTarget(DiagnosticTargetKind.Node, id);

            id = ExtractId(path, "'].states['");
            if (!string.IsNullOrEmpty(id))
                return new DiagnosticTarget(DiagnosticTargetKind.Node, id);

            id = ExtractId(path, "$.machines['");
            return string.IsNullOrEmpty(id)
                ? default
                : new DiagnosticTarget(DiagnosticTargetKind.Node, id);
        }

        private static string ExtractId(string path, string marker)
        {
            var start = path.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            start += marker.Length;
            var end = path.IndexOf("']", start, StringComparison.Ordinal);
            return end <= start ? string.Empty : path.Substring(start, end - start);
        }
    }
}
