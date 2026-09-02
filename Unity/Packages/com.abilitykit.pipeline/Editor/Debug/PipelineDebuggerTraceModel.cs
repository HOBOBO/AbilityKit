#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal readonly struct PipelineDebuggerTraceRow
    {
        public PipelineDebuggerTraceRow(
            PipelineTraceEvent traceEvent,
            string timeLabel)
        {
            TraceEvent = traceEvent;
            TimeLabel = timeLabel
                ?? throw new ArgumentNullException(nameof(timeLabel));
        }

        public PipelineTraceEvent TraceEvent { get; }
        public string TimeLabel { get; }
    }

    internal sealed class PipelineDebuggerTraceModel
    {
        private readonly List<PipelineDebuggerTraceRow> _visibleRows =
            new List<PipelineDebuggerTraceRow>();

        public IReadOnlyList<PipelineDebuggerTraceRow> VisibleRows =>
            _visibleRows;

        public void Rebuild(
            IReadOnlyList<PipelineTraceEvent> trace,
            PipelineDebuggerTraceFilter filter,
            string? search,
            DateTime registeredAtUtc,
            bool relativeTime)
        {
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            _visibleRows.Clear();
            for (int i = 0; i < trace.Count; i++)
            {
                PipelineTraceEvent item = trace[i];
                if (!PipelineDebuggerViewPolicy.MatchesTrace(
                        item,
                        filter,
                        search))
                {
                    continue;
                }

                _visibleRows.Add(new PipelineDebuggerTraceRow(
                    item,
                    FormatTime(item, registeredAtUtc, relativeTime)));
            }
        }

        public static int? ResolveSelection(
            int? selectedSequence,
            IReadOnlyList<PipelineTraceEvent> trace)
        {
            if (!selectedSequence.HasValue)
                return null;
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            return TryFind(trace, selectedSequence.Value, out _)
                ? selectedSequence
                : null;
        }

        public static bool TryGetSelected(
            int? selectedSequence,
            IReadOnlyList<PipelineTraceEvent> trace,
            out PipelineTraceEvent selected)
        {
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            if (selectedSequence.HasValue)
                return TryFind(trace, selectedSequence.Value, out selected);

            selected = default;
            return false;
        }

        public static string FormatTime(
            PipelineTraceEvent item,
            DateTime registeredAtUtc,
            bool relativeTime)
        {
            if (!relativeTime)
                return item.UtcTime.ToString("HH:mm:ss.fff");

            double seconds = Math.Max(
                0d,
                (item.UtcTime - registeredAtUtc).TotalSeconds);
            return "+" + seconds.ToString("0.000");
        }

        private static bool TryFind(
            IReadOnlyList<PipelineTraceEvent> trace,
            int sequence,
            out PipelineTraceEvent selected)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace[i].Seq != sequence) continue;
                selected = trace[i];
                return true;
            }

            selected = default;
            return false;
        }
    }
}

#endif
