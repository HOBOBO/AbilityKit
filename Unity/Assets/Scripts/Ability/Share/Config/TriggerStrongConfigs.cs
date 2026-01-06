using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using UnityEngine;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public abstract class ConditionRuntimeConfigBase
    {
        public abstract string Type { get; }
        public abstract ConditionDef ToConditionDef();
    }

    [Serializable]
    public abstract class ActionRuntimeConfigBase
    {
        public abstract string Type { get; }
        public abstract ActionDef ToActionDef();
    }

    [Serializable]
    public sealed class ArgEqConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => "arg_eq";

        public string Key;

        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;

        public ArgRuntimeEntry Value = new ArgRuntimeEntry();

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["key"] = Key;

            if (ValueSource == ValueSourceKind.Var)
            {
                dict["value_source"] = "var";
                dict["value_key"] = ValueFromKey;
                if (ValueFromScope == VarScope.Global)
                {
                    dict["value_scope"] = "global";
                }
            }
            else
            {
                dict["value_source"] = "const";
                dict["value"] = Value != null ? Value.GetBoxedValue() : null;
            }

            return new ConditionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class ArgGreaterThanConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => "arg_gt";

        public string Key;
        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;

        public float Threshold;
        public ArgRuntimeEntry ThresholdValue = new ArgRuntimeEntry();

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["key"] = Key;

            if (ValueSource == ValueSourceKind.Var)
            {
                dict["value_source"] = "var";
                dict["value_key"] = ValueFromKey;
                if (ValueFromScope == VarScope.Global)
                {
                    dict["value_scope"] = "global";
                }
            }
            else
            {
                dict["value_source"] = "const";
                if (ThresholdValue != null && ThresholdValue.Kind != ArgValueKind.None)
                {
                    dict["value"] = ThresholdValue.GetBoxedValue();
                }
                else
                {
                    dict["value"] = Threshold;
                }
            }

            return new ConditionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class SetVarActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => "set_var";

        public VarScope Scope = VarScope.Local;

        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;

        public string Key;
        public ArgRuntimeEntry Value = new ArgRuntimeEntry();

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["key"] = Key;

            if (ValueSource == ValueSourceKind.Var)
            {
                dict["value_source"] = "var";
                dict["value_key"] = ValueFromKey;
                if (ValueFromScope == VarScope.Global)
                {
                    dict["value_scope"] = "global";
                }
            }
            else
            {
                dict["value_source"] = "const";
                dict["value"] = Value != null ? Value.GetBoxedValue() : null;
            }

            if (Scope == VarScope.Global)
            {
                dict["scope"] = "global";
            }

            return new ActionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class DebugLogActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => "debug_log";

        public string Message;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["message"] = Message;

            return new ActionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class LogAttackerNameActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => "log_attacker";

        public string Format = "{0}攻击者名字";

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["format"] = Format;

            return new ActionDef(Type, dict);
        }
    }
}
