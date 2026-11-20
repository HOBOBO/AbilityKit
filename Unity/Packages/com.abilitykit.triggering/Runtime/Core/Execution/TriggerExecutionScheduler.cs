using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    public readonly struct TriggerScheduledExecutionHandle : IEquatable<TriggerScheduledExecutionHandle>
    {
        public TriggerScheduledExecutionHandle(long id) => Id = id;

        public long Id { get; }
        public bool IsValid => Id > 0L;

        public bool Equals(TriggerScheduledExecutionHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TriggerScheduledExecutionHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => IsValid ? Id.ToString() : "Invalid";
    }

    public enum TriggerScheduledExecutionState : byte
    {
        Waiting = 0,
        Completed = 1,
        Interrupted = 2,
        Failed = 3,
    }

    public readonly struct TriggerScheduledExecutionInfo
    {
        public TriggerScheduledExecutionInfo(
            long id,
            TriggerScheduledExecutionState state,
            double elapsedMs,
            double lastExecutionMs,
            int executionCount,
            string failureReason)
        {
            Id = id;
            State = state;
            ElapsedMs = elapsedMs;
            LastExecutionMs = lastExecutionMs;
            ExecutionCount = executionCount;
            FailureReason = failureReason ?? string.Empty;
        }

        public long Id { get; }
        public TriggerScheduledExecutionState State { get; }
        public double ElapsedMs { get; }
        public double LastExecutionMs { get; }
        public int ExecutionCount { get; }
        public string FailureReason { get; }
    }

    public interface ITriggerExecutionScheduler
    {
        TriggerScheduledExecutionHandle Schedule<TCtx>(
            ITriggerPlanExecutable executable,
            object args,
            in ExecCtx<TCtx> context,
            EScheduleMode mode,
            float intervalMs,
            int maxExecutions,
            bool canBeInterrupted)
            where TCtx : class;
    }

    /// <summary>
    /// 帧边界的有序调度进度和下一个执行 ID，不包含定义或执行绑定。
    /// 构造后，调用方在预检和恢复期间不得修改传入的数组。
    /// </summary>
    public readonly struct TriggerExecutionProgress
    {
        /// <summary>
        /// 使用调用方已有的独立进度数组，不另行复制。
        /// </summary>
        public TriggerExecutionProgress(long nextExecutionId, TriggerScheduledExecutionInfo[] entries)
        {
            NextExecutionId = nextExecutionId;
            Entries = entries ?? Array.Empty<TriggerScheduledExecutionInfo>();
        }

        /// <summary>
        /// 此帧之后分配的下一个执行 ID。
        /// </summary>
        public long NextExecutionId { get; }

        /// <summary>
        /// 捕获时按调度顺序排列的进度，包含仍保留的终态。
        /// </summary>
        public TriggerScheduledExecutionInfo[] Entries { get; }
    }

    /// <summary>
    /// 逐帧推进已绑定的可执行子树。快照只记录进度，不记录执行委托或上下文。
    /// </summary>
    public sealed class TriggerExecutionScheduler : ITriggerExecutionScheduler
    {
        private sealed class Entry
        {
            public readonly long Id;
            public readonly EScheduleMode Mode;
            public readonly double IntervalMs;
            public readonly int MaxExecutions;
            public readonly bool CanBeInterrupted;
            public readonly Func<TriggerPlanExecutionResult> Execute;
            public EntryProgress Progress;

            public Entry(long id, EScheduleMode mode, double intervalMs, int maxExecutions, bool canBeInterrupted,
                Func<TriggerPlanExecutionResult> execute)
            {
                Id = id;
                Mode = mode;
                IntervalMs = intervalMs;
                MaxExecutions = maxExecutions;
                CanBeInterrupted = canBeInterrupted;
                Execute = execute;
                Progress.State = TriggerScheduledExecutionState.Waiting;
            }
        }

        private struct EntryProgress
        {
            public TriggerScheduledExecutionState State;
            public double ElapsedMs;
            public double LastExecutionMs;
            public int ExecutionCount;
            public string FailureReason;
        }

        private readonly Dictionary<long, Entry> _definitions = new Dictionary<long, Entry>();
        private readonly List<Entry> _ordered = new List<Entry>();
        private readonly bool _retainFinishedEntries;
        private long _nextId = 1L;
        private int _tickDepth;
        private int _externalExecutionDepth;

        public TriggerExecutionScheduler(bool retainFinishedEntries = false)
        {
            _retainFinishedEntries = retainFinishedEntries;
        }

        public int ActiveCount { get; private set; }
        public int DefinitionCount => _definitions.Count;
        public long NextExecutionId => _nextId;

        public TriggerScheduledExecutionHandle Schedule<TCtx>(
            ITriggerPlanExecutable executable,
            object args,
            in ExecCtx<TCtx> context,
            EScheduleMode mode,
            float intervalMs,
            int maxExecutions,
            bool canBeInterrupted)
            where TCtx : class
        {
            if (executable == null) throw new ArgumentNullException(nameof(executable));
            if (mode == EScheduleMode.Transient) throw new ArgumentException("Transient nodes execute immediately and must not be scheduled.", nameof(mode));
            if (intervalMs < 0f) throw new ArgumentOutOfRangeException(nameof(intervalMs));
            if (maxExecutions == 0 || maxExecutions < -1) throw new ArgumentOutOfRangeException(nameof(maxExecutions));

            var capturedContext = context;
            var id = _nextId++;
            if (_nextId <= 0L) _nextId = 1L;
            var entry = new Entry(id, mode, intervalMs, maxExecutions, canBeInterrupted,
                () => executable.Execute(args, in capturedContext));
            _definitions.Add(id, entry);
            _ordered.Add(entry);
            ActiveCount++;
            return new TriggerScheduledExecutionHandle(id);
        }

        public void Tick(float deltaTimeMs)
        {
            var delta = Math.Max(0d, deltaTimeMs);
            _tickDepth++;
            try
            {
                for (var index = 0; index < _ordered.Count; index++)
                {
                    var entry = _ordered[index];
                    if (entry.Progress.State != TriggerScheduledExecutionState.Waiting) continue;

                    entry.Progress.ElapsedMs += delta;
                    if (!IsDue(entry)) continue;

                    try
                    {
                        var result = entry.Execute();
                        if (result.IsFailed)
                        {
                            Complete(entry, TriggerScheduledExecutionState.Failed, result.Reason);
                            continue;
                        }

                        entry.Progress.ExecutionCount++;
                        entry.Progress.LastExecutionMs = entry.Progress.ElapsedMs;
                        if (ShouldComplete(entry)) Complete(entry, TriggerScheduledExecutionState.Completed, null);
                    }
                    catch (Exception ex)
                    {
                        Complete(entry, TriggerScheduledExecutionState.Failed, ex.Message);
                    }
                }
            }
            finally
            {
                _tickDepth--;
                if (_tickDepth == 0 && !_retainFinishedEntries) PruneFinishedEntries();
            }
        }

        public bool ExecuteExternal(in TriggerScheduledExecutionHandle handle)
        {
            if (!TryGetWaiting(handle, out var entry) || entry.Mode != EScheduleMode.External) return false;
            _externalExecutionDepth++;
            try
            {
                var result = entry.Execute();
                if (result.IsFailed)
                {
                    Complete(entry, TriggerScheduledExecutionState.Failed, result.Reason);
                    if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
                    return false;
                }

                entry.Progress.ExecutionCount++;
                entry.Progress.LastExecutionMs = entry.Progress.ElapsedMs;
                if (entry.MaxExecutions > 0 && entry.Progress.ExecutionCount >= entry.MaxExecutions)
                {
                    Complete(entry, TriggerScheduledExecutionState.Completed, null);
                    if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
                }
                return true;
            }
            catch (Exception ex)
            {
                Complete(entry, TriggerScheduledExecutionState.Failed, ex.Message);
                if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
                return false;
            }
            finally
            {
                _externalExecutionDepth--;
            }
        }

        public bool Interrupt(in TriggerScheduledExecutionHandle handle, string reason = null)
        {
            if (!TryGetWaiting(handle, out var entry) || !entry.CanBeInterrupted) return false;
            Complete(entry, TriggerScheduledExecutionState.Interrupted, reason);
            if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
            return true;
        }

        public TriggerScheduledExecutionInfo[] GetExecutions()
        {
            var entries = new TriggerScheduledExecutionInfo[_ordered.Count];
            for (var index = 0; index < _ordered.Count; index++)
            {
                var entry = _ordered[index];
                entries[index] = new TriggerScheduledExecutionInfo(
                    entry.Id,
                    entry.Progress.State,
                    entry.Progress.ElapsedMs,
                    entry.Progress.LastExecutionMs,
                    entry.Progress.ExecutionCount,
                    entry.Progress.FailureReason);
            }

            return entries;
        }

        public bool ContainsDefinition(long id) => _definitions.ContainsKey(id);

        /// <summary>
        /// 在帧边界捕获独立进度，包括保留的终态和 ID 游标，不捕获执行绑定。
        /// </summary>
        public TriggerExecutionProgress CaptureProgress()
        {
            RequireFrameBoundary();
            return new TriggerExecutionProgress(_nextId, GetExecutions());
        }

        /// <summary>
        /// 只读校验游标、进度和原地定义是否存在，不调用可执行对象。
        /// 调用方必须保证目标来自同一有效历史，ID 命中不证明定义没有跨分支复用。
        /// </summary>
        public void ValidateProgress(in TriggerExecutionProgress progress) => ValidateProgressEntries(in progress);

        /// <summary>
        /// 在帧边界按已验证的值静默恢复。保留项不改变现有相对调度顺序，未列出的项被移除。
        /// 校验失败时不写入；不重建缺失定义，不重放历史执行，也不改变终态留存选项。
        /// 调用方不得使用已失效分支的快照，也不得从执行回调调用。
        /// </summary>
        public void RestoreProgress(in TriggerExecutionProgress progress)
        {
            var retained = ValidateProgressEntries(in progress);
            foreach (var item in progress.Entries)
                AssignProgress(_definitions[item.Id], in item);

            var destination = 0;
            var activeCount = 0;
            for (var index = 0; index < _ordered.Count; index++)
            {
                var entry = _ordered[index];
                var waiting = entry.Progress.State == TriggerScheduledExecutionState.Waiting;
                if (!retained.Contains(entry.Id) || (!_retainFinishedEntries && !waiting))
                {
                    _definitions.Remove(entry.Id);
                    continue;
                }

                _ordered[destination++] = entry;
                if (waiting) activeCount++;
            }

            _ordered.RemoveRange(destination, _ordered.Count - destination);
            _nextId = progress.NextExecutionId;
            ActiveCount = activeCount;
        }

        private HashSet<long> ValidateProgressEntries(in TriggerExecutionProgress progress)
        {
            RequireFrameBoundary();
            if (progress.NextExecutionId <= 0 || progress.Entries == null)
                throw new InvalidOperationException("Invalid scheduled execution progress or ID cursor.");

            var ids = new HashSet<long>();
            foreach (var item in progress.Entries)
            {
                if (item.Id <= 0 || item.Id >= progress.NextExecutionId || !ids.Add(item.Id) ||
                    !_definitions.ContainsKey(item.Id) || !IsValidProgress(in item))
                    throw new InvalidOperationException(
                        $"Scheduled execution {item.Id} cannot be restored without valid progress and its in-process definition.");
            }

            return ids;
        }

        private void RequireFrameBoundary()
        {
            if (_tickDepth != 0 || _externalExecutionDepth != 0)
                throw new InvalidOperationException("Scheduled execution progress requires a frame boundary, outside execution callbacks.");
        }

        public void SetExecutionProgress(in TriggerScheduledExecutionInfo progress)
        {
            if (!_definitions.TryGetValue(progress.Id, out var entry))
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (!IsValidProgress(in progress))
                throw new ArgumentOutOfRangeException(nameof(progress));
            AssignProgress(entry, in progress);
            RecountActive();
            if (!_retainFinishedEntries && _tickDepth == 0 &&
                progress.State != TriggerScheduledExecutionState.Waiting)
                Remove(progress.Id);
        }

        private static bool IsValidProgress(in TriggerScheduledExecutionInfo progress) =>
            progress.State >= TriggerScheduledExecutionState.Waiting &&
            progress.State <= TriggerScheduledExecutionState.Failed &&
            progress.ElapsedMs >= 0 && !double.IsInfinity(progress.ElapsedMs) &&
            progress.LastExecutionMs >= 0 && !double.IsInfinity(progress.LastExecutionMs) &&
            progress.ExecutionCount >= 0;

        private static void AssignProgress(Entry entry, in TriggerScheduledExecutionInfo progress)
        {
            entry.Progress.State = progress.State;
            entry.Progress.ElapsedMs = progress.ElapsedMs;
            entry.Progress.LastExecutionMs = progress.LastExecutionMs;
            entry.Progress.ExecutionCount = progress.ExecutionCount;
            entry.Progress.FailureReason = progress.FailureReason;
        }

        public bool Remove(long id)
        {
            if (_tickDepth != 0) throw new InvalidOperationException("Cannot remove an execution during a scheduler tick.");
            if (!_definitions.TryGetValue(id, out var entry)) return false;
            _definitions.Remove(id);
            _ordered.Remove(entry);
            if (entry.Progress.State == TriggerScheduledExecutionState.Waiting) ActiveCount--;
            return true;
        }

        public void SetNextExecutionId(long nextId)
        {
            if (nextId <= 0) throw new ArgumentOutOfRangeException(nameof(nextId));
            for (var index = 0; index < _ordered.Count; index++)
                if (_ordered[index].Id >= nextId) throw new ArgumentOutOfRangeException(nameof(nextId));
            _nextId = nextId;
        }

        public void Clear()
        {
            _definitions.Clear();
            _ordered.Clear();
            _nextId = 1L;
            ActiveCount = 0;
        }

        private static bool IsDue(Entry entry)
        {
            switch (entry.Mode)
            {
                case EScheduleMode.Timed:
                    return entry.Progress.ElapsedMs >= entry.IntervalMs;
                case EScheduleMode.Periodic:
                case EScheduleMode.Continuous:
                case EScheduleMode.Conditional:
                    return entry.Progress.ExecutionCount == 0
                        ? entry.Progress.ElapsedMs >= entry.IntervalMs
                        : entry.Progress.ElapsedMs - entry.Progress.LastExecutionMs >= entry.IntervalMs;
                case EScheduleMode.External:
                    return false;
                default:
                    return false;
            }
        }

        private static bool ShouldComplete(Entry entry)
        {
            if (entry.Mode == EScheduleMode.Timed) return true;
            return entry.MaxExecutions > 0 && entry.Progress.ExecutionCount >= entry.MaxExecutions;
        }

        private bool TryGetWaiting(in TriggerScheduledExecutionHandle handle, out Entry entry)
        {
            entry = null;
            return handle.IsValid && _definitions.TryGetValue(handle.Id, out entry) && entry.Progress.State == TriggerScheduledExecutionState.Waiting;
        }

        private void Complete(Entry entry, TriggerScheduledExecutionState state, string reason)
        {
            if (entry.Progress.State == TriggerScheduledExecutionState.Waiting && ActiveCount > 0) ActiveCount--;
            entry.Progress.State = state;
            entry.Progress.FailureReason = reason ?? string.Empty;
        }

        private void RecountActive()
        {
            ActiveCount = 0;
            for (var index = 0; index < _ordered.Count; index++)
            {
                if (_ordered[index].Progress.State == TriggerScheduledExecutionState.Waiting) ActiveCount++;
            }
        }

        private void PruneFinishedEntries()
        {
            for (var index = _ordered.Count - 1; index >= 0; index--)
            {
                if (_ordered[index].Progress.State == TriggerScheduledExecutionState.Waiting) continue;
                _definitions.Remove(_ordered[index].Id);
                _ordered.RemoveAt(index);
            }
        }
    }
}
