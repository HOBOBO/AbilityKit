using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerActionType("attr_effect_duration", "属性加成(持续)", "行为/属性", 0)]
    public sealed class AddAttributeEffectForDurationActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return AddAttributeEffectForDurationAction.FromDef(def);
        }
    }
}
