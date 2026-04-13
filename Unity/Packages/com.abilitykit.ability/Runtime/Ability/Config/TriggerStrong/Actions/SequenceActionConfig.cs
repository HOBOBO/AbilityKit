using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.CodeGen;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    [TriggerAction(TriggerActionTypes.Seq)]
    public sealed class SequenceActionConfig : ActionConfigBase
    {
        public override string Type => TriggerActionTypes.Seq;

        public List<ActionConfigBase> Items = new List<ActionConfigBase>();

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
            dict[TriggerDefArgKeys.Items] = items;
            return new ActionDef(Type, dict);
        }
    }
}
