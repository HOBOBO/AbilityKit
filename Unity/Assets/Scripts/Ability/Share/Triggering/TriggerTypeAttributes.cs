using System;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public static class TriggerConditionTypes
    {
        public const string All = "all";
        public const string Any = "any";
        public const string Not = "not";
        public const string ArgEq = "arg_eq";
        public const string ArgGt = "arg_gt";
        public const string NumVarGt = "num_var_gt";
    }

    public static class TriggerActionTypes
    {
        public const string Seq = "seq";
        public const string SetVar = "set_var";
        public const string SetNumVar = "set_num_var";
        public const string AttrEffectDuration = "attr_effect_duration";
        public const string DebugLog = "debug_log";
        public const string LogAttacker = "log_attacker";
        public const string EffectExecute = "effect_execute";
        public const string AddBuff = "add_buff";
    }

    public static class TriggerDefArgKeys
    {
        public const string Items = "items";
        public const string Item = "item";
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerConditionTypeAttribute : Attribute
    {
        public TriggerConditionTypeAttribute(string type)
        {
            Type = type;
            DisplayName = type;
            Category = string.Empty;
            Order = 0;
        }

        public TriggerConditionTypeAttribute(string type, string displayName, string category = "", int order = 0)
        {
            Type = type;
            DisplayName = string.IsNullOrEmpty(displayName) ? type : displayName;
            Category = category ?? string.Empty;
            Order = order;
        }

        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerActionTypeAttribute : Attribute
    {
        public TriggerActionTypeAttribute(string type)
        {
            Type = type;
            DisplayName = type;
            Category = string.Empty;
            Order = 0;
        }

        public TriggerActionTypeAttribute(string type, string displayName, string category = "", int order = 0)
        {
            Type = type;
            DisplayName = string.IsNullOrEmpty(displayName) ? type : displayName;
            Category = category ?? string.Empty;
            Order = order;
        }

        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
    }
}
