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
    /// Frame-driven scheduler for executable subtrees.
    /// </summary>
    public sealed class TriggerExecutionScheduler : ITriggerExecutionScheduler
    {
        private sealed class Entry
        {
            public long Id;
            public EScheduleMode Mode;
            public double IntervalMs;
            public int MaxExecutions;
            public bool CanBeInterrupted;
            public TriggerScheduledExecutionState State;
            public double ElapsedMs;
            public double LastExecutionMs;
            public int ExecutionCount;
            public string FailureReason;
            public Func<TriggerPlanExecutionResult> Execute;
        }

        private readonly Dictionary<long, Entry> _definitions = new Dictionary<long, Entry>();
        private readonly List<Entry> _ordered = new List<Entry>();
        private readonly bool _retainFinishedEntries;
        private long _nextId = 1L;
        private int _tickDepth;

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
            var entry = new Entry
            {
                Id = id,
                Mode = mode,
                IntervalMs = intervalMs,
                MaxExecutions = maxExecutions,
                CanBeInterrupted = canBeInterrupted,
                State = TriggerScheduledExecutionState.Waiting,
                Execute = () => executable.Execute(args, in capturedContext),
            };
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
                for (var i = 0; i < _ordered.Count; i++)
                {
                    var entry = _ordered[i];
                    if (entry.State != TriggerScheduledExecutionState.Waiting) continue;

                    entry.ElapsedMs += delta;
                    if (!IsDue(entry)) continue;

                    try
                    {
                        var result = entry.Execute();
                        if (result.IsFailed)
                        {
                            Complete(entry, TriggerScheduledExecutionState.Failed, result.Reason);
                            continue;
                        }

                        entry.ExecutionCount++;
                        entry.LastExecutionMs = entry.ElapsedMs;
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
            try
            {
                var result = entry.Execute();
                if (result.IsFailed)
                {
                    Complete(entry, TriggerScheduledExecutionState.Failed, result.Reason);
                    if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
                    return false;
                }

                entry.ExecutionCount++;
                entry.LastExecutionMs = entry.ElapsedMs;
                if (entry.MaxExecutions > 0 && entry.ExecutionCount >= entry.MaxExecutions)
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
            for (var i = 0; i < _ordered.Count; i++)
            {
                var entry = _ordered[i];
                entries[i] = new TriggerScheduledExecutionInfo(
                    entry.Id,
                    entry.State,
                    entry.ElapsedMs,
                    entry.LastExecutionMs,
                    entry.ExecutionCount,
                    entry.FailureReason);
            }

            return entries;
        }

        public bool ContainsDefinition(long id) => _definitions.ContainsKey(id);

        public void SetExecutionProgress(in TriggerScheduledExecutionInfo progress)
        {
            if (!_definitions.TryGetValue(progress.Id, out var entry))
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (progress.State < TriggerScheduledExecutionState.Waiting ||
                progress.State > TriggerScheduledExecutionState.Failed ||
                progress.ElapsedMs < 0 || double.IsNaN(progress.ElapsedMs) || double.IsInfinity(progress.ElapsedMs) ||
                progress.LastExecutionMs < 0 || double.IsNaN(progress.LastExecutionMs) ||
                double.IsInfinity(progress.LastExecutionMs) || progress.ExecutionCount < 0)
                throw new ArgumentOutOfRangeException(nameof(progress));
            entry.State = progress.State;
            entry.ElapsedMs = progress.ElapsedMs;
            entry.LastExecutionMs = progress.LastExecutionMs;
            entry.ExecutionCount = progress.ExecutionCount;
            entry.FailureReason = progress.FailureReason;
            RecountActive();
            if (!_retainFinishedEntries && _tickDepth == 0 &&
                progress.State != TriggerScheduledExecutionState.Waiting)
                Remove(progress.Id);
        }

        public bool Remove(long id)
        {
            if (_tickDepth != 0) throw new InvalidOperationException("Cannot remove an execution during a scheduler tick.");
            if (!_definitions.TryGetValue(id, out var entry)) return false;
            _definitions.Remove(id);
            _ordered.Remove(entry);
            if (entry.State == TriggerScheduledExecutionState.Waiting) ActiveCount--;
            return true;
        }

        public void SetNextExecutionId(long nextId)
        {
            if (nextId <= 0) throw new ArgumentOutOfRangeException(nameof(nextId));
            for (var i = 0; i < _ordered.Count; i++)
                if (_ordered[i].Id >= nextId) throw new ArgumentOutOfRangeException(nameof(nextId));
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
                    return entry.ElapsedMs >= entry.IntervalMs;
                case EScheduleMode.Periodic:
                case EScheduleMode.Continuous:
                case EScheduleMode.Conditional:
                    return entry.ExecutionCount == 0
                        ? entry.ElapsedMs >= entry.IntervalMs
                        : entry.ElapsedMs - entry.LastExecutionMs >= entry.IntervalMs;
                case EScheduleMode.External:
                    return false;
                default:
                    return false;
            }
        }

        private static bool ShouldComplete(Entry entry)
        {
            if (entry.Mode == EScheduleMode.Timed) return true;
            return entry.MaxExecutions > 0 && entry.ExecutionCount >= entry.MaxExecutions;
        }

        private bool TryGetWaiting(in TriggerScheduledExecutionHandle handle, out Entry entry)
        {
            entry = null;
            return handle.IsValid && _definitions.TryGetValue(handle.Id, out entry) && entry.State == TriggerScheduledExecutionState.Waiting;
        }

        private void Complete(Entry entry, TriggerScheduledExecutionState state, string reason)
        {
            if (entry.State == TriggerScheduledExecutionState.Waiting && ActiveCount > 0) ActiveCount--;
            entry.State = state;
            entry.FailureReason = reason ?? string.Empty;
        }

        private void RecountActive()
        {
            ActiveCount = 0;
            for (var i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].State == TriggerScheduledExecutionState.Waiting) ActiveCount++;
            }
        }

        private void PruneFinishedEntries()
        {
            for (var i = _ordered.Count - 1; i >= 0; i--)
            {
                if (_ordered[i].State == TriggerScheduledExecutionState.Waiting) continue;
                _definitions.Remove(_ordered[i].Id);
                _ordered.RemoveAt(i);
            }
        }
    }
}
