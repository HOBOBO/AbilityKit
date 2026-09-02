#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal sealed class PipelineDebuggerDetailsModel
    {
        private static readonly IReadOnlyList<string> CompactLabels =
            new[] { "Overview", "Phases", "Trace", "Context" };

        public PipelineDebuggerDetailTab SelectedTab { get; private set; } =
            PipelineDebuggerDetailTab.Overview;

        public void Select(PipelineDebuggerDetailTab tab)
        {
            if (!Enum.IsDefined(typeof(PipelineDebuggerDetailTab), tab))
                throw new ArgumentOutOfRangeException(nameof(tab));

            SelectedTab = tab;
        }

        public IReadOnlyList<string> BuildTabLabels(
            bool compact,
            int phaseCount,
            int traceCount,
            int contextCount)
        {
            if (phaseCount < 0)
                throw new ArgumentOutOfRangeException(nameof(phaseCount));
            if (traceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(traceCount));
            if (contextCount < 0)
                throw new ArgumentOutOfRangeException(nameof(contextCount));

            if (compact)
                return CompactLabels;

            return new[]
            {
                "Overview",
                "Phases " + phaseCount,
                "Trace " + traceCount,
                "Context " + contextCount
            };
        }
    }
}

#endif
