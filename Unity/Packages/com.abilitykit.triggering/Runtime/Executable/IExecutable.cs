using System;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 执行状态与结果
    // ========================================================================

    /// <summary>
    /// 行为执行状态
    /// </summary>
    public enum EExecutionStatus : byte
    {
        Success = 0,
        Skipped = 1,
        Failed = 2,
    }

    /// <summary>
    /// 行为执行结果
    /// </summary>
    public readonly struct ExecutionResult
    {
        public readonly EExecutionStatus Status;
        public readonly int ExecutedCount;
        public readonly string FailureReason;

        public bool IsSuccess => Status == EExecutionStatus.Success;
        public bool IsSkipped => Status == EExecutionStatus.Skipped;
        public bool IsFailed => Status == EExecutionStatus.Failed;

        public static ExecutionResult Success(int executedCount = 1)
            => new(EExecutionStatus.Success, executedCount, null);

        public static ExecutionResult Skipped(string reason = null)
            => new(EExecutionStatus.Skipped, 0, reason);

        public static ExecutionResult Failed(string reason)
            => new(EExecutionStatus.Failed, 0, reason);

        public static ExecutionResult None => new(EExecutionStatus.Success, 0, null);

        private ExecutionResult(EExecutionStatus status, int executedCount, string failureReason)
        {
            Status = status;
            ExecutedCount = executedCount;
            FailureReason = failureReason;
        }

        public ExecutionResult Merge(ExecutionResult other)
        {
            if (other.IsFailed) return other;
            if (IsFailed) return this;
            if (other.IsSkipped) return this;
            if (IsSkipped) return other;
            return new ExecutionResult(EExecutionStatus.Success, ExecutedCount + other.ExecutedCount, null);
        }
    }

    /// <summary>
    /// 行为元数据
    /// </summary>
    public readonly struct ExecutableMetadata
    {
        public readonly int TypeId;
        public readonly string TypeName;
        public readonly bool IsComposite;
        public readonly bool IsScheduled;
        public readonly float? DefaultDurationMs;
        public readonly float? DefaultPeriodMs;

        public ExecutableMetadata(
            int typeId,
            string typeName,
            bool isComposite = false,
            bool isScheduled = false,
            float? defaultDurationMs = null,
            float? defaultPeriodMs = null)
        {
            TypeId = typeId;
            TypeName = typeName;
            IsComposite = isComposite;
            IsScheduled = isScheduled;
            DefaultDurationMs = defaultDurationMs;
            DefaultPeriodMs = defaultPeriodMs;
        }
    }

    // ========================================================================
    // 核心行为接口
    // ========================================================================

    /// <summary>
    /// 行为接口 (所有行为的基础)
    /// </summary>
    public interface IExecutable
    {
        string Name { get; }
        ExecutableMetadata Metadata { get; }
        ExecutionResult Execute(object ctx);
    }

    /// <summary>
    /// 原子行为接口 (不可再分的最小执行单元)
    /// </summary>
    public interface IAtomicExecutable : IExecutable
    {
        // 原子行为 Execute() 一次性完成，立即返回结果
    }

    /// <summary>
    /// 组合行为接口 (包含子节点的行为)
    /// </summary>
    public interface ICompositeExecutable : IExecutable
    {
        int ChildCount { get; }
        ISimpleExecutable GetChild(int index);
    }

    /// <summary>
    /// 简单行为标记接口 (瞬时执行的叶子节点或组合)
    /// </summary>
    public interface ISimpleExecutable : IExecutable
    {
    }

    // ========================================================================
    // 复合执行模式
    // ========================================================================

    /// <summary>
    /// 复合执行模式
    /// </summary>
    public enum ECompositeMode
    {
        /// <summary>顺序执行，遇到失败停止</summary>
        Sequence,
        /// <summary>选择第一个成功的</summary>
        Selector,
        /// <summary>并行执行，等待全部完成</summary>
        Parallel,
        /// <summary>并行执行，任一成功即成功</summary>
        ParallelSelector,
        /// <summary>并行执行，任一失败即失败</summary>
        ParallelSequence,
    }

    /// <summary>
    /// 顺序执行组合器
    /// </summary>
    public interface ISequenceExecutable : ICompositeExecutable
    {
        // 语义: 顺序执行子节点，任一失败则整体失败
    }

    /// <summary>
    /// 选择执行组合器
    /// </summary>
    public interface ISelectorExecutable : ICompositeExecutable
    {
        // 语义: 选择第一个成功的子节点执行
    }

    /// <summary>
    /// 并行执行组合器
    /// </summary>
    public interface IParallelExecutable : ICompositeExecutable
    {
        ECompositeMode ParallelMode { get; }
        float TimeoutMs { get; set; }
    }

    /// <summary>
    /// 条件分支组合器
    /// </summary>
    public interface IConditionalExecutable : ICompositeExecutable
    {
        int EvaluateConditionIndex(object ctx);
    }

    /// <summary>
    /// Switch 分支组合器
    /// </summary>
    public interface ISwitchExecutable : ICompositeExecutable
    {
        Func<object, int> ValueSelector { get; set; }
    }

    /// <summary>
    /// 带有内部行为的接口（用于装饰器）
    /// 替代反射方式，提供类型安全的访问
    /// </summary>
    public interface IHasInner
    {
        ISimpleExecutable Inner { get; set; }
    }

    // ========================================================================
    // 调度模式
    // ========================================================================

    /// <summary>
    /// 调度行为接口
    /// </summary>
    public interface IScheduledExecutable : IExecutable, ISimpleExecutable
    {
        Config.EScheduleMode ScheduleMode { get; }
        bool IsPeriodic { get; }
        float PeriodMs { get; }
        float DurationMs { get; }
    }

    /// <summary>
    /// 调度控制器接口
    /// </summary>
    public interface IScheduleController
    {
        bool IsCompleted { get; }
        bool IsInterrupted { get; }
        string InterruptionReason { get; }
        void Update(float deltaTimeMs);
        void RequestInterrupt(string reason);
    }

    /// <summary>
    /// 空控制器
    /// </summary>
    public sealed class NullScheduleController : IScheduleController
    {
        public static readonly NullScheduleController Instance = new();

        public bool IsCompleted => true;
        public bool IsInterrupted => false;
        public string InterruptionReason => null;
        public void Update(float deltaTimeMs) { }
        public void RequestInterrupt(string reason) { }

        private NullScheduleController() { }
    }
}
