#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    internal sealed class PipelineDebuggerToolbarModel
    {
        internal const int DefaultHistoryCapacity = 128;
        internal const int DefaultTraceCapacity = 2048;

        private static readonly int[] HistoryCapacityValues =
        {
            32,
            128,
            512,
            2048
        };

        private static readonly int[] TraceCapacityValues =
        {
            512,
            2048,
            8192,
            32768
        };

        private static readonly float[] RefreshIntervalValues =
        {
            0.05f,
            0.1f,
            0.25f
        };

        public bool CaptureEnabled { get; private set; }
        public bool FollowLatest { get; private set; }
        public bool RelativeTraceTime { get; private set; }
        public bool ConfirmInterrupt { get; private set; }
        public int HistoryCapacity { get; private set; }
        public int TraceCapacity { get; private set; }
        public float RefreshIntervalSeconds { get; private set; }
        public string StatsText { get; private set; } = string.Empty;
        public bool CanClearHistory { get; private set; }

        public IReadOnlyList<int> HistoryCapacities => HistoryCapacityValues;
        public IReadOnlyList<int> TraceCapacities => TraceCapacityValues;
        public IReadOnlyList<float> RefreshIntervals => RefreshIntervalValues;

        public void Rebuild(
            EditorPipelineRegistry.DebugStats stats,
            bool captureEnabled,
            bool followLatest,
            bool relativeTraceTime,
            bool confirmInterrupt,
            int historyCapacity,
            int traceCapacity,
            float refreshIntervalSeconds)
        {
            CaptureEnabled = captureEnabled;
            FollowLatest = followLatest;
            RelativeTraceTime = relativeTraceTime;
            ConfirmInterrupt = confirmInterrupt;
            HistoryCapacity = historyCapacity;
            TraceCapacity = traceCapacity;
            RefreshIntervalSeconds = ClampRefreshInterval(refreshIntervalSeconds);
            StatsText = $"Runs {stats.Total}  |  Active {stats.Active}  |  Failed {stats.Failed}  |  Pinned {stats.Pinned}";
            CanClearHistory = stats.History > 0;
        }

        public bool SetCaptureEnabled(bool value)
        {
            if (CaptureEnabled == value)
                return false;

            CaptureEnabled = value;
            return true;
        }

        public bool SetFollowLatest(bool value)
        {
            if (FollowLatest == value)
                return false;

            FollowLatest = value;
            return true;
        }

        public bool ToggleRelativeTraceTime()
        {
            RelativeTraceTime = !RelativeTraceTime;
            return RelativeTraceTime;
        }

        public bool ToggleConfirmInterrupt()
        {
            ConfirmInterrupt = !ConfirmInterrupt;
            return ConfirmInterrupt;
        }

        public void SetHistoryCapacity(int value)
        {
            if (!Contains(HistoryCapacityValues, value))
                throw new ArgumentOutOfRangeException(nameof(value));

            HistoryCapacity = value;
        }

        public void SetTraceCapacity(int value)
        {
            if (!Contains(TraceCapacityValues, value))
                throw new ArgumentOutOfRangeException(nameof(value));

            TraceCapacity = value;
        }

        public void SetRefreshInterval(float value)
        {
            if (!Contains(RefreshIntervalValues, value))
                throw new ArgumentOutOfRangeException(nameof(value));

            RefreshIntervalSeconds = value;
        }

        public bool IsRefreshIntervalSelected(float value)
        {
            return Approximately(RefreshIntervalSeconds, value);
        }

        public void ResetOptions()
        {
            CaptureEnabled = true;
            FollowLatest = true;
            RelativeTraceTime = true;
            ConfirmInterrupt = true;
            HistoryCapacity = DefaultHistoryCapacity;
            TraceCapacity = DefaultTraceCapacity;
            RefreshIntervalSeconds = PipelineDebuggerWorkspaceState.DefaultRefreshIntervalSeconds;
        }

        private static float ClampRefreshInterval(float value)
        {
            if (value < PipelineDebuggerWorkspaceState.MinRefreshIntervalSeconds)
                return PipelineDebuggerWorkspaceState.MinRefreshIntervalSeconds;
            return value > PipelineDebuggerWorkspaceState.MaxRefreshIntervalSeconds
                ? PipelineDebuggerWorkspaceState.MaxRefreshIntervalSeconds
                : value;
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<float> values, float value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Approximately(values[i], value))
                    return true;
            }

            return false;
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }
    }
}

#endif
