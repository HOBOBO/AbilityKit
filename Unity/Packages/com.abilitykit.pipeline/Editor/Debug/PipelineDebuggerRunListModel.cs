#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal sealed class PipelineDebuggerRunListModel
    {
        private readonly List<PipelineDebuggerRunView> _visibleRuns =
            new List<PipelineDebuggerRunView>();

        public IReadOnlyList<PipelineDebuggerRunView> VisibleRuns =>
            _visibleRuns;

        public void Rebuild(
            IReadOnlyList<PipelineDebuggerRunView> runs,
            PipelineDebuggerRunFilter filter,
            string? search)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            _visibleRuns.Clear();
            for (int i = 0; i < runs.Count; i++)
            {
                PipelineDebuggerRunView run = runs[i];
                if (PipelineDebuggerViewPolicy.MatchesRun(
                        run,
                        filter,
                        search))
                {
                    _visibleRuns.Add(run);
                }
            }

            if (filter == PipelineDebuggerRunFilter.All)
            {
                _visibleRuns.Sort(CompareGroupedRuns);
            }
            else
            {
                _visibleRuns.Sort(CompareNewestFirst);
            }
        }

        public int? ResolveSelection(
            int? selectedRunId,
            bool followLatest,
            int? registrySelectedRunId)
        {
            return PipelineDebuggerViewPolicy.ResolveSelection(
                selectedRunId,
                followLatest,
                registrySelectedRunId,
                _visibleRuns);
        }

        private static int CompareGroupedRuns(
            PipelineDebuggerRunView left,
            PipelineDebuggerRunView right)
        {
            int groupOrder = PipelineDebuggerViewPolicy
                .GetRunGroup(left)
                .CompareTo(
                    PipelineDebuggerViewPolicy.GetRunGroup(right));

            return groupOrder != 0
                ? groupOrder
                : CompareNewestFirst(left, right);
        }

        private static int CompareNewestFirst(
            PipelineDebuggerRunView left,
            PipelineDebuggerRunView right)
        {
            return right.RegisteredAtUtc.CompareTo(
                left.RegisteredAtUtc);
        }
    }
}

#endif
