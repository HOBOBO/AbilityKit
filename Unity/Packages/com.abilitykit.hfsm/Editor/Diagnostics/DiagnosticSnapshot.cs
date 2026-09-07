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

    public sealed class DiagnosticSnapshot
    {
        internal DiagnosticSnapshot(
            DefinitionExportResult exportResult,
            string catalogSource)
        {
            ExportResult = exportResult ?? throw new ArgumentNullException(nameof(exportResult));
            CatalogSource = catalogSource ?? string.Empty;
            ErrorCount = exportResult.Issues.Count(issue =>
                issue.Severity == LegacyImportSeverity.Error);
            WarningCount = exportResult.Issues.Count(issue =>
                issue.Severity == LegacyImportSeverity.Warning);
            DefinitionHash = exportResult.Definition == null
                ? string.Empty
                : exportResult.Definition.ComputeDefinitionHash().ToString("X16");
        }

        public DefinitionExportResult ExportResult { get; }

        public IReadOnlyList<DefinitionExportIssue> Issues => ExportResult.Issues;

        public string CatalogSource { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public string DefinitionHash { get; }

        public bool IsExportReady => ExportResult.IsSuccess;
        public EditorDiagnosticCollection ToPlatformDiagnostics(Action<DiagnosticTarget> locate = null)
        {
            var diagnostics = new EditorDiagnosticCollection();
            foreach (var issue in Issues)
            {
                var target = Diagnostics.ResolveTarget(issue.Path);
                diagnostics.Add(new EditorDiagnostic(
                    issue.Code,
                    issue.Severity == LegacyImportSeverity.Error
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
}
