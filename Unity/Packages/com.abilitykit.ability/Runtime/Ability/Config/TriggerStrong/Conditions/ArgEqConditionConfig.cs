using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.CodeGen;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    [TriggerCondition(TriggerConditionTypes.ArgEq)]
    [TriggerParam(0, "key", ETriggerParamType.Int, ETriggerParamSource.Payload)]
    [TriggerParam(1, "value", ETriggerParamType.Int, ETriggerParamSource.Const | ETriggerParamSource.Payload)]
    public sealed class ArgEqConditionConfig : ConditionConfigBase
    {
        public override string Type => TriggerConditionTypes.ArgEq;

        public string Key;

        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;

        public ArgRuntimeEntryCore Value = new ArgRuntimeEntryCore();

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
