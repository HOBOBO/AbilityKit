using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Builtins
{
    [TriggerConditionType("arg_eq", "参数等于", "条件/参数", 0)]
    public sealed class ArgEqualsConditionFactory : IConditionFactory
    {
        public ITriggerCondition Create(ConditionDef def)
        {
            return ArgEqualsCondition.FromDef(def);
        }
    }
}
