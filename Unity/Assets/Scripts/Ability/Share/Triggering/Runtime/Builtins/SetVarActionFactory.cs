using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Builtins
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
