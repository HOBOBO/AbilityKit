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
                        diagnostic.Message));
                }
            }

            if (issues.Count != export.Issues.Count)
            {
                export = new DefinitionExportResult(export.Definition, export.Json, issues);
            }

            return new DiagnosticSnapshot(
                export,
                catalogAsset != null ? catalogAsset.name : "Assembly scan");
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
