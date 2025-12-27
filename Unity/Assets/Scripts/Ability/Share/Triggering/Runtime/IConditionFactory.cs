using AbilityKit.Triggering.Definitions;

namespace AbilityKit.Triggering.Runtime
{
    public interface IConditionFactory
    {
        ITriggerCondition Create(ConditionDef def);
    }
}
