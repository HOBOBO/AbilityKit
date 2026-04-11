using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class SetVarActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.SetVar;

        public VarScope Scope = VarScope.Local;

        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        public VarScope ValueFromScope = VarScope.Local;
        public string ValueFromKey;

        public string Key;
        public ArgRuntimeEntryCore Value = new ArgRuntimeEntryCore();

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
}
