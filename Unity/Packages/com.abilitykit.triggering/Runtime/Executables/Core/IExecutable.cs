using System;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    public enum EExecutionStatus : byte
    {
        Success = 0,
        Skipped = 1,
        Failed = 2,
        Interrupted = 3
    }

    public readonly struct ExecutionResult
    {
        public readonly EExecutionStatus Status;
        public readonly int ExecutedCount;
        public readonly string FailureReason;

        public bool IsSuccess => Status == EExecutionStatus.Success;
        public bool IsSkipped => Status == EExecutionStatus.Skipped;
        public bool IsFailed => Status == EExecutionStatus.Failed;
        public bool IsInterrupted => Status == EExecutionStatus.Interrupted;

        public static ExecutionResult Success(int executedCount = 1)
            => new(EExecutionStatus.Success, executedCount, null);

        public static ExecutionResult Skipped(string reason = null)
            => new(EExecutionStatus.Skipped, 0, reason);

        public static ExecutionResult Failed(string reason)
            => new(EExecutionStatus.Failed, 0, reason);

        public static ExecutionResult Interrupted(string reason)
            => new(EExecutionStatus.Interrupted, 0, reason);

        public static ExecutionResult None => new(EExecutionStatus.Success, 0, null);

        private ExecutionResult(EExecutionStatus status, int executedCount, string failureReason)
        {
            Status = status;
            ExecutedCount = executedCount;
            FailureReason = failureReason;
        }

        public ExecutionResult Merge(ExecutionResult other)
        {
            if (other.IsFailed || other.IsInterrupted) return other;
            if (IsFailed || IsInterrupted) return this;
            if (other.IsSkipped) return this;
            if (IsSkipped) return other;
            return new ExecutionResult(EExecutionStatus.Success, ExecutedCount + other.ExecutedCount, null);
        }
    }

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

    public interface IExecutable
    {
        string Name { get; }
        ExecutableMetadata Metadata { get; }
        ExecutionResult Execute(ActionContext ctx);
    }

    public interface IAtomicExecutable : IExecutable
    {
    }

    public interface ICompositeExecutable : IExecutable
    {
        int ChildCount { get; }
        ISimpleExecutable GetChild(int index);
    }

    public interface ISimpleExecutable : IExecutable
    {
    }

    public interface ISequenceExecutable : ICompositeExecutable
    {
    }

    public interface ISelectorExecutable : ICompositeExecutable
    {
    }

    public interface IParallelExecutable : ICompositeExecutable
    {
        ECompositeMode ParallelMode { get; set; }
    }

    public enum ECompareMode
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

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

    public interface ICondition
    {
        string Name { get; }
        ConditionResult Evaluate(ActionContext ctx);
    }

    public interface IConfigurableCondition : ICondition
    {
        void Configure(ConditionConfig config, ConfigToExecutableConverter converter);
    }

    [ConditionTypeId(TypeIdRegistry.Condition.NumericCompare, "NumericCompare")]
    public sealed class NumericCompareCondition : IConfigurableCondition
    {
        public string Name => "NumericCompare";
        public NumericValueRef Left { get; set; }
        public NumericValueRef Right { get; set; }
        public ECompareMode Mode { get; set; } = ECompareMode.Equal;

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
            Left = converter.ConvertNumericValueRef(config.Left);
            Right = converter.ConvertNumericValueRef(config.Right);
            Mode = ParseCompareMode(config.CompareOp);
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            var left = Left.Resolve(ctx);
            var right = Right.Resolve(ctx);
            var passed = Mode switch
            {
                ECompareMode.Equal => Math.Abs(left - right) < 0.0001d,
                ECompareMode.NotEqual => Math.Abs(left - right) >= 0.0001d,
                ECompareMode.Greater => left > right,
                ECompareMode.GreaterOrEqual => left >= right,
                ECompareMode.Less => left < right,
                ECompareMode.LessOrEqual => left <= right,
                _ => false
            };

            return passed ? ConditionResult.Pass : ConditionResult.Fail($"{left} {Mode} {right}");
        }

        private static ECompareMode ParseCompareMode(string compareOp)
        {
            return compareOp?.ToLowerInvariant() switch
            {
                "not_equal" or "notequal" or "neq" or "!=" => ECompareMode.NotEqual,
                "greater" or ">" => ECompareMode.Greater,
                "greater_or_equal" or "greaterequal" or ">=" => ECompareMode.GreaterOrEqual,
                "less" or "<" => ECompareMode.Less,
                "less_or_equal" or "lessequal" or "<=" => ECompareMode.LessOrEqual,
                _ => ECompareMode.Equal
            };
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.PayloadCompare, "PayloadCompare")]
    public sealed class PayloadCompareCondition : IConfigurableCondition
    {
        public string Name => "PayloadCompare";
        public int FieldId { get; set; }
        public ECompareMode Mode { get; set; } = ECompareMode.Equal;
        public NumericValueRef CompareValue { get; set; }
        public bool Negate { get; set; }

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
            FieldId = config.FieldId;
            CompareValue = converter.ConvertNumericValueRef(config.CompareValue);
            Negate = config.Negate;
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            return ConditionResult.Fail("PayloadCompare requires a formal payload predicate binding");
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.Multi, "Multi")]
    public sealed class MultiCondition : IConfigurableCondition
    {
        public string Name => "Multi";
        public ICondition[] Conditions { get; set; } = Array.Empty<ICondition>();
        public bool RequireAll { get; set; } = true;

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            if (Conditions == null || Conditions.Length == 0) return ConditionResult.Pass;

            ConditionResult lastFailure = ConditionResult.Pass;
            for (int i = 0; i < Conditions.Length; i++)
            {
                var result = Conditions[i]?.Evaluate(ctx) ?? ConditionResult.Fail($"Condition[{i}] is null");
                if (RequireAll && !result.Passed) return result;
                if (!RequireAll && result.Passed) return ConditionResult.Pass;
                lastFailure = result;
            }

            return RequireAll ? ConditionResult.Pass : lastFailure;
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.Not, "Not")]
    public sealed class NotCondition : IConfigurableCondition
    {
        public string Name => "Not";
        public ICondition Inner { get; set; }

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            var result = Inner?.Evaluate(ctx) ?? ConditionResult.Fail("Inner condition is null");
            return result.Passed ? ConditionResult.Fail("Inner condition passed") : ConditionResult.Pass;
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.And, "And")]
    public sealed class AndCondition : IConfigurableCondition
    {
        public string Name => "And";
        public ICondition[] Conditions { get; set; } = Array.Empty<ICondition>();

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            if (Conditions == null || Conditions.Length == 0) return ConditionResult.Pass;
            for (int i = 0; i < Conditions.Length; i++)
            {
                var result = Conditions[i]?.Evaluate(ctx) ?? ConditionResult.Fail($"Condition[{i}] is null");
                if (!result.Passed) return result;
            }

            return ConditionResult.Pass;
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.Or, "Or")]
    public sealed class OrCondition : IConfigurableCondition
    {
        public string Name => "Or";
        public ICondition[] Conditions { get; set; } = Array.Empty<ICondition>();

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            if (Conditions == null || Conditions.Length == 0) return ConditionResult.Fail("No conditions");
            ConditionResult lastFailure = ConditionResult.Fail("No condition passed");
            for (int i = 0; i < Conditions.Length; i++)
            {
                var result = Conditions[i]?.Evaluate(ctx) ?? ConditionResult.Fail($"Condition[{i}] is null");
                if (result.Passed) return ConditionResult.Pass;
                lastFailure = result;
            }

            return lastFailure;
        }
    }

    [ConditionTypeId(TypeIdRegistry.Condition.Const, "Const")]
    public sealed class ConstCondition : IConfigurableCondition
    {
        public string Name => "Const";
        public bool Value { get; set; } = true;

        public void Configure(ConditionConfig config, ConfigToExecutableConverter converter)
        {
            Value = !config.Negate;
        }

        public ConditionResult Evaluate(ActionContext ctx)
        {
            return Value ? ConditionResult.Pass : ConditionResult.Fail("Const condition evaluated to false");
        }
    }

    public interface IConditionalExecutable : ICompositeExecutable
    {
        int EvaluateConditionIndex(ActionContext ctx);
    }

    public interface ISwitchExecutable : ICompositeExecutable
    {
        Func<object, int> ValueSelector { get; set; }
    }

    public interface IHasInner
    {
        ISimpleExecutable Inner { get; set; }
    }

    public interface IScheduledExecutable : IExecutable, ISimpleExecutable
    {
        EScheduleMode ScheduleMode { get; }
        bool IsPeriodic { get; }
        float PeriodMs { get; }
        float DurationMs { get; }
    }
}
