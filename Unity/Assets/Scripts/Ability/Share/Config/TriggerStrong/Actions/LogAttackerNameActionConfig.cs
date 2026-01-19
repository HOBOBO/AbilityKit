using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class LogAttackerNameActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.LogAttacker;

        public string Format = "{0}攻击者名字";

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["format"] = Format;

            return new ActionDef(Type, dict);
        }
    }
}
