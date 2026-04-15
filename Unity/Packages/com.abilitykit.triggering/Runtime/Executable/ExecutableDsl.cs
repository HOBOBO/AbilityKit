using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 行为构建器基类
    // ========================================================================

    /// <summary>
    /// 行为构建器
    /// </summary>
    public class ExecutableBuilder<T> where T : ISimpleExecutable, new()
    {
        protected readonly T _executable = new();

        public T Build() => _executable;
        public ISimpleExecutable ToExecutable() => _executable;
    }

    // ========================================================================
    // Sequence 构建器
    // ========================================================================

    /// <summary>
    /// Sequence 构建器
    /// </summary>
    public sealed class SequenceBuilder : ExecutableBuilder<SequenceExecutable>
    {
        public SequenceBuilder Add(ISimpleExecutable child) { _executable.Add(child); return this; }
        public SequenceBuilder AddRange(params ISimpleExecutable[] children) { _executable.AddRange(children); return this; }

        public SequenceBuilder AddAction(ActionId actionId)
        {
            _executable.Add(new ActionCallExecutable { ActionId = actionId, Arity = 0 });
            return this;
        }

        public SequenceBuilder AddAction(ActionId actionId, double arg0)
        {
            _executable.Add(new ActionCallExecutable { ActionId = actionId, Arg0 = NumericValueRef.Const(arg0), Arity = 1 });
            return this;
        }

        public SequenceBuilder AddAction(ActionId actionId, double arg0, double arg1)
        {
            _executable.Add(new ActionCallExecutable { ActionId = actionId, Arg0 = NumericValueRef.Const(arg0), Arg1 = NumericValueRef.Const(arg1), Arity = 2 });
            return this;
        }

        public SequenceBuilder AddAction(ActionId actionId, NumericValueRef arg0)
        {
            _executable.Add(new ActionCallExecutable { ActionId = actionId, Arg0 = arg0, Arity = 1 });
            return this;
        }

        public SequenceBuilder AddAction(ActionId actionId, NumericValueRef arg0, NumericValueRef arg1)
        {
            _executable.Add(new ActionCallExecutable { ActionId = actionId, Arg0 = arg0, Arg1 = arg1, Arity = 2 });
            return this;
        }

        public SequenceBuilder If(ICondition condition, ISimpleExecutable body)
        {
            _executable.Add(new IfExecutable { Condition = condition, Body = body });
            return this;
        }

        public SequenceBuilder IfElse(ICondition condition, ISimpleExecutable thenBody, ISimpleExecutable elseBody)
        {
            _executable.Add(new IfElseExecutable().If(condition, thenBody).Else(elseBody));
            return this;
        }

        public SequenceBuilder Delay(float delayMs)
        {
            _executable.Add(new DelayExecutable { DelayMs = delayMs });
            return this;
        }

        public SequenceBuilder Log(string message)
        {
            _executable.Add(new DebugLogExecutable { Message = message });
            return this;
        }

        public SequenceBuilder Event(string eventName)
        {
            _executable.Add(new EventSendExecutable { EventName = eventName });
            return this;
        }
    }

    // ========================================================================
    // Selector 构建器
    // ========================================================================

    /// <summary>
    /// Selector 构建器
    /// </summary>
    public sealed class SelectorBuilder : ExecutableBuilder<SelectorExecutable>
    {
        public SelectorBuilder Add(ISimpleExecutable child) { _executable.Add(child); return this; }
        public SelectorBuilder AddAction(ActionId actionId) { _executable.Add(new ActionCallExecutable { ActionId = actionId, Arity = 0 }); return this; }
    }

    // ========================================================================
    // Parallel 构建器
    // ========================================================================

    /// <summary>
    /// Parallel 构建器
    /// </summary>
    public sealed class ParallelBuilder : ExecutableBuilder<ParallelExecutable>
    {
        public ParallelBuilder Add(ISimpleExecutable child) { _executable.Add(child); return this; }
        public ParallelBuilder SetMode(ECompositeMode mode) { _executable.ParallelMode = mode; return this; }
        public ParallelBuilder SetTimeout(float timeoutMs) { _executable.TimeoutMs = timeoutMs; return this; }
    }

    // ========================================================================
    // IfElse 构建器
    // ========================================================================

    /// <summary>
    /// If-Else 构建器
    /// </summary>
    public sealed class IfElseBuilder : ExecutableBuilder<IfElseExecutable>
    {
        public IfElseBuilder If(ICondition condition, ISimpleExecutable body)
        {
            _executable.If(condition, body);
            return this;
        }

        public IfElseBuilder ElseIf(ICondition condition, ISimpleExecutable body)
        {
            _executable.ElseIf(condition, body);
            return this;
        }

        public IfElseBuilder Else(ISimpleExecutable body)
        {
            _executable.Else(body);
            return this;
        }
    }

    // ========================================================================
    // Switch 构建器
    // ========================================================================

    /// <summary>
    /// Switch 构建器
    /// </summary>
    public sealed class SwitchBuilder : ExecutableBuilder<SwitchExecutable>
    {
        public SwitchBuilder Selector(Func<object, int> selector)
        {
            _executable.ValueSelector = selector;
            return this;
        }

        public SwitchBuilder Case(int value, ISimpleExecutable body)
        {
            _executable.Case(value, body);
            return this;
        }

        public SwitchBuilder Case(int value, ActionId actionId)
        {
            _executable.Case(value, new ActionCallExecutable { ActionId = actionId, Arity = 0 });
            return this;
        }

        public SwitchBuilder Case(int value, ActionId actionId, double arg0)
        {
            _executable.Case(value, new ActionCallExecutable { ActionId = actionId, Arg0 = NumericValueRef.Const(arg0), Arity = 1 });
            return this;
        }

        public SwitchBuilder Default(ISimpleExecutable body)
        {
            _executable.Default(body);
            return this;
        }
    }

    // ========================================================================
    // RandomSelector 构建器
    // ========================================================================

    /// <summary>
    /// RandomSelector 构建器
    /// </summary>
    public sealed class RandomSelectorBuilder : ExecutableBuilder<RandomSelectorExecutable>
    {
        public RandomSelectorBuilder Add(ISimpleExecutable child, float weight = 1f)
        {
            _executable.Children.Add(child);
            return this;
        }

        public RandomSelectorBuilder SetWeights(params float[] weights)
        {
            _executable.Weights = weights;
            return this;
        }
    }

    // ========================================================================
    // Repeat 构建器
    // ========================================================================

    /// <summary>
    /// Repeat 构建器
    /// </summary>
    public sealed class RepeatBuilder : ExecutableBuilder<RepeatExecutable>
    {
        public RepeatBuilder SetChild(ISimpleExecutable child) { _executable.Child = child; return this; }
        public RepeatBuilder SetCount(int count) { _executable.Count = count; return this; }
        public RepeatBuilder StopOnFailure(bool value) { _executable.StopOnFailure = value; return this; }
    }

    // ========================================================================
    // Until 构建器
    // ========================================================================

    /// <summary>
    /// Until 构建器
    /// </summary>
    public sealed class UntilBuilder : ExecutableBuilder<UntilExecutable>
    {
        public UntilBuilder SetChild(ISimpleExecutable child) { _executable.Child = child; return this; }
        public UntilBuilder UntilSuccess() { _executable.UntilSuccess = true; return this; }
        public UntilBuilder UntilFailure() { _executable.UntilSuccess = false; return this; }
        public UntilBuilder SetMaxIterations(int max) { _executable.MaxIterations = max; return this; }
    }

    // ========================================================================
    // 条件构建器扩展
    // ========================================================================

    /// <summary>
    /// 条件构建器扩展
    /// </summary>
    public static class ConditionBuilderExtensions
    {
        public static ICondition Const(bool value)
            => new ConstCondition { Value = value };

        public static ICondition And(this ICondition left, ICondition right)
            => new AndCondition { Left = left, Right = right };

        public static ICondition Or(this ICondition left, ICondition right)
            => new OrCondition { Left = left, Right = right };

        public static ICondition Not(this ICondition inner)
            => new NotCondition { Inner = inner };

        public static ICondition Compare(ECompareOp op, NumericValueRef left, NumericValueRef right)
            => new NumericCompareCondition { Op = op, Left = left, Right = right };

        public static ICondition PayloadCompare(int fieldId, ECompareOp op, NumericValueRef compareValue)
            => new PayloadCompareCondition { FieldId = fieldId, Op = op, CompareValue = compareValue };

        public static ICondition PayloadCompare(int fieldId, ECompareOp op, double compareValue)
            => PayloadCompare(fieldId, op, NumericValueRef.Const(compareValue));

        public static ICondition HasTarget(bool negate = false)
            => new HasTargetCondition { Negate = negate };

        public static ICondition AllOf(params ICondition[] conditions)
            => new MultiCondition { Combinator = EConditionCombinator.And, Conditions = new List<ICondition>(conditions) };

        public static ICondition AnyOf(params ICondition[] conditions)
            => new MultiCondition { Combinator = EConditionCombinator.Or, Conditions = new List<ICondition>(conditions) };

        public static ICondition Gt(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.GreaterThan, left, right);

        public static ICondition Ge(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.GreaterThanOrEqual, left, right);

        public static ICondition Lt(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.LessThan, left, right);

        public static ICondition Le(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.LessThanOrEqual, left, right);

        public static ICondition Eq(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.Equal, left, right);

        public static ICondition Ne(NumericValueRef left, NumericValueRef right)
            => Compare(ECompareOp.NotEqual, left, right);
    }

    // ========================================================================
    // 调度构建器扩展
    // ========================================================================

    /// <summary>
    /// 调度构建器扩展
    /// </summary>
    public static class ScheduledExecutableBuilderExtensions
    {
        public static IScheduledExecutable Timed(this ISimpleExecutable inner, float durationMs)
            => ScheduledExecutableFactory.WrapTimed(inner, durationMs);

        public static IScheduledExecutable Periodic(this ISimpleExecutable inner, float periodMs, int maxExecutions = -1)
            => ScheduledExecutableFactory.WrapPeriodic(inner, periodMs, maxExecutions);

        public static IScheduledExecutable External(this ISimpleExecutable inner)
            => ScheduledExecutableFactory.WrapExternal(inner);
    }

    // ========================================================================
    // DSL 静态入口
    // ========================================================================

    /// <summary>
    /// 行为 DSL 静态入口
    /// </summary>
    public static class ExecutableDsl
    {
        public static SequenceBuilder Sequence() => new();
        public static SelectorBuilder Selector() => new();
        public static ParallelBuilder Parallel() => new();
        public static IfElseBuilder IfElse() => new();
        public static SwitchBuilder Switch() => new();
        public static RandomSelectorBuilder RandomSelector() => new();
        public static RepeatBuilder Repeat() => new();
        public static UntilBuilder Until() => new();

        public static ISimpleExecutable If(ICondition condition, ISimpleExecutable body)
            => new IfExecutable { Condition = condition, Body = body };

        public static ActionCallExecutable Action(ActionId actionId)
            => new ActionCallExecutable { ActionId = actionId, Arity = 0 };

        public static ActionCallExecutable Action(ActionId actionId, double arg0)
            => new ActionCallExecutable { ActionId = actionId, Arg0 = NumericValueRef.Const(arg0), Arity = 1 };

        public static ActionCallExecutable Action(ActionId actionId, double arg0, double arg1)
            => new ActionCallExecutable { ActionId = actionId, Arg0 = NumericValueRef.Const(arg0), Arg1 = NumericValueRef.Const(arg1), Arity = 2 };

        public static ActionCallExecutable Action(ActionId actionId, NumericValueRef arg0)
            => new ActionCallExecutable { ActionId = actionId, Arg0 = arg0, Arity = 1 };

        public static ActionCallExecutable Action(ActionId actionId, NumericValueRef arg0, NumericValueRef arg1)
            => new ActionCallExecutable { ActionId = actionId, Arg0 = arg0, Arg1 = arg1, Arity = 2 };

        public static DelayExecutable Delay(float delayMs)
            => new DelayExecutable { DelayMs = delayMs };

        public static DebugLogExecutable Log(string message)
            => new DebugLogExecutable { Message = message };

        public static EventSendExecutable Event(string eventName)
            => new EventSendExecutable { EventName = eventName };

        public static NoOpExecutable NoOp() => NoOpExecutable.Instance;
        public static SuccessExecutable Success() => SuccessExecutable.Instance;
        public static FailExecutable Fail(string reason = null) => new FailExecutable { Reason = reason };
    }
}
