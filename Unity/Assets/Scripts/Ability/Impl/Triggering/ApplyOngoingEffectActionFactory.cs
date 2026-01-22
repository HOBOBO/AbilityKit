using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("apply_ongoing_effect", "施加持续效果", "行为/持续效果", 0)]
    [Preserve]
    public sealed class ApplyOngoingEffectActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ApplyOngoingEffectAction.FromDef(def);
        }
    }
}
