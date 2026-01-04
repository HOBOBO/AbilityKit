using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
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
