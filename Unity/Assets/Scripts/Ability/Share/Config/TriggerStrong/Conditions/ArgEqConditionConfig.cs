using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class ArgEqConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => TriggerConditionTypes.ArgEq;

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
}
