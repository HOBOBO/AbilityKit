#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal readonly struct PipelineDebuggerRunView
    {
        public PipelineDebuggerRunView(
            int runId,
            DateTime registeredAtUtc,
            bool isActive,
            bool isPinned,
            EAbilityPipelineState state,
            string ownerName,
            string pipelineType,
            string configType,
            string phaseId)
        {
            RunId = runId;
            RegisteredAtUtc = registeredAtUtc;
            IsActive = isActive;
            IsPinned = isPinned;
            State = state;
            OwnerName = ownerName ?? string.Empty;
            PipelineType = pipelineType ?? string.Empty;
            ConfigType = configType ?? string.Empty;
            PhaseId = phaseId ?? string.Empty;
        }

        public int RunId { get; }
        public DateTime RegisteredAtUtc { get; }
        public bool IsActive { get; }
        public bool IsPinned { get; }
        public EAbilityPipelineState State { get; }
        public string OwnerName { get; }
        public string PipelineType { get; }
        public string ConfigType { get; }
        public string PhaseId { get; }
    }

    internal static class PipelineDebuggerViewPolicy
    {
        public static bool MatchesRun(
            PipelineDebuggerRunView run,
            PipelineDebuggerRunFilter filter,
            string? search)
        {
            bool matchesFilter = filter switch
            {
                PipelineDebuggerRunFilter.Active => run.IsActive,
                PipelineDebuggerRunFilter.History => !run.IsActive,
                PipelineDebuggerRunFilter.Failed => run.State == EAbilityPipelineState.Failed,
                PipelineDebuggerRunFilter.Pinned => run.IsPinned,
                _ => true
            };

            if (!matchesFilter || string.IsNullOrWhiteSpace(search))
                return matchesFilter;

            return Contains(run.OwnerName, search)
                || Contains(run.PipelineType, search)
                || Contains(run.ConfigType, search)
                || Contains(run.PhaseId, search)
                || run.RunId.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int GetRunGroup(PipelineDebuggerRunView run)
        {
            if (run.IsActive) return 0;
            return run.IsPinned ? 1 : 2;
        }

        public static int FindNewestRunId(IReadOnlyList<PipelineDebuggerRunView> runs)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));
            if (runs.Count == 0)
                throw new ArgumentException("At least one run is required.", nameof(runs));

            int runId = runs[0].RunId;
            DateTime newest = runs[0].RegisteredAtUtc;
            for (int i = 1; i < runs.Count; i++)
            {
                if (runs[i].RegisteredAtUtc <= newest) continue;
                newest = runs[i].RegisteredAtUtc;
                runId = runs[i].RunId;
            }
            return runId;
        }

        public static int? ResolveSelection(
            int? selectedRunId,
            bool followLatest,
            int? registrySelectedRunId,
            IReadOnlyList<PipelineDebuggerRunView> visibleRuns)
        {
            if (visibleRuns == null)
                throw new ArgumentNullException(nameof(visibleRuns));
            if (visibleRuns.Count == 0)
                return null;

            bool selectedVisible = false;
            for (int i = 0; i < visibleRuns.Count; i++)
            {
                if (visibleRuns[i].RunId != selectedRunId) continue;
                selectedVisible = true;
                break;
            }

            if (followLatest)
            {
                int latestId = FindNewestRunId(visibleRuns);
                if (!selectedRunId.HasValue
                    || !selectedVisible
                    || registrySelectedRunId == latestId)
                {
                    return latestId;
                }
            }

            return selectedVisible
                ? selectedRunId
                : visibleRuns[0].RunId;
        }

        public static bool MatchesTrace(
            PipelineTraceEvent item,
            PipelineDebuggerTraceFilter filter,
            string? search)
        {
            bool matchesFilter = filter switch
            {
                PipelineDebuggerTraceFilter.Lifecycle =>
                    item.Type == EPipelineTraceEventType.RunStart
                    || item.Type == EPipelineTraceEventType.RunEnd,
                PipelineDebuggerTraceFilter.Phases =>
                    item.Type == EPipelineTraceEventType.PhaseStart
                    || item.Type == EPipelineTraceEventType.PhaseComplete
                    || item.Type == EPipelineTraceEventType.PhaseError,
                PipelineDebuggerTraceFilter.Errors =>
                    item.Type == EPipelineTraceEventType.PhaseError
                    || item.State == EAbilityPipelineState.Failed,
                PipelineDebuggerTraceFilter.Control =>
                    item.Type == EPipelineTraceEventType.Pause
                    || item.Type == EPipelineTraceEventType.Resume
                    || item.Type == EPipelineTraceEventType.Interrupt,
                _ => true
            };

            if (!matchesFilter || string.IsNullOrWhiteSpace(search))
                return matchesFilter;

            return Contains(item.PhaseId.ToString(), search)
                || Contains(item.Message, search)
                || Contains(item.State.ToString(), search)
                || Contains(item.Type.ToString(), search);
        }

        public static bool MatchesContext(
            string name,
            string initial,
            string current,
            string? search)
        {
            return string.IsNullOrWhiteSpace(search)
                || Contains(name, search)
                || Contains(initial, search)
                || Contains(current, search);
        }

        private static bool Contains(string? value, string? search)
        {
            return value != null
                && search != null
                && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

#endif
