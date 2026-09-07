#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Migration
{

    public sealed class LegacyImportIssue
    {
        public LegacyImportIssue(
            string code,
            LegacyImportSeverity severity,
            string sourcePath,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            SourcePath = sourcePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public LegacyImportSeverity Severity { get; }

        public string SourcePath { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Severity} at {SourcePath}: {Message}";
    }
}
