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
    public sealed class AllConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => "all";

        public List<ConditionRuntimeConfigBase> Items = new List<ConditionRuntimeConfigBase>();

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            var items = new List<ConditionDef>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    items.Add(c.ToConditionDef());
                }
            }
            dict["items"] = items;
            return new ConditionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class AnyConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => "any";

        public List<ConditionRuntimeConfigBase> Items = new List<ConditionRuntimeConfigBase>();

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            var items = new List<ConditionDef>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    items.Add(c.ToConditionDef());
                }
            }
            dict["items"] = items;
            return new ConditionDef(Type, dict);
        }
    }

    [Serializable]
    public sealed class NotConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => "not";

        public ConditionRuntimeConfigBase Item;

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["item"] = Item != null ? Item.ToConditionDef() : null;
            return new ConditionDef(Type, dict);
        }
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
                    throw new InvalidOperationException("arg_gt requires const threshold value");
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
    public sealed class SequenceActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => "seq";

        public List<ActionRuntimeConfigBase> Items = new List<ActionRuntimeConfigBase>();

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            var items = new List<ActionDef>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var a = Items[i];
                    if (a == null) continue;
                    items.Add(a.ToActionDef());
                }
            }
            dict["items"] = items;
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

    [Serializable]
    public sealed class ExecuteEffectActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => "effect_execute";

        public int EffectId;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["effectId"] = EffectId;
            return new ActionDef(Type, dict);
        }
    }
}
