using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("play_presentation", "琛ㄧ幇", "琛屼负/Presentation", 0)]
    [Preserve]
    public sealed class PlayPresentationActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return PlayPresentationAction.FromDef(def);
        }
    }
}
