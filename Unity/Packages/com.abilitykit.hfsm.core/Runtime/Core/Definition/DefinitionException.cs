#nullable enable
using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Definition
{

    public sealed class DefinitionException : InvalidOperationException
    {
        public DefinitionException(IReadOnlyList<ValidationIssue> issues)
            : base(BuildMessage(issues))
        {
            Issues = issues ?? Array.Empty<ValidationIssue>();
        }

        public IReadOnlyList<ValidationIssue> Issues { get; }

        private static string BuildMessage(IReadOnlyList<ValidationIssue>? issues)
        {
            if (issues == null || issues.Count == 0) return "The HFSM definition is invalid.";
            return $"The HFSM definition is invalid: {issues[0]}";
        }
    }
}
