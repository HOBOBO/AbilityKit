using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor.Export
{

    public sealed class DefinitionExportResult
    {
        internal DefinitionExportResult(
            StateMachineDefinition definition,
            string json,
            List<DefinitionExportIssue> issues)
        {
            Definition = definition;
            Json = json ?? string.Empty;
            Issues = issues.AsReadOnly();
        }

        public StateMachineDefinition Definition { get; }

        public string Json { get; }

        public IReadOnlyList<DefinitionExportIssue> Issues { get; }

        public bool IsSuccess => Definition != null &&
                                 !string.IsNullOrEmpty(Json) &&
                                 Issues.All(issue => issue.Severity != LegacyImportSeverity.Error);
    }
}
