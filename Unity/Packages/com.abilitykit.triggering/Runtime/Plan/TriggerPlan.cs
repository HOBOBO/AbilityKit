using AbilityKit.Triggering.Registry;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public readonly struct LegacyPredicatePlan
    {
        public readonly string Type;
        public readonly IReadOnlyDictionary<string, object> Args;

        public LegacyPredicatePlan(string type, IReadOnlyDictionary<string, object> args)
        {
            Type = type;
            Args = args;
        }
    }

    public readonly struct LegacyActionPlan
    {
        public readonly string Type;
        public readonly IReadOnlyDictionary<string, object> Args;

        public LegacyActionPlan(string type, IReadOnlyDictionary<string, object> args)
        {
            Type = type;
            Args = args;
        }
    }

    public readonly struct ActionCallPlan
    {
        public readonly ActionId Id;
        public readonly byte Arity;
        public readonly IntValueRef Arg0;
        public readonly IntValueRef Arg1;

        public ActionCallPlan(ActionId id)
        {
            Id = id;
            Arity = 0;
            Arg0 = default;
            Arg1 = default;
        }

        public ActionCallPlan(ActionId id, IntValueRef arg0)
        {
            Id = id;
            Arity = 1;
            Arg0 = arg0;
            Arg1 = default;
        }

        public ActionCallPlan(ActionId id, int arg0)
            : this(id, IntValueRef.Const(arg0))
        {
        }

        public ActionCallPlan(ActionId id, IntValueRef arg0, IntValueRef arg1)
        {
            Id = id;
            Arity = 2;
            Arg0 = arg0;
            Arg1 = arg1;
        }

        public ActionCallPlan(ActionId id, int arg0, int arg1)
            : this(id, IntValueRef.Const(arg0), IntValueRef.Const(arg1))
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
        public readonly IntValueRef PredicateArg0;
        public readonly IntValueRef PredicateArg1;

        public readonly PredicateExprPlan PredicateExpr;

        public readonly LegacyPredicatePlan? LegacyPredicate;

        public readonly ActionCallPlan[] Actions;

        public readonly LegacyActionPlan[] LegacyActions;

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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = null;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, IntValueRef predicateArg0, ActionCallPlan[] actions)
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = null;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, int predicateArg0, ActionCallPlan[] actions)
            : this(phase, priority, predicateId, IntValueRef.Const(predicateArg0), actions)
        {
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, IntValueRef predicateArg0, IntValueRef predicateArg1, ActionCallPlan[] actions)
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = null;
        }

        public TriggerPlan(int phase, int priority, FunctionId predicateId, int predicateArg0, int predicateArg1, ActionCallPlan[] actions)
            : this(phase, priority, predicateId, IntValueRef.Const(predicateArg0), IntValueRef.Const(predicateArg1), actions)
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = null;
        }

        public TriggerPlan(int phase, int priority, PredicateExprPlan predicateExpr, ActionCallPlan[] actions, LegacyActionPlan[] legacyActions)
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = legacyActions;
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = null;
        }

        public TriggerPlan(int phase, int priority, LegacyPredicatePlan legacyPredicate, ActionCallPlan[] actions, LegacyActionPlan[] legacyActions = null)
        {
            Phase = phase;
            Priority = priority;
            PredicateKind = EPredicateKind.Legacy;
            HasPredicate = true;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            LegacyPredicate = legacyPredicate;
            Actions = actions;
            LegacyActions = legacyActions;
        }

        public TriggerPlan(int phase, int priority, ActionCallPlan[] actions, LegacyActionPlan[] legacyActions)
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
            LegacyPredicate = null;
            Actions = actions;
            LegacyActions = legacyActions;
        }
    }
}
