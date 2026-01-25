using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class NotConditionConfig : ConditionRuntimeConfigBase
    {
        public override string Type => TriggerConditionTypes.Not;

        public ConditionRuntimeConfigBase Item;

        public override ConditionDef ToConditionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict[TriggerDefArgKeys.Item] = Item != null ? Item.ToConditionDef() : null;
            return new ConditionDef(Type, dict);
        }
    }
}
