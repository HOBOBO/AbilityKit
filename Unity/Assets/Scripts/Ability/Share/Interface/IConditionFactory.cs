using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface IConditionFactory
    {
        ITriggerCondition Create(ConditionDef def);
    }
}
