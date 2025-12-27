using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Builtins
{
    [TriggerConditionType("arg_gt", "参数大于", "条件/参数", 10)]
    public sealed class ArgGreaterThanConditionFactory : IConditionFactory
    {
        public ITriggerCondition Create(ConditionDef def)
        {
            return ArgGreaterThanCondition.FromDef(def);
        }
    }
}
