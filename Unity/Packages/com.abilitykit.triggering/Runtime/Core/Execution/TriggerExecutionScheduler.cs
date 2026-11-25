using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 执行树调度条目的本地标识；不包含调度器身份或历史分支信息。
    /// </summary>
    public readonly struct TriggerScheduledExecutionHandle : IEquatable<TriggerScheduledExecutionHandle>
    {
        /// <summary>
        /// 以给定 ID 构造句柄，不查询调度器中的条目。
        /// </summary>
        public TriggerScheduledExecutionHandle(long id) => Id = id;

        /// <summary>
        /// 调度器分配的条目标识。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// ID 是否为正数；不表示该定义仍然存在。
        /// </summary>
        public bool IsValid => Id > 0L;

        /// <summary>
        /// 只比较 ID，不比较调度器或历史分支。
        /// </summary>
        public bool Equals(TriggerScheduledExecutionHandle other) => Id == other.Id;

        /// <summary>
        /// 判断对象是否为相同 ID 的调度句柄。
        /// </summary>
        public override bool Equals(object obj) => obj is TriggerScheduledExecutionHandle other && Equals(other);

        /// <summary>
        /// 返回 ID 的哈希值。
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// 输出有效 ID 或无效标记，供诊断使用。
        /// </summary>
        public override string ToString() => IsValid ? Id.ToString() : "Invalid";
    }

    /// <summary>
    /// 执行树调度的等待状态和终态。
    /// </summary>
    public enum TriggerScheduledExecutionState : byte
    {
        /// <summary>
        /// 任务可继续推进或等待外部触发。
        /// </summary>
        Waiting = 0,

        /// <summary>
        /// 任务已按调度方式或次数上限完成。
        /// </summary>
        Completed = 1,

        /// <summary>
        /// 等待中的任务已被中断。
        /// </summary>
        Interrupted = 2,

        /// <summary>
        /// 执行返回失败或抛出异常，任务终止。
        /// </summary>
        Failed = 3,
    }

    /// <summary>
    /// 单条任务的进度值，不包含执行定义或宿主引用。
    /// </summary>
    public readonly struct TriggerScheduledExecutionInfo
    {
        /// <summary>
        /// 构造待查询或恢复的进度值；不在此校验 ID、时间或状态。
        /// </summary>
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

        /// <summary>
        /// 进度所属的任务 ID。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// 当前等待状态或终态。
        /// </summary>
        public TriggerScheduledExecutionState State { get; }

        /// <summary>
        /// 等待期间累积的已推进毫秒数。
        /// </summary>
        public double ElapsedMs { get; }

        /// <summary>
        /// 最近一次计入执行次数时的累计毫秒数。
        /// </summary>
        public double LastExecutionMs { get; }

        /// <summary>
        /// 已计入的非失败执行次数。
        /// </summary>
        public int ExecutionCount { get; }

        /// <summary>
        /// 记录的失败或中断原因；未提供原因时构造器写为空字符串。
        /// </summary>
        public string FailureReason { get; }
    }

    /// <summary>
    /// 执行树节点请求登记子树的宿主接口，不负责应用的帧驱动或快照编解码。
    /// </summary>
    public interface ITriggerExecutionScheduler
    {
        /// <summary>
        /// 提交子树、载荷和上下文，以及调度方式、毫秒间隔、次数上限和中断选项。
        /// </summary>
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
    /// 由宿主逐帧推进已绑定的可执行子树；不读取系统时钟，也不负责领域对象恢复。
    /// 快照只记录进度，不记录执行委托或上下文；同一调度器应由宿主串行调用。
    /// </summary>
    public sealed class TriggerExecutionScheduler : ITriggerExecutionScheduler
    {
        /// <summary>
        /// 宿主内保留的任务定义与执行绑定；可变进度集中在独立字段。
        /// </summary>
        private sealed class Entry
        {
            public readonly long Id;
            public readonly EScheduleMode Mode;
            public readonly double IntervalMs;
            public readonly int MaxExecutions;
            public readonly bool CanBeInterrupted;
            public EntryProgress Progress;
            private readonly Func<TriggerPlanExecutionResult> _execute;

            public Entry(long id, EScheduleMode mode, float intervalMs, int maxExecutions, bool canBeInterrupted,
                Func<TriggerPlanExecutionResult> execute)
            {
                Id = id;
                Mode = mode;
                IntervalMs = intervalMs;
                MaxExecutions = maxExecutions;
                CanBeInterrupted = canBeInterrupted;
                _execute = execute;
                Progress.State = TriggerScheduledExecutionState.Waiting;
            }

            public TriggerPlanExecutionResult Execute() => _execute();
        }

        /// <summary>
        /// 随帧推进变化的状态，不持有动作或上下文。
        /// </summary>
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

        /// <summary>
        /// 创建空调度器；可保留终态定义，以供诊断及同宿主进度恢复。
        /// </summary>
        public TriggerExecutionScheduler(bool retainFinishedEntries = false)
        {
            _retainFinishedEntries = retainFinishedEntries;
        }

        /// <summary>
        /// 当前仍在等待的任务数量。
        /// </summary>
        public int ActiveCount { get; private set; }

        /// <summary>
        /// 当前保留的定义数量，包括尚未裁剪的终态。
        /// </summary>
        public int DefinitionCount => _definitions.Count;

        /// <summary>
        /// 下次登记使用的本地 ID，不具备跨宿主唯一性。
        /// </summary>
        public long NextExecutionId => _nextId;

        /// <summary>
        /// 注册可执行子树，保留安排时的上下文值及传入的对象引用，不复制对象内容。
        /// 即时节点不进入此队列；按间隔推进的任务以宿主传入的毫秒增量计时。
        /// </summary>
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
            return AddEntry(new Entry(AllocateExecutionId(), mode, intervalMs, maxExecutions, canBeInterrupted,
                () => executable.Execute(args, in capturedContext)));
        }

        private long AllocateExecutionId()
        {
            var id = _nextId++;
            if (_nextId <= 0L) _nextId = 1L;
            return id;
        }

        private TriggerScheduledExecutionHandle AddEntry(Entry entry)
        {
            _definitions.Add(entry.Id, entry);
            _ordered.Add(entry);
            ActiveCount++;
            return new TriggerScheduledExecutionHandle(entry.Id);
        }

        /// <summary>
        /// 按条目顺序累加宿主提供的毫秒增量，负增量按零处理。
        /// 每次遍历对到期条目至多派发一次，不补跑跨过的周期；外部触发条目不自动派发。
        /// 动作失败或抛出异常时记录失败终态，并继续推进后续条目。
        /// </summary>
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

        /// <summary>
        /// 推进一个仍在等待的外部触发任务；失败时记录终态并返回否。
        /// </summary>
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

        /// <summary>
        /// 将可中断的等待项置为中断终态，不调用子树；没有匹配项时返回否。
        /// </summary>
        public bool Interrupt(in TriggerScheduledExecutionHandle handle, string reason = null)
        {
            if (!TryGetWaiting(handle, out var entry) || !entry.CanBeInterrupted) return false;
            Complete(entry, TriggerScheduledExecutionState.Interrupted, reason);
            if (!_retainFinishedEntries && _tickDepth == 0) Remove(entry.Id);
            return true;
        }

        /// <summary>
        /// 按当前队列顺序复制进度值，包含仍保留的终态，不包含 ID 游标或执行定义。
        /// </summary>
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

        /// <summary>
        /// 检查本地 ID 的定义是否留存，不校验定义所属的历史分支。
        /// </summary>
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
        /// 在帧边界只读校验游标、进度和原地定义是否存在，不调用可执行对象。
        /// 调用方必须保证目标来自同一有效历史，ID 命中不证明定义没有跨分支复用。
        /// </summary>
        public void ValidateProgress(in TriggerExecutionProgress progress) => PreflightProgress(in progress);

        /// <summary>
        /// 在帧边界重新校验后静默恢复。保留项不改变现有相对调度顺序，未列出的项被移除。
        /// 校验失败时不写入；不重建缺失定义，不重放历史执行，也不改变终态留存选项。
        /// 调用方不得使用已失效分支的快照，也不得从执行回调调用。
        /// </summary>
        public void RestoreProgress(in TriggerExecutionProgress progress)
        {
            var retained = PreflightProgress(in progress);
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

        private HashSet<long> PreflightProgress(in TriggerExecutionProgress progress)
        {
            RequireFrameBoundary();
            return TriggerExecutionProgressValidator.ValidateSnapshot(in progress, _definitions);
        }

        private void RequireFrameBoundary()
        {
            if (_tickDepth != 0 || _externalExecutionDepth != 0)
                throw new InvalidOperationException("Scheduled execution progress requires a frame boundary, outside execution callbacks.");
        }

        /// <summary>
        /// 静默修改单条已存在定义的进度并重计活动数量，不更新 ID 游标。
        /// 无效进度或缺失定义会被拒绝；帧快照恢复使用 <see cref="RestoreProgress"/>。
        /// </summary>
        public void SetExecutionProgress(in TriggerScheduledExecutionInfo progress)
        {
            if (!_definitions.TryGetValue(progress.Id, out var entry))
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (!TriggerExecutionProgressValidator.IsValidEntry(in progress))
                throw new ArgumentOutOfRangeException(nameof(progress));
            AssignProgress(entry, in progress);
            RecountActive();
            if (!_retainFinishedEntries && _tickDepth == 0 &&
                progress.State != TriggerScheduledExecutionState.Waiting)
                Remove(progress.Id);
        }

        private static void AssignProgress(Entry entry, in TriggerScheduledExecutionInfo progress)
        {
            entry.Progress.State = progress.State;
            entry.Progress.ElapsedMs = progress.ElapsedMs;
            entry.Progress.LastExecutionMs = progress.LastExecutionMs;
            entry.Progress.ExecutionCount = progress.ExecutionCount;
            entry.Progress.FailureReason = progress.FailureReason;
        }

        /// <summary>
        /// 直接移除已登记定义，不调用子树；不存在时返回否，逐帧推进期间拒绝移除。
        /// </summary>
        public bool Remove(long id)
        {
            if (_tickDepth != 0) throw new InvalidOperationException("Cannot remove an execution during a scheduler tick.");
            if (!_definitions.TryGetValue(id, out var entry)) return false;
            _definitions.Remove(id);
            _ordered.Remove(entry);
            if (entry.Progress.State == TriggerScheduledExecutionState.Waiting) ActiveCount--;
            return true;
        }

        /// <summary>
        /// 设置本地分配游标；新值必须为正并大于所有保留定义的 ID。
        /// </summary>
        public void SetNextExecutionId(long nextId)
        {
            if (nextId <= 0) throw new ArgumentOutOfRangeException(nameof(nextId));
            for (var index = 0; index < _ordered.Count; index++)
                if (_ordered[index].Id >= nextId) throw new ArgumentOutOfRangeException(nameof(nextId));
            _nextId = nextId;
        }

        /// <summary>
        /// 静默清除全部定义和进度，并将分配游标重置为一。
        /// </summary>
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
