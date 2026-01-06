using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface IActionFactory
    {
        ITriggerAction Create(ActionDef def);
    }
}
