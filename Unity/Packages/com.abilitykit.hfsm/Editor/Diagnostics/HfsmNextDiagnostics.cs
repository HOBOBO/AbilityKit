using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.HFSM.Unity.Migration;
using UnityHFSM.Editor.Export;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Compilation;

namespace UnityHFSM.Editor.Diagnostics
{
    public enum HfsmDiagnosticTargetKind
    {
        None = 0,
        Node = 1,
        Transition = 2,
    }

    public readonly struct HfsmDiagnosticTarget
    {
        public HfsmDiagnosticTarget(HfsmDiagnosticTargetKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }

        public HfsmDiagnosticTargetKind Kind { get; }

        public string Id { get; }

        public bool IsValid => Kind != HfsmDiagnosticTargetKind.None && !string.IsNullOrEmpty(Id);
    }

    public sealed class HfsmNextDiagnosticSnapshot
    {
        internal HfsmNextDiagnosticSnapshot(
            HfsmNextDefinitionExportResult exportResult,
            string catalogSource)
        {
            ExportResult = exportResult ?? throw new ArgumentNullException(nameof(exportResult));
            CatalogSource = catalogSource ?? string.Empty;
            ErrorCount = exportResult.Issues.Count(issue =>
                issue.Severity == HfsmLegacyImportSeverity.Error);
            WarningCount = exportResult.Issues.Count(issue =>
                issue.Severity == HfsmLegacyImportSeverity.Warning);
            DefinitionHash = exportResult.Definition == null
                ? string.Empty
                : exportResult.Definition.ComputeDefinitionHash().ToString("X16");
        }

        public HfsmNextDefinitionExportResult ExportResult { get; }

        public IReadOnlyList<HfsmNextDefinitionExportIssue> Issues => ExportResult.Issues;

        public string CatalogSource { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public string DefinitionHash { get; }

        public bool IsExportReady => ExportResult.IsSuccess;
        public EditorDiagnosticCollection ToPlatformDiagnostics(Action<HfsmDiagnosticTarget> locate = null)
        {
            var diagnostics = new EditorDiagnosticCollection();
            foreach (var issue in Issues)
            {
                var target = HfsmNextDiagnostics.ResolveTarget(issue.Path);
                diagnostics.Add(new EditorDiagnostic(
                    issue.Code,
                    issue.Severity == HfsmLegacyImportSeverity.Error
                        ? EditorDiagnosticSeverity.Error
                        : EditorDiagnosticSeverity.Warning,
                    issue.Message,
                    issue.Path,
                    locate: target.IsValid && locate != null
                        ? () => locate(target)
                        : (Action)null));
            }
            return diagnostics;
        }
    }

    public static class HfsmNextDiagnostics
    {
        public static HfsmNextDiagnosticSnapshot Analyze(HfsmGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var catalogAsset = HfsmEditorBindingCatalog.ConfiguredAsset;
            var export = catalogAsset != null
                ? HfsmNextDefinitionExporter.ExportUsingCatalogAsset(graph, catalogAsset)
                : HfsmNextDefinitionExporter.Export(graph);

            var issues = export.Issues.ToList();
            try
            {
                new StateMachineGraphCompiler().Compile(graph);
            }
            catch (GraphCompilationException exception)
            {
                foreach (var diagnostic in exception.Diagnostics)
                {
                    issues.Add(new HfsmNextDefinitionExportIssue(
                        "GRAPH_" + diagnostic.Code,
                        diagnostic.Severity == GraphDiagnosticSeverity.Error
                            ? HfsmLegacyImportSeverity.Error
                            : HfsmLegacyImportSeverity.Warning,
                        BuildGraphPath(diagnostic.ElementId, diagnostic.Code),
                        diagnostic.Message));
                }
            }

            if (issues.Count != export.Issues.Count)
            {
                export = new HfsmNextDefinitionExportResult(export.Definition, export.Json, issues);
            }

            return new HfsmNextDiagnosticSnapshot(
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

        public static HfsmDiagnosticTarget ResolveTarget(string path)
        {
            if (string.IsNullOrEmpty(path)) return default;

            var id = ExtractId(path, "$.edges['");
            if (!string.IsNullOrEmpty(id))
                return new HfsmDiagnosticTarget(HfsmDiagnosticTargetKind.Transition, id);

            id = ExtractId(path, "'].transitions['");
            if (!string.IsNullOrEmpty(id))
                return new HfsmDiagnosticTarget(HfsmDiagnosticTargetKind.Transition, id);

            id = ExtractId(path, "$.nodes['");
            if (!string.IsNullOrEmpty(id))
                return new HfsmDiagnosticTarget(HfsmDiagnosticTargetKind.Node, id);

            id = ExtractId(path, "'].states['");
            if (!string.IsNullOrEmpty(id))
                return new HfsmDiagnosticTarget(HfsmDiagnosticTargetKind.Node, id);

            id = ExtractId(path, "$.machines['");
            return string.IsNullOrEmpty(id)
                ? default
                : new HfsmDiagnosticTarget(HfsmDiagnosticTargetKind.Node, id);
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
