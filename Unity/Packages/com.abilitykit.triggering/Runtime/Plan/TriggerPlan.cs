using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public readonly struct ActionCallPlan
    {
        public readonly ActionId Id;
        public readonly byte Arity;
        public readonly NumericValueRef Arg0;
        public readonly NumericValueRef Arg1;

        /// <summary>
        /// 具名参数字典（key=参数名，value=参数值引用）
        /// 为 null 时表示向后兼容的位置参数模式（使用 Arg0/Arg1）
        /// </summary>
        public readonly Dictionary<string, ActionArgValue> Args;

        public ActionCallPlan(ActionId id)
        {
            Id = id;
            Arity = 0;
            Arg0 = default;
            Arg1 = default;
            Args = null;
        }

        public ActionCallPlan(ActionId id, NumericValueRef arg0)
        {
            Id = id;
            Arity = 1;
            Arg0 = arg0;
            Arg1 = default;
            Args = null;
        }

        public ActionCallPlan(ActionId id, double arg0)
            : this(id, NumericValueRef.Const(arg0))
        {
        }

        public ActionCallPlan(ActionId id, NumericValueRef arg0, NumericValueRef arg1)
        {
            Id = id;
            Arity = 2;
            Arg0 = arg0;
            Arg1 = arg1;
            Args = null;
        }

        public ActionCallPlan(ActionId id, double arg0, double arg1)
            : this(id, NumericValueRef.Const(arg0), NumericValueRef.Const(arg1))
        {
        }

        /// <summary>
        /// 创建带有具名参数的 ActionCallPlan
        /// Arity 由 Args 字典中的条目数量决定
        /// </summary>
        public static ActionCallPlan WithArgs(ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return new ActionCallPlan(id, args);
        }

        private ActionCallPlan(ActionId id, Dictionary<string, ActionArgValue> args)
        {
            Id = id;
            Arity = (byte)(args != null ? args.Count : 0);
            Arg0 = default;
            Arg1 = default;
            Args = args;
        }

        /// <summary>
        /// 是否使用了具名参数模式
        /// </summary>
        public bool HasNamedArgs => Args != null && Args.Count > 0;
    }

    public readonly struct TriggerPlan<TArgs>
    {
        public readonly int Phase;
        public readonly int Priority;

        /// <summary>
        /// 优先级打断阈值。Execute 成功后自动调用 StopBelowPriority。
        /// 0 = 不自动打断；>0 = 以此值为阈值打断更低优先级的触发器。
        /// </summary>
        public readonly int InterruptPriority;

        /// <summary>
        /// 触发器唯一标识（用于打断溯源）
        /// </summary>
        public readonly int TriggerId;

        public readonly EPredicateKind PredicateKind;
        public readonly bool HasPredicate;
        public readonly FunctionId PredicateId;

        public readonly byte PredicateArity;
        public readonly NumericValueRef PredicateArg0;
        public readonly NumericValueRef PredicateArg1;

        public readonly PredicateExprPlan PredicateExpr;

        public readonly ActionCallPlan[] Actions;

        /// <summary>
        /// 表现层 Cue（VFX / SFX / UI 反馈）
        /// </summary>
        public readonly ITriggerCue Cue;

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
            Cue = cue ?? NullTriggerCue.Instance;
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            NumericValueRef predicateArg0,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 1;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
            Cue = cue ?? NullTriggerCue.Instance;
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            double predicateArg0,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
            : this(phase, priority, triggerId, predicateId, NumericValueRef.Const(predicateArg0), interruptPriority, actions, cue)
        {
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            NumericValueRef predicateArg0,
            NumericValueRef predicateArg1,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 2;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = predicateArg1;
            PredicateExpr = default;
            Actions = actions;
            Cue = cue ?? NullTriggerCue.Instance;
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            double predicateArg0,
            double predicateArg1,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
            : this(phase, priority, triggerId, predicateId, NumericValueRef.Const(predicateArg0), NumericValueRef.Const(predicateArg1), interruptPriority, actions, cue)
        {
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            PredicateExprPlan predicateExpr,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Expr;
            HasPredicate = true;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = predicateExpr;
            Actions = actions;
            Cue = cue ?? NullTriggerCue.Instance;
        }

        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            int interruptPriority,
            ActionCallPlan[] actions,
            ITriggerCue cue)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.None;
            HasPredicate = false;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
            Cue = cue ?? NullTriggerCue.Instance;
        }

        // ========== 便捷构造器（向后兼容，不传 Cue）==========

        public TriggerPlan(int phase, int priority, int triggerId, FunctionId predicateId, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateId, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, FunctionId predicateId, NumericValueRef predicateArg0, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateId, predicateArg0, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, FunctionId predicateId, double predicateArg0, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateId, predicateArg0, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, FunctionId predicateId, NumericValueRef predicateArg0, NumericValueRef predicateArg1, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateId, predicateArg0, predicateArg1, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, FunctionId predicateId, double predicateArg0, double predicateArg1, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateId, predicateArg0, predicateArg1, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, PredicateExprPlan predicateExpr, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, predicateExpr, interruptPriority, actions, null)
        {
        }

        public TriggerPlan(int phase, int priority, int triggerId, int interruptPriority, ActionCallPlan[] actions)
            : this(phase, priority, triggerId, interruptPriority, actions, null)
        {
        }
    }

    /// <summary>
    /// ActionCallPlan 的扩展方法
    /// </summary>
    public static class ActionCallPlanExtensions
    {
        /// <summary>
        /// 创建带有一个具名参数的 ActionCallPlan
        /// </summary>
        public static ActionCallPlan WithArg(this ActionId id, string name, double value)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name] = ActionArgValue.OfConst(value, name)
            });
        }

        /// <summary>
        /// 创建带有两个具名参数的 ActionCallPlan
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, string name0, double value0, string name1, double value1)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1)
            });
        }

        /// <summary>
        /// 创建带有三个具名参数的 ActionCallPlan
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, string name0, double value0, string name1, double value1, string name2, double value2)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1),
                [name2] = ActionArgValue.OfConst(value2, name2)
            });
        }

        /// <summary>
        /// 创建带有具名参数的 ActionCallPlan
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(id, args);
        }
    }
}
