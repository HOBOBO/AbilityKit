using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    public sealed class ExecuteEffectActionConfig : ActionConfigBase
    {
        public override string Type => TriggerActionTypes.EffectExecute;

        public int EffectId;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["effectId"] = EffectId;
            return new ActionDef(Type, dict);
        }
    }
}
