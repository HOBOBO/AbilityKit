using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class AnyConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => TriggerConditionTypes.Any;

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
            dict[TriggerDefArgKeys.Items] = items;
            return new ConditionDef(Type, dict);
        }
    }
}
