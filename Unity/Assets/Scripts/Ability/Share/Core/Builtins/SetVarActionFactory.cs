using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerActionType("set_var", "设置变量", "行为/变量", 0)]
    public sealed class SetVarActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SetVarAction.FromDef(def);
        }
    }
}
