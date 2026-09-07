#nullable enable
using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Definition
{

    public sealed class ValidationResult
    {
        internal ValidationResult(List<ValidationIssue> issues)
        {
            Issues = issues.AsReadOnly();
        }

        public bool IsValid => Issues.Count == 0;

        public IReadOnlyList<ValidationIssue> Issues { get; }
    }
}
