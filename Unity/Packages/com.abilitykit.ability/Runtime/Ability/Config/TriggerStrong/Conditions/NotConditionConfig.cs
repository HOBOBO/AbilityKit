using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    public sealed class NotConditionConfig : ConditionConfigBase
    {
        public override string Type => TriggerConditionTypes.Not;

        public ConditionConfigBase Item;

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict[TriggerDefArgKeys.Item] = Item != null ? Item.ToConditionDef() : null;
            return new ConditionDef(Type, dict);
        }
    }
}
