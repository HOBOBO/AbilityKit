using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface IConditionCompiler
    {
        ITriggerCondition Compile(ConditionDef def);
    }
}
