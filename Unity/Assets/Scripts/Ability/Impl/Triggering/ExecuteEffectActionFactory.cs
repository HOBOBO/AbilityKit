using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("effect_execute", "执行效果", "行为/效果", 0)]
    public sealed class ExecuteEffectActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ExecuteEffectAction.FromDef(def);
        }
    }
}
