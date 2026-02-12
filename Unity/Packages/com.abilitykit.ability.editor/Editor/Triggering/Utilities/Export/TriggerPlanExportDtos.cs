#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Editor.Utilities
{
    [Serializable]
    internal sealed class TriggerPlanDatabaseDto
    {
        public readonly List<TriggerPlanDto> Triggers = new List<TriggerPlanDto>();

        // Optional: string table for actions like debug_log.
        public readonly Dictionary<int, string> Strings = new Dictionary<int, string>();
    }

    [Serializable]
    internal sealed class TriggerPlanDto
    {
        public int TriggerId;
        public string EventName;
        public int EventId;
        public bool AllowExternal;
        public int Phase;
        public int Priority;
        public PredicatePlanDto Predicate;
        public List<ActionCallPlanDto> Actions;
        public LegacyPredicateDto LegacyPredicate;
        public List<LegacyActionDto> LegacyActions;
    }

    [Serializable]
    internal sealed class LegacyPredicateDto
    {
        public string Type;
        public Dictionary<string, object> Args;
    }

    [Serializable]
    internal sealed class LegacyActionDto
    {
        public string Type;
        public Dictionary<string, object> Args;
    }

    [Serializable]
    internal sealed class PredicatePlanDto
    {
        public string Kind;
        public List<BoolExprNodeDto> Nodes;
    }

    [Serializable]
    internal sealed class BoolExprNodeDto
    {
        public string Kind;
        public bool ConstValue;
        public string CompareOp;
        public NumericValueRefDto Left;
        public NumericValueRefDto Right;
    }

    [Serializable]
    internal sealed class ActionCallPlanDto
    {
        public int ActionId;
        public int Arity;
        public NumericValueRefDto Arg0;
        public NumericValueRefDto Arg1;
    }

    [Serializable]
    internal sealed class NumericValueRefDto
    {
        public string Kind;
        public double ConstValue;
        public int BoardId;
        public int KeyId;
        public int FieldId;
        public string DomainId;
        public string Key;
        public string ExprText;
    }
}
#endif
