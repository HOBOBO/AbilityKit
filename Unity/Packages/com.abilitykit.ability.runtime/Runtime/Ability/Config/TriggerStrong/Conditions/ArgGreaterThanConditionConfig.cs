using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.CodeGen;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    [TriggerCondition(TriggerConditionTypes.ArgGt)]
    [TriggerParam(0, "key", ETriggerParamType.Int, ETriggerParamSource.Payload)]
    [TriggerParam(1, "value", ETriggerParamType.Int, ETriggerParamSource.Const | ETriggerParamSource.Payload)]
    public sealed class ArgGreaterThanConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => TriggerConditionTypes.ArgGt;

        public string Key;
        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;
        public ArgRuntimeEntryCore ThresholdValue = new ArgRuntimeEntryCore();

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
}
