using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("apply_ongoing_effect", "鏂藉姞鎸佺画鏁堟灉", "琛屼负/鎸佺画鏁堟灉", 0)]
    [Preserve]
    public sealed class ApplyOngoingEffectActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ApplyOngoingEffectAction.FromDef(def);
        }
    }
}
