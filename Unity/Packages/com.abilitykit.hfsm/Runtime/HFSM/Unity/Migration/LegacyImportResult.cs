#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Migration
{

    public sealed class LegacyImportResult
    {
        internal LegacyImportResult(StateMachineDefinition? definition, List<LegacyImportIssue> issues)
        {
            Definition = definition;
            Issues = issues.AsReadOnly();
        }

        /// <summary>Null when any error was found; warnings do not block import.</summary>
        public StateMachineDefinition? Definition { get; }

        public IReadOnlyList<LegacyImportIssue> Issues { get; }

        public bool IsSuccess => Definition != null && Issues.All(issue => issue.Severity != LegacyImportSeverity.Error);
    }
}
