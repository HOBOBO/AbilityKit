using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class GraphCompilationException : Exception
    {
        public GraphCompilationException(IReadOnlyList<GraphCompilationDiagnostic> diagnostics)
            : base(CreateMessage(diagnostics))
        {
            Diagnostics = diagnostics == null
                ? Array.Empty<GraphCompilationDiagnostic>()
                : new ReadOnlyCollection<GraphCompilationDiagnostic>(new List<GraphCompilationDiagnostic>(diagnostics));
        }

        public IReadOnlyList<GraphCompilationDiagnostic> Diagnostics { get; }

        private static string CreateMessage(IReadOnlyList<GraphCompilationDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "State machine graph compilation failed.";

            var messages = new string[diagnostics.Count];
            for (var index = 0; index < diagnostics.Count; index++)
                messages[index] = diagnostics[index].ToString();
            return "State machine graph compilation failed: " + string.Join("; ", messages);
        }
    }
}
