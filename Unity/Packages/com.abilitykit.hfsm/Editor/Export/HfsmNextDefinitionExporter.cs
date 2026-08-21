using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Unity.Migration;
using UnityHFSM.Graph;

namespace UnityHFSM.Editor.Export
{
    public sealed class HfsmNextDefinitionExportIssue
    {
        public HfsmNextDefinitionExportIssue(
            string code,
            HfsmLegacyImportSeverity severity,
            string path,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public HfsmLegacyImportSeverity Severity { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Severity} at {Path}: {Message}";
    }

    public sealed class HfsmNextDefinitionExportResult
    {
        internal HfsmNextDefinitionExportResult(
            HfsmDefinition definition,
            string json,
            List<HfsmNextDefinitionExportIssue> issues)
        {
            Definition = definition;
            Json = json ?? string.Empty;
            Issues = issues.AsReadOnly();
        }

        public HfsmDefinition Definition { get; }

        public string Json { get; }

        public IReadOnlyList<HfsmNextDefinitionExportIssue> Issues { get; }

        public bool IsSuccess => Definition != null &&
                                 !string.IsNullOrEmpty(Json) &&
                                 Issues.All(issue => issue.Severity != HfsmLegacyImportSeverity.Error);
    }

    /// <summary>Graph -> validated Next Definition -> canonical JSON export pipeline.</summary>
    public static class HfsmNextDefinitionExporter
    {
        public static HfsmNextDefinitionExportResult Export(
            HfsmGraphAsset graph,
            HfsmBindingCatalog catalog = null)
        {
            var issues = new List<HfsmNextDefinitionExportIssue>();
            var import = HfsmLegacyGraphImporter.Import(graph);
            foreach (var issue in import.Issues)
            {
                issues.Add(new HfsmNextDefinitionExportIssue(
                    issue.Code, issue.Severity, issue.SourcePath, issue.Message));
            }

            if (import.Definition == null)
                return new HfsmNextDefinitionExportResult(null, string.Empty, issues);

            catalog = catalog ?? HfsmEditorBindingCatalog.Catalog;
            foreach (var catalogIssue in catalog.Issues)
            {
                issues.Add(new HfsmNextDefinitionExportIssue(
                    catalogIssue.Code,
                    HfsmLegacyImportSeverity.Error,
                    "$.bindings",
                    catalogIssue.ToString()));
            }
            ValidateBindings(import.Definition, catalog, issues);
            if (issues.Any(issue => issue.Severity == HfsmLegacyImportSeverity.Error))
                return new HfsmNextDefinitionExportResult(null, string.Empty, issues);

            return new HfsmNextDefinitionExportResult(
                import.Definition,
                HfsmDefinitionJson.Save(import.Definition),
                issues);
        }

        public static HfsmNextDefinitionExportResult ExportUsingCatalogAsset(
            HfsmGraphAsset graph,
            HfsmBindingCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
                throw new ArgumentNullException(nameof(catalogAsset));
            return Export(graph, catalogAsset.BuildCatalog());
        }

        private static void ValidateBindings(
            HfsmDefinition definition,
            HfsmBindingCatalog catalog,
            List<HfsmNextDefinitionExportIssue> issues)
        {
            foreach (var machine in definition.Machines)
            {
                foreach (var state in machine.States)
                {
                    ValidateKey(
                        catalog,
                        HfsmBindingKind.State,
                        state.BehaviorKey,
                        $"$.machines['{machine.Id}'].states['{state.Id}'].behaviorKey",
                        issues);
                }

                foreach (var transition in machine.Transitions)
                {
                    var path = $"$.machines['{machine.Id}'].transitions['{transition.Id}']";
                    ValidateKey(catalog, HfsmBindingKind.Condition, transition.ConditionKey,
                        path + ".conditionKey", issues);
                    ValidateKey(catalog, HfsmBindingKind.Action, transition.ActionKey,
                        path + ".actionKey", issues);
                }
            }
        }

        private static void ValidateKey(
            HfsmBindingCatalog catalog,
            HfsmBindingKind kind,
            string key,
            string path,
            List<HfsmNextDefinitionExportIssue> issues)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (catalog.Contains(kind, key)) return;
            issues.Add(new HfsmNextDefinitionExportIssue(
                "HFSMNEXT001",
                HfsmLegacyImportSeverity.Error,
                path,
                $"Unknown {kind} binding key '{key}'. Register an HfsmBinding descriptor before export."));
        }
    }
}
