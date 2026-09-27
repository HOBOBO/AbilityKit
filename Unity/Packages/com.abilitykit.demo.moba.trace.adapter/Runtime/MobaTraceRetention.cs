using System;
using System.Linq;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTraceRetentionHandle : IDisposable
    {
        private readonly MobaTraceRegistry _trace;
        private readonly long _rootId;
        private readonly string _reason;
        private bool _disposed;

        internal MobaTraceRetentionHandle(MobaTraceRegistry trace, long rootId, string reason)
        {
            _trace = trace;
            _rootId = rootId;
            _reason = reason;
        }

        public bool IsValid => !_disposed && _trace != null && _rootId != 0;
        public long RootId => _rootId;
        public string Reason => _reason;

        public void Dispose()
        {
            if (!IsValid) return;
            _disposed = true;
            _trace.ReleaseRoot(_rootId);
        }
    }

    public readonly struct MobaTraceRetentionScanResult
    {
        public readonly int TotalRoots;
        public readonly int ActiveRoots;
        public readonly int RetainedRoots;
        public readonly int RetainedEndedRoots;
        public readonly int StaleRetainedRoots;
        public readonly int TotalNodes;
        public readonly int EndedNodes;

        public MobaTraceRetentionScanResult(
            int totalRoots,
            int activeRoots,
            int retainedRoots,
            int retainedEndedRoots,
            int staleRetainedRoots,
            int totalNodes,
            int endedNodes)
        {
            TotalRoots = totalRoots;
            ActiveRoots = activeRoots;
            RetainedRoots = retainedRoots;
            RetainedEndedRoots = retainedEndedRoots;
            StaleRetainedRoots = staleRetainedRoots;
            TotalNodes = totalNodes;
            EndedNodes = endedNodes;
        }
    }

    public static class MobaTraceRetentionExtensions
    {
        public static bool TryRetainPersistentSource(
            this MobaTraceRegistry trace,
            in MobaPersistentContextSourceSnapshot source,
            string reason,
            out MobaTraceRetentionHandle handle)
        {
            handle = default;
            if (trace == null || !source.IsValid) return false;
            if (!TryResolveRootId(in source, out var rootId)) return false;
            if (!trace.TryGetRootState(rootId, out _)) return false;

            trace.RetainRoot(rootId);
            handle = new MobaTraceRetentionHandle(trace, rootId, reason);
            return true;
        }

        public static bool TryRetainContextSource(
            this MobaTraceRegistry trace,
            in MobaContextSourceView source,
            string reason,
            out MobaTraceRetentionHandle handle)
        {
            var snapshot = MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);
            return trace.TryRetainPersistentSource(in snapshot, reason, out handle);
        }

        public static bool TryRetainPayloadSource(
            this MobaTraceRegistry trace,
            object payload,
            string reason,
            out MobaTraceRetentionHandle handle)
        {
            handle = default;
            if (trace == null || !MobaPersistentContextSourceSnapshotFactory.TryCapture(payload, out var snapshot)) return false;
            return trace.TryRetainPersistentSource(in snapshot, reason, out handle);
        }

        public static MobaTraceRetentionScanResult ScanRetention(
            this MobaTraceRegistry trace,
            IMobaBattleDiagnosticsService diagnostics = null,
            int staleFrameThreshold = 0,
            int currentFrame = 0,
            string warningKeyPrefix = "moba.trace.retention")
        {
            if (trace == null) return default;

            var totalRoots = 0;
            var activeRoots = 0;
            var retainedRoots = 0;
            var retainedEndedRoots = 0;
            var staleRetainedRoots = 0;
            var totalNodes = 0;
            var endedNodes = 0;

            foreach (var state in trace.GetRootStates().ToArray())
            {
                totalRoots++;
                if (state.ActiveCount > 0) activeRoots++;
                if (state.ExternalRefCount <= 0) continue;

                retainedRoots++;
                if (trace.TryGetRootStats(state.RootId, out var stats))
                {
                    totalNodes += stats.TotalNodes;
                    endedNodes += stats.EndedNodes;
                    if (stats.ActiveNodes == 0 && stats.EndedNodes > 1)
                    {
                        retainedEndedRoots++;
                    }
                }

                if (staleFrameThreshold > 0 && currentFrame > 0 && currentFrame - state.LastTouchedFrame >= staleFrameThreshold)
                {
                    staleRetainedRoots++;
                    diagnostics?.Warning(
                        warningKeyPrefix + ".stale." + state.RootId,
                        $"[MobaTraceRetention] Stale retained root detected root={state.RootId} refs={state.ExternalRefCount} active={state.ActiveCount} lastTouched={state.LastTouchedFrame} current={currentFrame}.");
                }
            }

            diagnostics?.Gauge(MobaBattleDiagnosticMetric.TraceRoots, totalRoots);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.TraceActiveRoots, activeRoots);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.TraceRetainedRoots, retainedRoots);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.TraceRetainedEndedRoots, retainedEndedRoots);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.TraceStaleRetainedRoots, staleRetainedRoots);

            return new MobaTraceRetentionScanResult(
                totalRoots,
                activeRoots,
                retainedRoots,
                retainedEndedRoots,
                staleRetainedRoots,
                totalNodes,
                endedNodes);
        }

        public static int PurgeReleased(this MobaTraceRegistry trace, int currentFrame, int keepEndedFrames = 0)
        {
            return trace != null ? trace.Purge(currentFrame, keepEndedFrames) : 0;
        }

        public static bool TryPurgeReleasedRoot(this MobaTraceRegistry trace, long rootId)
        {
            if (trace == null || rootId == 0) return false;
            if (!trace.TryGetRootState(rootId, out var state)) return false;
            if (state.ActiveCount > 0 || state.ExternalRefCount > 0) return false;

            trace.PurgeRoot(rootId);
            return true;
        }

        private static bool TryResolveRootId(in MobaPersistentContextSourceSnapshot source, out long rootId)
        {
            rootId = 0;
            if (!source.TryGetContextSource(out var view)) return false;
            rootId = view.RootContextId != 0 ? view.RootContextId : view.SourceContextId;
            return rootId != 0;
        }

    }
}
