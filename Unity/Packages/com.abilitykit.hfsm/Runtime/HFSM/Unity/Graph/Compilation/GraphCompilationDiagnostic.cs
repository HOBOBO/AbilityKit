using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class GraphCompilationDiagnostic
    {
        public GraphCompilationDiagnostic(
            string code,
            string message,
            string elementId,
            GraphDiagnosticSeverity severity = GraphDiagnosticSeverity.Error)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ElementId = elementId ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }
        public string Message { get; }
        public string ElementId { get; }
        public GraphDiagnosticSeverity Severity { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(ElementId)
                ? $"{Code}: {Message}"
                : $"{Code} [{ElementId}]: {Message}";
        }
    }
}
