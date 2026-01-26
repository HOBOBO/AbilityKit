using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class JsonActionConfig : ActionRuntimeConfigBase
    {
        public string TypeValue;
        public Dictionary<string, object> Args;
        public List<ActionRuntimeConfigBase> Items;

        public override string Type => TypeValue;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();

            if (Args != null)
            {
                foreach (var kv in Args)
                {
                    if (kv.Key == null) continue;
                    dict[kv.Key] = kv.Value;
                }
            }

            if (Items != null && string.Equals(Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var items = new List<ActionDef>(Items.Count);
                for (int i = 0; i < Items.Count; i++)
                {
                    var a = Items[i];
                    if (a == null) continue;
                    items.Add(a.ToActionDef());
                }
                dict[TriggerDefArgKeys.Items] = items;
            }

            return new ActionDef(Type, dict);
        }
    }
}
