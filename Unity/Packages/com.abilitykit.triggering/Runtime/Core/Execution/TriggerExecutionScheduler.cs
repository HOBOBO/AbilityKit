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

    public readonly struct TriggerScheduledExecutionSnapshot
    {
        public TriggerScheduledExecutionSnapshot(
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

    public readonly struct TriggerExecutionSchedulerSnapshot
    {
        public TriggerExecutionSchedulerSnapshot(long nextId, TriggerScheduledExecutionSnapshot[] entries)
        {
            NextId = nextId;
            Entries = entries ?? Array.Empty<TriggerScheduledExecutionSnapshot>();
        }

        public long NextId { get; }
        public TriggerScheduledExecutionSnapshot[] Entries { get; }
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
    /// Frame-driven scheduler for executable subtrees. Definitions are retained until
    /// Clear so an in-process rollback can reactivate an instance completed after the checkpoint.
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
        private long _nextId = 1L;

        public int ActiveCount { get; private set; }
        public int DefinitionCount => _definitions.Count;

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

        public bool ExecuteExternal(in TriggerScheduledExecutionHandle handle)
        {
            if (!TryGetWaiting(handle, out var entry) || entry.Mode != EScheduleMode.External) return false;
            try
            {
                var result = entry.Execute();
                if (result.IsFailed)
                {
                    Complete(entry, TriggerScheduledExecutionState.Failed, result.Reason);
                    return false;
                }

                entry.ExecutionCount++;
                entry.LastExecutionMs = entry.ElapsedMs;
                if (entry.MaxExecutions > 0 && entry.ExecutionCount >= entry.MaxExecutions)
                    Complete(entry, TriggerScheduledExecutionState.Completed, null);
                return true;
            }
            catch (Exception ex)
            {
                Complete(entry, TriggerScheduledExecutionState.Failed, ex.Message);
                return false;
            }
        }

        public bool Interrupt(in TriggerScheduledExecutionHandle handle, string reason = null)
        {
            if (!TryGetWaiting(handle, out var entry) || !entry.CanBeInterrupted) return false;
            Complete(entry, TriggerScheduledExecutionState.Interrupted, reason);
            return true;
        }

        public TriggerExecutionSchedulerSnapshot CaptureSnapshot()
        {
            var entries = new TriggerScheduledExecutionSnapshot[_ordered.Count];
            for (var i = 0; i < _ordered.Count; i++)
            {
                var entry = _ordered[i];
                entries[i] = new TriggerScheduledExecutionSnapshot(
                    entry.Id,
                    entry.State,
                    entry.ElapsedMs,
                    entry.LastExecutionMs,
                    entry.ExecutionCount,
                    entry.FailureReason);
            }

            return new TriggerExecutionSchedulerSnapshot(_nextId, entries);
        }

        public void RestoreSnapshot(in TriggerExecutionSchedulerSnapshot snapshot)
        {
            var states = snapshot.Entries ?? Array.Empty<TriggerScheduledExecutionSnapshot>();
            var restoredIds = new HashSet<long>();
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (!_definitions.TryGetValue(state.Id, out var entry))
                    throw new InvalidOperationException($"Cannot restore scheduled execution '{state.Id}' because its executable definition is unavailable.");
                restoredIds.Add(state.Id);
                entry.State = state.State;
                entry.ElapsedMs = Math.Max(0d, state.ElapsedMs);
                entry.LastExecutionMs = Math.Max(0d, state.LastExecutionMs);
                entry.ExecutionCount = Math.Max(0, state.ExecutionCount);
                entry.FailureReason = state.FailureReason;
            }

            // Definitions created after the checkpoint must be removed. Replay is allowed to
            // allocate their IDs again after NextId is restored.
            for (var i = _ordered.Count - 1; i >= 0; i--)
            {
                var entry = _ordered[i];
                if (!restoredIds.Contains(entry.Id))
                {
                    _ordered.RemoveAt(i);
                    _definitions.Remove(entry.Id);
                }
            }

            _nextId = Math.Max(1L, snapshot.NextId);
            RecountActive();
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
    }
}
