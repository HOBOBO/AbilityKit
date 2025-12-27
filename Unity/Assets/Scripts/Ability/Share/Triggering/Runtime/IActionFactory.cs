using AbilityKit.Triggering.Definitions;

namespace AbilityKit.Triggering.Runtime
{
    public interface IActionFactory
    {
        ITriggerAction Create(ActionDef def);
    }
}
