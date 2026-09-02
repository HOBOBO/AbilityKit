#if UNITY_EDITOR

#nullable enable

using System;

namespace AbilityKit.Pipeline.Editor
{
    internal enum PipelineDebuggerRunFilter
    {
        All,
        Active,
        History,
        Failed,
        Pinned
    }

    internal enum PipelineDebuggerDetailTab
    {
        Overview,
        Phases,
        Trace,
        Context
    }

    internal enum PipelineDebuggerTraceFilter
    {
        All,
        Lifecycle,
        Phases,
        Errors,
        Control
    }

    internal sealed class PipelineDebuggerWorkspaceState
    {
        internal const float DefaultRunPaneWidth = 300f;
        internal const float DefaultRefreshIntervalSeconds = 0.1f;
        internal const float MinRunPaneWidth = 220f;
        internal const float MaxRunPaneWidth = 520f;
        internal const float MinRefreshIntervalSeconds = 0.05f;
        internal const float MaxRefreshIntervalSeconds = 1f;

        private float _runPaneWidth;
        private float _refreshIntervalSeconds;
        private bool _registryChanged;
        private double _nextRefreshAt;

        public PipelineDebuggerWorkspaceState()
        {
            Reset();
        }

        public string RunSearch { get; set; } = string.Empty;
        public string TraceSearch { get; set; } = string.Empty;
        public string ContextSearch { get; set; } = string.Empty;
        public PipelineDebuggerRunFilter RunFilter { get; set; }
        public PipelineDebuggerDetailTab DetailTab { get; set; }
        public PipelineDebuggerTraceFilter TraceFilter { get; set; }
        public bool FollowLatest { get; set; }
        public bool RelativeTraceTime { get; set; }
        public bool ConfirmInterrupt { get; set; }
        public bool ShowOnlyChangedContext { get; set; }
        public bool ShowPhaseGraph { get; set; }

        public float RunPaneWidth
        {
            get => _runPaneWidth;
            set => _runPaneWidth = Clamp(value, MinRunPaneWidth, MaxRunPaneWidth);
        }

        public float RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set => _refreshIntervalSeconds = Clamp(
                value,
                MinRefreshIntervalSeconds,
                MaxRefreshIntervalSeconds);
        }

        public void Restore(PipelineDebuggerUserState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            FollowLatest = state.FollowLatest;
            RelativeTraceTime = state.RelativeTraceTime;
            ConfirmInterrupt = state.ConfirmInterrupt;
            ShowOnlyChangedContext = state.ShowOnlyChangedContext;
            ShowPhaseGraph = state.ShowPhaseGraph;
            RunFilter = ParseEnum(state.RunFilter, PipelineDebuggerRunFilter.All);
            DetailTab = ParseEnum(state.DetailTab, PipelineDebuggerDetailTab.Overview);
            TraceFilter = ParseEnum(state.TraceFilter, PipelineDebuggerTraceFilter.All);
            RunSearch = state.RunSearch;
            TraceSearch = state.TraceSearch;
            ContextSearch = state.ContextSearch;
            RunPaneWidth = state.RunPaneWidth;
            RefreshIntervalSeconds = state.RefreshIntervalSeconds;
            ResetRefreshGate();
        }

        public void Persist(PipelineDebuggerUserState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            state.FollowLatest = FollowLatest;
            state.RelativeTraceTime = RelativeTraceTime;
            state.ConfirmInterrupt = ConfirmInterrupt;
            state.ShowOnlyChangedContext = ShowOnlyChangedContext;
            state.ShowPhaseGraph = ShowPhaseGraph;
            state.RunFilter = (int)RunFilter;
            state.DetailTab = (int)DetailTab;
            state.TraceFilter = (int)TraceFilter;
            state.RunSearch = RunSearch;
            state.TraceSearch = TraceSearch;
            state.ContextSearch = ContextSearch;
            state.RunPaneWidth = RunPaneWidth;
            state.RefreshIntervalSeconds = RefreshIntervalSeconds;
        }

        public void Reset()
        {
            RunSearch = string.Empty;
            TraceSearch = string.Empty;
            ContextSearch = string.Empty;
            RunFilter = PipelineDebuggerRunFilter.All;
            DetailTab = PipelineDebuggerDetailTab.Overview;
            TraceFilter = PipelineDebuggerTraceFilter.All;
            FollowLatest = true;
            RelativeTraceTime = true;
            ConfirmInterrupt = true;
            ShowOnlyChangedContext = false;
            ShowPhaseGraph = true;
            RunPaneWidth = DefaultRunPaneWidth;
            RefreshIntervalSeconds = DefaultRefreshIntervalSeconds;
            ResetRefreshGate();
        }

        public void MarkRegistryChanged()
        {
            _registryChanged = true;
        }

        public bool TryBeginRefresh(double now)
        {
            if (!_registryChanged && now < _nextRefreshAt)
                return false;

            _registryChanged = false;
            _nextRefreshAt = now + RefreshIntervalSeconds;
            return true;
        }

        public void ResetRefreshGate()
        {
            _registryChanged = false;
            _nextRefreshAt = 0d;
        }

        private static TEnum ParseEnum<TEnum>(int value, TEnum fallback)
            where TEnum : struct, Enum
        {
            return Enum.IsDefined(typeof(TEnum), value)
                ? (TEnum)Enum.ToObject(typeof(TEnum), value)
                : fallback;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}

#endif
