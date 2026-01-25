using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerActionType("set_num_var", "设置数值变量", "行为/变量", 1)]
    [Preserve]
    public sealed class SetNumericVarActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SetNumericVarAction.FromDef(def);
        }
    }
}
