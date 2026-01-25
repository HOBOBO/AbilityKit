using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("play_presentation", "表现", "行为/Presentation", 0)]
    [Preserve]
    public sealed class PlayPresentationActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return PlayPresentationAction.FromDef(def);
        }
    }
}
