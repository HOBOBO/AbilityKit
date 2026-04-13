using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    public sealed class AddBuffActionConfig : ActionConfigBase
    {
        public override string Type => TriggerActionTypes.AddBuff;

        public List<int> BuffIds = new List<int>();

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["buffIds"] = BuffIds != null ? new List<int>(BuffIds) : null;
            return new ActionDef(Type, dict);
        }
    }
}
