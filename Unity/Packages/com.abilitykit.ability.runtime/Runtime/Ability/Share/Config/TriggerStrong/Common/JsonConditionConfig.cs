using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class JsonConditionConfig : ConditionRuntimeConfigBase
    {
        public string TypeValue;
        public Dictionary<string, object> Args;
        public List<ConditionRuntimeConfigBase> Items;
        public ConditionRuntimeConfigBase Item;

        public override string Type => TypeValue;

        public override ConditionDef ToConditionDef()
        {
            if (string.Equals(Type, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
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

            if (string.Equals(Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                var dict = PooledDefArgs.Rent();
                dict[TriggerDefArgKeys.Item] = Item != null ? Item.ToConditionDef() : null;
                return new ConditionDef(Type, dict);
            }

            if (Args == null || Args.Count == 0)
            {
                return new ConditionDef(Type);
            }

            var args = PooledDefArgs.Rent();
            foreach (var kv in Args)
            {
                if (kv.Key == null) continue;
                args[kv.Key] = kv.Value;
            }

            return new ConditionDef(Type, args);
        }
    }
}
