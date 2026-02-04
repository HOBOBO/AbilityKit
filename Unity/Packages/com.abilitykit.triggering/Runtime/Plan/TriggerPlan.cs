using AbilityKit.Triggering.Registry;
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

        public readonly EPredicateKind PredicateKind;
        public readonly bool HasPredicate;
        public readonly FunctionId PredicateId;

        public readonly byte PredicateArity;
        public readonly NumericValueRef PredicateArg0;
        public readonly NumericValueRef PredicateArg1;

        public readonly PredicateExprPlan PredicateExpr;

        public readonly ActionCallPlan[] Actions;

        public TriggerPlan(int phase, int priority, FunctionId predicateId, ActionCallPlan[] actions)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, NumericValueRef predicateArg0, ActionCallPlan[] actions)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 1;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, double predicateArg0, ActionCallPlan[] actions)
            : this(phase, priority, predicateId, NumericValueRef.Const(predicateArg0), actions)
        {
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, NumericValueRef predicateArg0, NumericValueRef predicateArg1, ActionCallPlan[] actions)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = 2;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = predicateArg1;
            PredicateExpr = default;
            Actions = actions;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, double predicateArg0, double predicateArg1, ActionCallPlan[] actions)
            : this(phase, priority, predicateId, NumericValueRef.Const(predicateArg0), NumericValueRef.Const(predicateArg1), actions)
        {
        }

        public TriggerPlan(int phase, int priority, PredicateExprPlan predicateExpr, ActionCallPlan[] actions)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.Expr;
            HasPredicate = true;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = predicateExpr;
            Actions = actions;
        }

        public TriggerPlan(int phase, int priority, ActionCallPlan[] actions)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.None;
            HasPredicate = false;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions;
        }
    }
}
