using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public readonly struct ActionCallPlan
    {
        public readonly ActionId Id;
        public readonly byte Arity;
        public readonly NumericValueRef Arg0;
        public readonly NumericValueRef Arg1;

        public ActionCallPlan(ActionId id)
        {
            Id = id;
            Arity = 0;
            Arg0 = default;
            Arg1 = default;
        }

        public ActionCallPlan(ActionId id, NumericValueRef arg0)
        {
            Id = id;
            Arity = 1;
            Arg0 = arg0;
            Arg1 = default;
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
        }

        public ActionCallPlan(ActionId id, double arg0, double arg1)
            : this(id, NumericValueRef.Const(arg0), NumericValueRef.Const(arg1))
        {
        }
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
}
