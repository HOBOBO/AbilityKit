using AbilityKit.Triggering.Definitions;

namespace AbilityKit.Triggering.Runtime
{
    public interface IConditionCompiler
    {
        ITriggerCondition Compile(ConditionDef def);
    }
}
