#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal readonly struct PipelineDebuggerOverviewSource
    {
        public PipelineDebuggerOverviewSource(
            int runId,
            EAbilityPipelineState state,
            bool isPaused,
            AbilityPipelinePhaseId currentPhase,
            float elapsedTime,
            double wallDurationSeconds,
            IReadOnlyList<AbilityPipelinePhaseId> activePhases,
            DateTime startedUtc,
            DateTime? endedUtc,
            string pipelineType,
            string configType,
            string contextType)
        {
            RunId = runId;
            State = state;
            IsPaused = isPaused;
            CurrentPhase = currentPhase;
            ElapsedTime = elapsedTime;
            WallDurationSeconds = wallDurationSeconds;
            ActivePhases = activePhases
                ?? throw new ArgumentNullException(nameof(activePhases));
            StartedUtc = startedUtc;
            EndedUtc = endedUtc;
            PipelineType = pipelineType
                ?? throw new ArgumentNullException(nameof(pipelineType));
            ConfigType = configType
                ?? throw new ArgumentNullException(nameof(configType));
            ContextType = contextType
                ?? throw new ArgumentNullException(nameof(contextType));
        }

        public int RunId { get; }
        public EAbilityPipelineState State { get; }
        public bool IsPaused { get; }
        public AbilityPipelinePhaseId CurrentPhase { get; }
        public float ElapsedTime { get; }
        public double WallDurationSeconds { get; }
        public IReadOnlyList<AbilityPipelinePhaseId> ActivePhases { get; }
        public DateTime StartedUtc { get; }
        public DateTime? EndedUtc { get; }
        public string PipelineType { get; }
        public string ConfigType { get; }
        public string ContextType { get; }
    }

    internal sealed class PipelineDebuggerOverviewModel
    {
        private readonly List<string> _activePhaseLabels =
            new List<string>();

        public int RunId { get; private set; }
        public string StateLabel { get; private set; } = string.Empty;
        public string CurrentPhaseLabel { get; private set; } = string.Empty;
        public string ElapsedTimeLabel { get; private set; } = string.Empty;
        public string WallDurationLabel { get; private set; } = string.Empty;
        public IReadOnlyList<string> ActivePhaseLabels => _activePhaseLabels;
        public string StartedUtcLabel { get; private set; } = string.Empty;
        public string EndedUtcLabel { get; private set; } = string.Empty;
        public string PipelineType { get; private set; } = string.Empty;
        public string ConfigType { get; private set; } = string.Empty;
        public string ContextType { get; private set; } = string.Empty;
        public string? LastError { get; private set; }

        public void Rebuild(
            PipelineDebuggerOverviewSource source,
            IReadOnlyList<PipelineTraceEvent> trace)
        {
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            RunId = source.RunId;
            StateLabel = source.IsPaused
                ? "Executing (Paused)"
                : source.State.ToString();
            CurrentPhaseLabel = string.IsNullOrEmpty(
                source.CurrentPhase.Value)
                ? "No phase"
                : source.CurrentPhase.ToString();
            ElapsedTimeLabel = source.ElapsedTime.ToString("0.000") + " s";
            WallDurationLabel =
                source.WallDurationSeconds.ToString("0.000") + " s";
            StartedUtcLabel = source.StartedUtc.ToString(
                "yyyy-MM-dd HH:mm:ss.fff");
            EndedUtcLabel = source.EndedUtc?.ToString(
                "yyyy-MM-dd HH:mm:ss.fff") ?? "Running";
            PipelineType = source.PipelineType;
            ConfigType = source.ConfigType;
            ContextType = source.ContextType;
            LastError = FindLastError(trace);

            _activePhaseLabels.Clear();
            for (int i = 0; i < source.ActivePhases.Count; i++)
                _activePhaseLabels.Add(source.ActivePhases[i].ToString());
        }

        public static string? FindLastError(
            IReadOnlyList<PipelineTraceEvent> trace)
        {
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            for (int i = trace.Count - 1; i >= 0; i--)
            {
                if (trace[i].State == EAbilityPipelineState.Failed
                    && !string.IsNullOrEmpty(trace[i].Message))
                {
                    return trace[i].Message;
                }
            }

            return null;
        }
    }
}

#endif
