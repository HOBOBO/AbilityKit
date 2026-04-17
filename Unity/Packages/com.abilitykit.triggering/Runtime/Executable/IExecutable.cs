using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

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

    // ========================================================================
    // 条件接口
    // ========================================================================

    /// <summary>
    /// 条件评估结果
    /// </summary>
    public readonly struct ConditionResult
    {
        public bool Passed { get; }
        public string FailureReason { get; }

        public static ConditionResult Pass => new(true, null);
        public static ConditionResult Fail(string reason = null) => new(false, reason);

        private ConditionResult(bool passed, string failureReason)
        {
            Passed = passed;
            FailureReason = failureReason;
        }
    }

    /// <summary>
    /// 条件接口
    /// </summary>
    public interface ICondition
    {
        string Name { get; }
        ConditionResult Evaluate(object ctx);
    }

    /// <summary>
    /// 组合条件
    /// </summary>
    public sealed class MultiCondition : ICondition
    {
        public string Name => "MultiCondition";
        public Config.EConditionCombinator Combinator { get; set; } = Config.EConditionCombinator.And;
        public List<ICondition> Conditions { get; set; } = new List<ICondition>();

        public ConditionResult Evaluate(object ctx)
        {
            if (Conditions.Count == 0) return ConditionResult.Pass;

            bool allPassed = true;
            foreach (var condition in Conditions)
            {
                var result = condition?.Evaluate(ctx) ?? ConditionResult.Pass;
                if (Combinator == EConditionCombinator.And)
                {
                    if (!result.Passed) return result;
                }
                else if (Combinator == EConditionCombinator.Or)
                {
                    if (result.Passed) return result;
                    allPassed = false;
                }
                else
                {
                    if (result.Passed)
                    {
                        if (allPassed) return ConditionResult.Fail("Multiple conditions passed in XOR");
                        allPassed = true;
                    }
                }
            }

            return Combinator switch
            {
                Config.EConditionCombinator.And => ConditionResult.Pass,
                Config.EConditionCombinator.Or => allPassed ? ConditionResult.Fail("No condition passed") : ConditionResult.Pass,
                _ => ConditionResult.Pass
            };
        }
    }

    /// <summary>
    /// 取反条件
    /// </summary>
    public sealed class NotCondition : ICondition
    {
        public string Name => "Not";
        public ICondition Inner { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var inner = Inner?.Evaluate(ctx) ?? ConditionResult.Pass;
            return inner.Passed ? ConditionResult.Fail("Inner condition passed") : ConditionResult.Pass;
        }
    }

    /// <summary>
    /// And 条件
    /// </summary>
    public sealed class AndCondition : ICondition
    {
        public string Name => "And";
        public ICondition Left { get; set; }
        public ICondition Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var leftResult = Left?.Evaluate(ctx) ?? ConditionResult.Pass;
            if (!leftResult.Passed) return leftResult;
            return Right?.Evaluate(ctx) ?? ConditionResult.Pass;
        }
    }

    /// <summary>
    /// Or 条件
    /// </summary>
    public sealed class OrCondition : ICondition
    {
        public string Name => "Or";
        public ICondition Left { get; set; }
        public ICondition Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var leftResult = Left?.Evaluate(ctx) ?? ConditionResult.Pass;
            if (leftResult.Passed) return leftResult;
            return Right?.Evaluate(ctx) ?? ConditionResult.Fail("No condition passed");
        }
    }

    /// <summary>
    /// 数值比较条件
    /// </summary>
    public sealed class NumericCompareCondition : ICondition
    {
        public string Name => "NumericCompare";
        public ECompareOp Op { get; set; }
        public NumericValueRef Left { get; set; }
        public NumericValueRef Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var left = Left.Resolve(ctx);
            var right = Right.Resolve(ctx);
            return Op switch
            {
                ECompareOp.GreaterThan => left > right ? ConditionResult.Pass : ConditionResult.Fail($"left {left} not > right {right}"),
                ECompareOp.GreaterThanOrEqual => left >= right ? ConditionResult.Pass : ConditionResult.Fail($"left {left} not >= right {right}"),
                ECompareOp.LessThan => left < right ? ConditionResult.Pass : ConditionResult.Fail($"left {left} not < right {right}"),
                ECompareOp.LessThanOrEqual => left <= right ? ConditionResult.Pass : ConditionResult.Fail($"left {left} not <= right {right}"),
                ECompareOp.Equal => Math.Abs(left - right) < 0.0001 ? ConditionResult.Pass : ConditionResult.Fail($"left {left} not == right {right}"),
                ECompareOp.NotEqual => Math.Abs(left - right) >= 0.0001 ? ConditionResult.Pass : ConditionResult.Fail($"left {left} == right {right}"),
                _ => ConditionResult.Fail($"Unknown op {Op}")
            };
        }
    }

    /// <summary>
    /// 载荷字段比较条件
    /// </summary>
    public sealed class PayloadCompareCondition : ICondition
    {
        public string Name => "PayloadCompare";
        public int FieldId { get; set; }
        public ECompareOp Op { get; set; }
        public NumericValueRef CompareValue { get; set; }
        public bool Negate { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var compareVal = CompareValue.Resolve(ctx);
            return Negate ? ConditionResult.Fail("Payload compare not implemented") : ConditionResult.Pass;
        }
    }

    /// <summary>
    /// 是否有目标条件
    /// </summary>
    public sealed class HasTargetCondition : ICondition
    {
        public string Name => "HasTarget";
        public bool Negate { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            // TODO: 实现目标检测逻辑
            return Negate ? ConditionResult.Fail("HasTarget not implemented") : ConditionResult.Pass;
        }
    }

    /// <summary>
    /// 常量条件
    /// </summary>
    public sealed class ConstCondition : ICondition
    {
        public string Name => "Const";
        public bool Value { get; set; } = true;

        public ConditionResult Evaluate(object ctx)
        {
            return Value ? ConditionResult.Pass : ConditionResult.Fail("Const is false");
        }
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
