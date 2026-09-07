using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor.Export
{
    public sealed class DefinitionExportIssue
    {
        public DefinitionExportIssue(
            string code,
            LegacyImportSeverity severity,
            string path,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public LegacyImportSeverity Severity { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Severity} at {Path}: {Message}";
    }
}
