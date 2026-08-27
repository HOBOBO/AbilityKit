using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Unity.Migration;
using UnityHFSM.Editor.Export;
using UnityHFSM.Graph;

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
    }

    public static class HfsmNextDiagnostics
    {
        public static HfsmNextDiagnosticSnapshot Analyze(HfsmGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var catalogAsset = HfsmEditorBindingCatalog.ConfiguredAsset;
            if (catalogAsset != null)
            {
                return new HfsmNextDiagnosticSnapshot(
                    HfsmNextDefinitionExporter.ExportUsingCatalogAsset(graph, catalogAsset),
                    catalogAsset.name);
            }

            return new HfsmNextDiagnosticSnapshot(
                HfsmNextDefinitionExporter.Export(graph),
                "Assembly scan");
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
