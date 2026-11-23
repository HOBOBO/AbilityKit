using System;

namespace AbilityKit.Ability.StateSync.Prediction.Reconciliation
{
    /// <summary>
    /// 绑定会话、世界、状态流、实体生命周期、控制者和输入代际；不从帧号推断身份。
    /// </summary>
    internal readonly struct ReconciliationScope
    {
        private readonly (long Session, ulong World, Guid Stream, int Entity, long Lifetime, int Controller, long Epoch) _key;

        internal ReconciliationScope(long session, ulong world, Guid stream, int entity, long lifetime, int controller, long epoch)
        {
            _key = (session, world, stream, entity, lifetime, controller, epoch);
        }

        internal bool Matches(ReconciliationScope other) => _key.Equals(other._key);
    }

    /// <summary>
    /// 由宿主在导入和提交后交付的权威移动值；修订号只在所属作用域内比较。
    /// </summary>
    internal readonly struct CommittedMovement<TState> where TState : unmanaged
    {
        internal readonly ReconciliationScope Scope;
        internal readonly long Revision;
        internal readonly int Tick;
        internal readonly TState State;
        internal readonly bool IsFullBaseline;

        internal CommittedMovement(ReconciliationScope scope, long revision, int tick, TState state, bool isFullBaseline = false)
        {
            Scope = scope;
            Revision = revision;
            Tick = tick;
            State = state;
            IsFullBaseline = isFullBaseline;
        }
    }

    /// <summary>
    /// 同一权威截面的连续输入终结证明。宿主保证前缀内均已生效或终局拒绝，且前缀后没有已反映的移动效果。
    /// </summary>
    internal readonly struct InputTerminalProof
    {
        private readonly ReconciliationScope _scope;
        private readonly long _revision;
        private readonly int _tick;
        internal readonly long ThroughSequence;

        internal InputTerminalProof(ReconciliationScope scope, long revision, int tick, long throughSequence)
        {
            _scope = scope;
            _revision = revision;
            _tick = tick;
            ThroughSequence = throughSequence;
        }

        internal bool Matches(ReconciliationScope scope, long revision, int tick) =>
            _revision == revision && _tick == tick && _scope.Matches(scope);
    }

    /// <summary>
    /// 同步步进隔离候选。输入视图仅在本次调用有效，不得重入对账器或触发外部副作用。
    /// 状态和命令必须是自包含的值，不能通过句柄引用可写外部对象；依赖不足时返回假。
    /// </summary>
    internal delegate bool TryMovementStep<TState, TInput>(int tick, ReadOnlySpan<TInput> inputs, ref TState candidate)
        where TState : unmanaged where TInput : unmanaged;

    /// <summary>
    /// 最近一次对账的结果，仅供本地诊断，不控制会话的成功提交。
    /// </summary>
    internal enum ReconciliationStatus
    {
        /// <summary>
        /// 忽略无效、陈旧或不属于当前作用域的提交。
        /// </summary>
        Ignored,
        /// <summary>
        /// 隔离预测候选已完整步进并发布。
        /// </summary>
        Predicted,
        /// <summary>
        /// 权威锚点已足够新，没有需要推进的 Tick。
        /// </summary>
        AuthoritativeOnly,
        /// <summary>
        /// 缺少与该提交绑定且未倒退的终结证明。
        /// </summary>
        InvalidTerminalProof,
        /// <summary>
        /// 仍待终结的输入原计划 Tick 不晚于权威锚点。
        /// </summary>
        PendingInputBeforeAnchor,
        /// <summary>
        /// 输入容量不足或封存后到达的输入尚未被锚点和水位覆盖。
        /// </summary>
        UncoveredInput,
        /// <summary>
        /// 目标与锚点之间的 Tick 数超过单次预算。
        /// </summary>
        ReplayLimitExceeded,
        /// <summary>
        /// 拟重演区间存在未封存或已淘汰的 Tick。
        /// </summary>
        MissingTickCoverage,
        /// <summary>
        /// 步进返回失败或抛异常，没有发布部分预测结果。
        /// </summary>
        StepFailed,
        /// <summary>
        /// 已知权威基线失效，只等待当前作用域成功提交全量。
        /// </summary>
        WaitingForFullBaseline
    }

    /// <summary>
    /// 包含对账原因和实际调用步进的次数，失败的最后一次调用也计入。
    /// </summary>
    internal readonly struct ReconciliationResult
    {
        internal readonly ReconciliationStatus Status;
        internal readonly int AttemptedTicks;
        internal readonly Exception StepException;

        internal ReconciliationResult(ReconciliationStatus status, int attemptedTicks = 0, Exception stepException = null)
        {
            Status = status;
            AttemptedTicks = attemptedTicks;
            StepException = stepException;
        }
    }
}
