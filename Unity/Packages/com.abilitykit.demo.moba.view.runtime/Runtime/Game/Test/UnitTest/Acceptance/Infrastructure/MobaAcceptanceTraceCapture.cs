using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 在验收场景执行前开启的采集窗口。只在宿主逻辑线程读取诊断投影。
    /// </summary>
    internal sealed class MobaAcceptanceTraceCapture : IDisposable
    {
        private readonly MobaTraceRegistry _trace;
        private readonly IFrameTime _clock;
        private readonly int _startFrame;
        private string _incompleteReason;
        private bool _disposed;

        public MobaAcceptanceTraceCapture(MobaTraceRegistry trace, IFrameTime clock)
        {
            _trace = trace;
            _clock = clock;
            _startFrame = clock?.Frame.Value ?? 0;
            if (_trace == null || _clock == null)
                _incompleteReason = "trace-or-frame-clock-unavailable";
            if (_trace != null)
                _trace.RegistryEvent += Observe;
        }

        public MobaAcceptanceTraceRecord[] Capture(string caseId, out MobaAcceptanceTraceEvidence evidence)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaAcceptanceTraceCapture));
            var endFrame = _clock?.Frame.Value ?? _startFrame;
            var records = Project(_trace, _clock, caseId, _startFrame, endFrame, out var omittedNodes);
            var reason = _incompleteReason;
            if (reason == null && _trace.ObservationFailureCount != 0) reason = "trace-projection-failed";
            if (reason == null && (omittedNodes || endFrame < _startFrame)) reason = "trace-window-inconsistent";
            evidence = new MobaAcceptanceTraceEvidence
            {
                isComplete = reason == null,
                startFrame = _startFrame,
                endFrame = endFrame,
                recordCount = records.Length,
                reason = reason,
            };
            return records;
        }

        public static MobaAcceptanceTraceRecord[] ProjectRetainedNodes(MobaTraceRegistry trace, IFrameTime clock, string caseId) =>
            Project(trace, clock, caseId, int.MinValue, int.MaxValue, out _);

        private static MobaAcceptanceTraceRecord[] Project(MobaTraceRegistry trace, IFrameTime clock, string caseId,
            int startFrame, int endFrame, out bool omittedNodes)
        {
            omittedNodes = false;
            if (trace == null || clock == null) return Array.Empty<MobaAcceptanceTraceRecord>();
            var records = new List<MobaAcceptanceTraceRecord>();
            var mappedNodes = 0;
            foreach (MobaExecutionKind kind in Enum.GetValues(typeof(MobaExecutionKind)))
            {
                if (kind == MobaExecutionKind.None) continue;
                foreach (var node in trace.GetNodesByKind((int)kind))
                {
                    mappedNodes++;
                    if (node.CreatedFrame < startFrame) continue;
                    if (node.CreatedFrame > endFrame || !node.IsValid)
                    {
                        omittedNodes = true;
                        continue;
                    }

                    var metadata = node.Metadata;
                    records.Add(new MobaAcceptanceTraceRecord
                    {
                        caseId = caseId,
                        frame = node.CreatedFrame,
                        timeMs = (int)Math.Round(clock.FrameToTime(new FrameIndex(node.CreatedFrame)) * 1000d),
                        rootId = node.RootId,
                        parentId = node.ParentId,
                        nodeId = node.ContextId,
                        kind = kind.ToString(),
                        kindValue = node.Kind,
                        configId = metadata?.ConfigId ?? 0,
                        sourceActorId = metadata?.SourceActorId ?? 0,
                        targetActorId = metadata?.TargetActorId ?? 0,
                        sourceId = metadata?.SourceId ?? 0,
                        targetId = metadata?.TargetId ?? 0,
                        originSourceId = metadata?.OriginSourceId ?? 0,
                        originTargetId = metadata?.OriginTargetId ?? 0,
                        originSource = metadata?.OriginSource,
                        originTarget = metadata?.OriginTarget,
                        isRoot = node.IsRoot,
                        isEnded = node.IsEnded,
                        endedFrame = node.EndedFrame,
                        endReason = node.EndReason,
                        childCount = node.ChildCount,
                    });
                    if (metadata == null) omittedNodes = true;
                }
            }

            omittedNodes |= mappedNodes != trace.TotalNodeCount;
            records.Sort((left, right) =>
            {
                var rootOrder = left.rootId.CompareTo(right.rootId);
                return rootOrder != 0 ? rootOrder : left.nodeId.CompareTo(right.nodeId);
            });
            return records.ToArray();
        }

        private void Observe(TraceRegistryEvent change)
        {
            if (_incompleteReason != null) return;
            if (change.Kind == TraceRegistryEventKind.RootPurged || change.Kind == TraceRegistryEventKind.RegistryCleared
                || change.Kind == TraceRegistryEventKind.PredictionRetracted)
                _incompleteReason = change.Kind.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_trace != null) _trace.RegistryEvent -= Observe;
        }
    }
}
