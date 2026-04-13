using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerConditionType("arg_gt", "参数大于", "条件/参数", 10)]
    [Preserve]
    public sealed class ArgGreaterThanConditionFactory : IConditionFactory
    {
        public ITriggerCondition Create(ConditionDef def)
        {
            return ArgGreaterThanCondition.FromDef(def);
        }
    }
}
