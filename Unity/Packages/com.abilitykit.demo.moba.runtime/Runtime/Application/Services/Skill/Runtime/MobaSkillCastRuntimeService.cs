using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Trace;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSkillCastRuntimeService))]
    public sealed class MobaSkillCastRuntimeService : IService
    {
        private static readonly ObjectPool<List<long>> s_longListPool = Pools.GetPool(
            createFunc: () => new List<long>(8),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64);

        private readonly Dictionary<long, MobaSkillCastRuntime> _runtimes = new Dictionary<long, MobaSkillCastRuntime>();
        private readonly Dictionary<long, long> _runtimeByTraceContextId = new Dictionary<long, long>();
        private readonly Dictionary<long, MobaSkillRuntimeRetainHandle> _retains = new Dictionary<long, MobaSkillRuntimeRetainHandle>();
        private readonly List<long> _endingBuffer = new List<long>(8);
        private readonly List<MobaSkillRuntimeChildRef> _diagnosticChildrenBuffer = new List<MobaSkillRuntimeChildRef>(8);
        private long _nextRuntimeId = 1L;
        private long _nextRetainId = 1L;
        private int _nextGeneration = 1;

        [WorldInject(required: false)]
        private MobaTraceRegistry _trace = null;

        public MobaSkillRuntimeLifecycleHookService LifecycleHooks { get; } = new MobaSkillRuntimeLifecycleHookService();

        public int Count => _runtimes.Count;

        public MobaSkillCastRuntime Create(in MobaSkillCastRuntimeCreateRequest request)
        {
            var runtimeId = _nextRuntimeId++;
            var generation = _nextGeneration++;
            if (_nextGeneration <= 0) _nextGeneration = 1;
            var runtime = new MobaSkillCastRuntime(runtimeId, generation, in request);
            _runtimes.Add(runtimeId, runtime);

            if (runtime.RootTraceContextId != 0L)
            {
                _runtimeByTraceContextId[runtime.RootTraceContextId] = runtimeId;
            }

            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.Created, runtime, default, default, MobaSkillRuntimeEndReason.None, forced: false);
            return runtime;
        }

        public bool TryCreate(in MobaSkillCastRuntimeCreateRequest request, out MobaSkillCastRuntimeHandle handle)
        {
            var runtime = Create(in request);
            handle = runtime.Handle;
            return handle.IsValid;
        }

        public bool TryGet(long runtimeId, out MobaSkillCastRuntime runtime)
        {
            if (runtimeId == 0L)
            {
                runtime = null;
                return false;
            }

            return _runtimes.TryGetValue(runtimeId, out runtime) && runtime != null && !runtime.IsEnded;
        }

        public bool TryGet(in MobaSkillCastRuntimeHandle handle, out MobaSkillCastRuntime runtime)
        {
            runtime = null;
            if (!handle.IsValid) return false;
            if (!TryGet(handle.RuntimeId, out var found)) return false;
            if (found.Generation != handle.Generation) return false;
            runtime = found;
            return true;
        }

        public bool TryGetByTraceContext(long traceContextId, out MobaSkillCastRuntime runtime)
        {
            runtime = null;
            if (traceContextId == 0L) return false;
            return _runtimeByTraceContextId.TryGetValue(traceContextId, out var runtimeId) && TryGet(runtimeId, out runtime);
        }

        public bool UpdateStage(long runtimeId, SkillCastStage stage)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            runtime.Stage = stage;
            return true;
        }

        public bool UpdateInput(long runtimeId, in AbilityKit.Core.Mathematics.Vec3 aimPos, in AbilityKit.Core.Mathematics.Vec3 aimDir, int targetActorId)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            runtime.UpdateInput(in aimPos, in aimDir, targetActorId);
            return true;
        }

        public bool TryGetBlackboard(in MobaSkillCastRuntimeHandle handle, out MobaSkillRuntimeBlackboard blackboard)
        {
            blackboard = null;
            if (!TryGet(in handle, out var runtime)) return false;
            blackboard = runtime.Blackboard;
            return blackboard != null;
        }

        public bool SetBlackboardValue(in MobaSkillCastRuntimeHandle handle, in MobaSkillRuntimeBlackboardKey key, in MobaSkillRuntimeValue value)
        {
            return TryGetBlackboard(in handle, out var blackboard) && blackboard.Set(in key, in value);
        }

        public bool TryGetBlackboardValue(in MobaSkillCastRuntimeHandle handle, in MobaSkillRuntimeBlackboardKey key, out MobaSkillRuntimeValue value)
        {
            value = default;
            return TryGetBlackboard(in handle, out var blackboard) && blackboard.TryGet(in key, out value);
        }

        public int AddBlackboardInt(in MobaSkillCastRuntimeHandle handle, in MobaSkillRuntimeBlackboardKey key, int delta = 1)
        {
            return TryGetBlackboard(in handle, out var blackboard) ? blackboard.AddInt(in key, delta) : 0;
        }

        public bool AddBlackboardActorId(in MobaSkillCastRuntimeHandle handle, in MobaSkillRuntimeBlackboardKey key, int actorId)
        {
            return TryGetBlackboard(in handle, out var blackboard) && blackboard.AddActorId(in key, actorId);
        }

        public bool AddBlackboardContextId(in MobaSkillCastRuntimeHandle handle, in MobaSkillRuntimeBlackboardKey key, long contextId)
        {
            return TryGetBlackboard(in handle, out var blackboard) && blackboard.AddContextId(in key, contextId);
        }

        public bool TryGetDiagnostics(in MobaSkillCastRuntimeHandle handle, out MobaSkillRuntimeDiagnostics diagnostics)
        {
            diagnostics = default;
            if (!TryGet(in handle, out var runtime)) return false;
            diagnostics = runtime.CreateDiagnosticsSnapshot();
            return true;
        }

        /// <summary>
        /// Returns an immutable inspection snapshot for one active runtime.
        /// The snapshot intentionally excludes mutable runtime and Blackboard references.
        /// </summary>
        public bool TryGetDetailDiagnostics(
            in MobaSkillCastRuntimeHandle handle,
            out MobaSkillRuntimeDetailDiagnostics diagnostics)
        {
            diagnostics = default;
            if (!TryGet(in handle, out var runtime)) return false;
            diagnostics = runtime.CreateDetailDiagnosticsSnapshot();
            return true;
        }

        /// <summary>
        /// 将当前活动技能运行时复制为独立诊断快照。
        /// 调用方只会拿到值类型快照，不会持有内部运行时实例。
        /// </summary>
        public int CopyDiagnosticsTo(List<MobaSkillRuntimeDiagnostics> results)
        {
            if (results == null) return 0;

            var start = results.Count;
            foreach (var pair in _runtimes)
            {
                var runtime = pair.Value;
                if (runtime == null || runtime.IsEnded) continue;
                results.Add(runtime.CreateDiagnosticsSnapshot());
            }

            return results.Count - start;
        }

        public bool TryGetDiagnostics(long runtimeId, out MobaSkillRuntimeDiagnostics diagnostics)
        {
            diagnostics = default;
            if (!TryGet(runtimeId, out var runtime)) return false;
            diagnostics = runtime.CreateDiagnosticsSnapshot();
            return true;
        }

        public int CopyPendingChildren(in MobaSkillCastRuntimeHandle handle, List<MobaSkillRuntimeChildRef> results, MobaSkillRuntimeChildKind kind = MobaSkillRuntimeChildKind.Unknown)
        {
            if (!TryGet(in handle, out var runtime)) return 0;
            return runtime.CopyChildrenTo(results, kind);
        }

        public int CountPendingChildren(in MobaSkillCastRuntimeHandle handle, MobaSkillRuntimeChildKind kind = MobaSkillRuntimeChildKind.Unknown)
        {
            return TryGet(in handle, out var runtime) ? runtime.CountChildren(kind) : 0;
        }

        public MobaSkillRuntimeScanResult ScanDiagnostics(IMobaBattleDiagnosticsService diagnostics = null, int maxWarningCount = 8)
        {
            var active = 0;
            var waiting = 0;
            var pendingChildren = 0;
            var warningCount = 0;

            foreach (var kv in _runtimes)
            {
                var runtime = kv.Value;
                if (runtime == null || runtime.IsEnded) continue;

                active++;
                pendingChildren += runtime.PendingChildren;
                if (!runtime.PipelineEnded || runtime.PendingChildren <= 0) continue;

                waiting++;
                if (diagnostics != null && warningCount < maxWarningCount)
                {
                    warningCount++;
                    _diagnosticChildrenBuffer.Clear();
                    runtime.CopyChildrenTo(_diagnosticChildrenBuffer);
                    diagnostics.Warning(
                        "moba.skill.runtime.waitingChildren." + runtime.RuntimeId,
                        () => FormatWaitingChildrenWarning(runtime, _diagnosticChildrenBuffer),
                        maxCount: 1);
                }
            }

            _diagnosticChildrenBuffer.Clear();
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.SkillRuntimeActive, active);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.SkillRuntimeWaitingChildren, waiting);
            diagnostics?.Gauge(MobaBattleDiagnosticMetric.SkillRuntimePendingChildren, pendingChildren);
            return new MobaSkillRuntimeScanResult(active, waiting, pendingChildren);
        }

        public bool RetainChild(long runtimeId, in MobaSkillRuntimeChildRef child)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            if (!runtime.RetainChild(in child)) return false;
            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.ChildRetained, runtime, in child, default, MobaSkillRuntimeEndReason.None, forced: false);
            return true;
        }

        public bool RetainChild(in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillRuntimeChildRef child, out MobaSkillRuntimeRetainHandle retainHandle)
        {
            retainHandle = default;
            if (!TryGet(in runtimeHandle, out var runtime)) return false;
            if (!runtime.RetainChild(in child)) return false;

            var retainId = _nextRetainId++;
            if (_nextRetainId == 0L) _nextRetainId = 1L;
            retainHandle = new MobaSkillRuntimeRetainHandle(retainId, runtime.Handle, in child);
            _retains.Add(retainId, retainHandle);
            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.ChildRetained, runtime, in child, in retainHandle, MobaSkillRuntimeEndReason.None, forced: false);
            return true;
        }

        public bool ReleaseChild(long runtimeId, in MobaSkillRuntimeChildRef child)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            var released = runtime.ReleaseChild(in child);
            if (released)
            {
                NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.ChildReleased, runtime, in child, default, MobaSkillRuntimeEndReason.None, forced: false);
                TryFinalize(runtime);
            }

            return released;
        }

        public bool ReleaseChild(in MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (!retainHandle.IsValid) return false;
            if (!_retains.TryGetValue(retainHandle.RetainId, out var stored)) return false;
            if (!stored.Equals(retainHandle)) return false;

            _retains.Remove(retainHandle.RetainId);

            var runtimeHandle = retainHandle.Runtime;
            var child = retainHandle.Child;
            if (!TryGet(in runtimeHandle, out var runtime)) return true;

            var released = runtime.ReleaseChild(in child);
            if (released)
            {
                NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.ChildReleased, runtime, in child, in retainHandle, MobaSkillRuntimeEndReason.None, forced: false);
                TryFinalize(runtime);
            }

            return released;
        }

        public bool MarkPipelineEnded(long runtimeId, MobaSkillRuntimeEndReason reason)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            return EndPipeline(runtime, reason, ToStage(reason));
        }

        public bool MarkPipelineEnded(in MobaSkillCastRuntimeHandle handle, MobaSkillRuntimeEndReason reason)
        {
            if (!TryGet(in handle, out var runtime)) return false;
            return EndPipeline(runtime, reason, ToStage(reason));
        }

        public bool Cancel(long runtimeId, MobaSkillRuntimeEndReason reason = MobaSkillRuntimeEndReason.Cancelled)
        {
            if (!TryGet(runtimeId, out var runtime)) return false;
            return EndPipeline(runtime, NormalizeCancelReason(reason), SkillCastStage.Cancelled);
        }

        public bool Cancel(in MobaSkillCastRuntimeHandle handle, MobaSkillRuntimeEndReason reason = MobaSkillRuntimeEndReason.Cancelled)
        {
            if (!TryGet(in handle, out var runtime)) return false;
            return EndPipeline(runtime, NormalizeCancelReason(reason), SkillCastStage.Cancelled);
        }

        public bool ForceTerminate(in MobaSkillCastRuntimeHandle handle, MobaSkillRuntimeEndReason reason = MobaSkillRuntimeEndReason.RollbackCleanup)
        {
            if (!TryGet(in handle, out var runtime)) return false;
            return ForceTerminateRuntime(runtime, reason, MobaSkillRuntimeLifecycleEventKind.ForceTerminated);
        }

        public void Clear()
        {
            while (_runtimes.Count > 0)
            {
                MobaSkillCastRuntime runtime = null;
                foreach (var pair in _runtimes)
                {
                    runtime = pair.Value;
                    break;
                }

                if (runtime == null)
                {
                    _runtimes.Clear();
                    break;
                }

                ForceTerminateRuntime(runtime, MobaSkillRuntimeEndReason.RollbackCleanup, MobaSkillRuntimeLifecycleEventKind.Cleared);
            }

            _runtimeByTraceContextId.Clear();
            _retains.Clear();
            _endingBuffer.Clear();
            _diagnosticChildrenBuffer.Clear();
        }

        public void Dispose()
        {
            Clear();
            LifecycleHooks.Clear();
        }

        private bool EndPipeline(MobaSkillCastRuntime runtime, MobaSkillRuntimeEndReason reason, SkillCastStage stage)
        {
            if (runtime == null || runtime.IsEnded || runtime.IsEnding || runtime.PipelineEnded) return false;
            runtime.PipelineEnded = true;
            runtime.Stage = stage;
            runtime.EndReason = reason == MobaSkillRuntimeEndReason.None ? MobaSkillRuntimeEndReason.PipelineCompleted : reason;
            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.PipelineEnded, runtime, default, default, runtime.EndReason, forced: false);
            TryFinalize(runtime);
            return true;
        }

        private bool ForceTerminateRuntime(
            MobaSkillCastRuntime runtime,
            MobaSkillRuntimeEndReason reason,
            MobaSkillRuntimeLifecycleEventKind lifecycleKind)
        {
            if (runtime == null || runtime.IsEnded || runtime.IsEnding) return false;
            runtime.PipelineEnded = true;
            runtime.Stage = SkillCastStage.Cancelled;
            runtime.EndReason = reason == MobaSkillRuntimeEndReason.None ? MobaSkillRuntimeEndReason.RollbackCleanup : reason;
            NotifyLifecycle(lifecycleKind, runtime, default, default, runtime.EndReason, forced: true);
            RevokeChildCapabilities(runtime, runtime.EndReason);
            TryFinalize(runtime, force: true);
            return true;
        }

        private void RevokeChildCapabilities(MobaSkillCastRuntime runtime, MobaSkillRuntimeEndReason reason)
        {
            if (runtime == null || runtime.PendingChildren == 0)
            {
                RemoveRetains(runtime != null ? runtime.Handle : default);
                return;
            }

            var children = new List<MobaSkillRuntimeChildRef>(runtime.PendingChildren);
            runtime.CopyChildrenTo(children);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var retainHandle = TakeRetain(runtime.Handle, in child);
                if (!runtime.ReleaseChild(in child)) continue;
                NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.ChildReleased, runtime, in child, in retainHandle, reason, forced: true);
            }

            RemoveRetains(runtime.Handle);
        }

        private MobaSkillRuntimeRetainHandle TakeRetain(in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillRuntimeChildRef child)
        {
            var retainId = 0L;
            var retainHandle = default(MobaSkillRuntimeRetainHandle);
            foreach (var pair in _retains)
            {
                if (!pair.Value.Runtime.Equals(runtimeHandle) || !pair.Value.Child.Equals(child)) continue;
                retainId = pair.Key;
                retainHandle = pair.Value;
                break;
            }

            if (retainId != 0L) _retains.Remove(retainId);
            return retainHandle;
        }

        private static MobaSkillRuntimeEndReason NormalizeCancelReason(MobaSkillRuntimeEndReason reason)
        {
            return reason == MobaSkillRuntimeEndReason.None ? MobaSkillRuntimeEndReason.Cancelled : reason;
        }

        private void TryFinalize(MobaSkillCastRuntime runtime, bool force = false)
        {
            if (runtime == null || runtime.IsEnded || runtime.IsEnding) return;
            if (!force && !runtime.PipelineEnded) return;
            if (!force && runtime.PendingChildren > 0)
            {
                var waitingReason = runtime.EndReason == MobaSkillRuntimeEndReason.None ? MobaSkillRuntimeEndReason.PipelineCompleted : runtime.EndReason;
                NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.WaitingChildren, runtime, default, default, waitingReason, forced: false);
                return;
            }

            runtime.IsEnding = true;
            var reason = runtime.EndReason == MobaSkillRuntimeEndReason.None ? MobaSkillRuntimeEndReason.PipelineCompleted : runtime.EndReason;
            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.Finalizing, runtime, default, default, reason, force);
            runtime.NotifyEnding(reason);

            if (runtime.RootTraceContextId != 0L)
            {
                try
                {
                    _trace?.EndContext(runtime.RootTraceContextId, ToTraceReason(reason));
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaSkillCastRuntimeService] Trace.EndContext failed (runtimeId={runtime.RuntimeId}, rootTraceContextId={runtime.RootTraceContextId}, reason={reason})");
                }

                _runtimeByTraceContextId.Remove(runtime.RootTraceContextId);
            }

            RemoveRetains(runtime.Handle);
            _endingBuffer.Add(runtime.RuntimeId);
            runtime.IsEnded = true;
            runtime.IsEnding = false;
            NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind.Finalized, runtime, default, default, reason, force);
            FlushEnded();
        }

        private void NotifyLifecycle(MobaSkillRuntimeLifecycleEventKind kind, MobaSkillCastRuntime runtime, in MobaSkillRuntimeChildRef child, in MobaSkillRuntimeRetainHandle retainHandle, MobaSkillRuntimeEndReason reason, bool forced)
        {
            if (runtime == null || LifecycleHooks.Count == 0) return;
            var lifecycleEvent = new MobaSkillRuntimeLifecycleEvent(kind, runtime, in child, in retainHandle, reason, forced);
            LifecycleHooks.Notify(in lifecycleEvent);
        }

        private static string FormatWaitingChildrenWarning(MobaSkillCastRuntime runtime, List<MobaSkillRuntimeChildRef> children)
        {
            if (runtime == null) return "[MobaSkillRuntime] runtime waiting children but runtime is null.";
            var text = $"[MobaSkillRuntime] pipeline ended but waiting children. runtime={runtime.Handle}, skillId={runtime.SkillId}, caster={runtime.CasterActorId}, target={runtime.TargetActorId}, pending={runtime.PendingChildren}, reason={runtime.EndReason}";
            if (children == null || children.Count == 0) return text;

            text += ", children=";
            for (var i = 0; i < children.Count; i++)
            {
                if (i > 0) text += ";";
                text += children[i].ToString();
            }

            return text;
        }

        private void FlushEnded()
        {
            if (_endingBuffer.Count == 0) return;
            for (int i = 0; i < _endingBuffer.Count; i++)
            {
                _runtimes.Remove(_endingBuffer[i]);
            }
            _endingBuffer.Clear();
        }

        private void RemoveRetains(in MobaSkillCastRuntimeHandle handle)
        {
            if (!handle.IsValid || _retains.Count == 0) return;

            var removeIds = s_longListPool.Get();
            try
            {
                foreach (var kv in _retains)
                {
                    if (kv.Value.Runtime.Equals(handle))
                    {
                        removeIds.Add(kv.Key);
                    }
                }

                for (int i = 0; i < removeIds.Count; i++)
                {
                    _retains.Remove(removeIds[i]);
                }
            }
            finally
            {
                s_longListPool.Release(removeIds);
            }
        }

        private static SkillCastStage ToStage(MobaSkillRuntimeEndReason reason)
        {
            switch (reason)
            {
                case MobaSkillRuntimeEndReason.Cancelled:
                case MobaSkillRuntimeEndReason.OwnerRemoved:
                case MobaSkillRuntimeEndReason.RollbackCleanup:
                    return SkillCastStage.Cancelled;
                case MobaSkillRuntimeEndReason.Failed:
                    return SkillCastStage.Failed;
                default:
                    return SkillCastStage.Completed;
            }
        }

        private static TraceLifecycleReason ToTraceReason(MobaSkillRuntimeEndReason reason)
        {
            switch (reason)
            {
                case MobaSkillRuntimeEndReason.Cancelled:
                    return TraceLifecycleReason.Cancelled;
                case MobaSkillRuntimeEndReason.Failed:
                    return TraceLifecycleReason.Failed;
                case MobaSkillRuntimeEndReason.OwnerRemoved:
                    return TraceLifecycleReason.Dead;
                case MobaSkillRuntimeEndReason.RollbackCleanup:
                    return TraceLifecycleReason.Cancelled;
                default:
                    return TraceLifecycleReason.Completed;
            }
        }
    }
}
