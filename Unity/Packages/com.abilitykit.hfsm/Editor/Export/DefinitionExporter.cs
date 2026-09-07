using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>Graph -> validated Next Definition -> canonical JSON export pipeline.</summary>
    public static class DefinitionExporter
    {
        public static DefinitionExportResult Export(
            GraphAsset graph,
            BindingCatalog catalog = null)
        {
            var issues = new List<DefinitionExportIssue>();
            var import = LegacyGraphImporter.Import(graph);
            foreach (var issue in import.Issues)
            {
                issues.Add(new DefinitionExportIssue(
                    issue.Code, issue.Severity, issue.SourcePath, issue.Message));
            }

            if (import.Definition == null)
                return new DefinitionExportResult(null, string.Empty, issues);

            catalog = catalog ?? EditorBindingCatalog.Catalog;
            foreach (var catalogIssue in catalog.Issues)
            {
                issues.Add(new DefinitionExportIssue(
                    catalogIssue.Code,
                    LegacyImportSeverity.Error,
                    "$.bindings",
                    catalogIssue.ToString()));
            }
            ValidateBindings(import.Definition, catalog, issues);
            if (issues.Any(issue => issue.Severity == LegacyImportSeverity.Error))
                return new DefinitionExportResult(null, string.Empty, issues);

            return new DefinitionExportResult(
                import.Definition,
                DefinitionJson.Save(import.Definition),
                issues);
        }

        public static DefinitionExportResult ExportUsingCatalogAsset(
            GraphAsset graph,
            BindingCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
                throw new ArgumentNullException(nameof(catalogAsset));
            return Export(graph, catalogAsset.BuildCatalog());
        }

        private static void ValidateBindings(
            StateMachineDefinition definition,
            BindingCatalog catalog,
            List<DefinitionExportIssue> issues)
        {
            foreach (var machine in definition.Machines)
            {
                foreach (var state in machine.States)
                {
                    ValidateKey(
                        catalog,
                        BindingKind.State,
                        state.BehaviorKey,
                        $"$.machines['{machine.Id}'].states['{state.Id}'].behaviorKey",
                        issues);
                }

                foreach (var transition in machine.Transitions)
                {
                    var path = $"$.machines['{machine.Id}'].transitions['{transition.Id}']";
                    ValidateKey(catalog, BindingKind.Condition, transition.ConditionKey,
                        path + ".conditionKey", issues);
                    ValidateKey(catalog, BindingKind.Action, transition.ActionKey,
                        path + ".actionKey", issues);
                }
            }
        }

        private static void ValidateKey(
            BindingCatalog catalog,
            BindingKind kind,
            string key,
            string path,
            List<DefinitionExportIssue> issues)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (catalog.Contains(kind, key)) return;
            issues.Add(new DefinitionExportIssue(
                "HFSMNEXT001",
                LegacyImportSeverity.Error,
                path,
                $"Unknown {kind} binding key '{key}'. Register an Binding descriptor before export."));
        }
    }
}
