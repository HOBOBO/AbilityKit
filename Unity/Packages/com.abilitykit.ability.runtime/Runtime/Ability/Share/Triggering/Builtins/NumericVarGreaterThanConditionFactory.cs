using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerConditionType("num_var_gt", "数值变量大于", "条件/变量", 20)]
    [Preserve]
    public sealed class NumericVarGreaterThanConditionFactory : IConditionFactory
    {
        public ITriggerCondition Create(ConditionDef def)
        {
            return NumericVarGreaterThanCondition.FromDef(def);
        }
    }
}
