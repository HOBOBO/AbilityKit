using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("take_damage", "鍙楀埌浼ゅ(鐢熸垚鍨?", "琛屼负/Combat", 0)]
    [Preserve]
    public sealed class TakeDamageActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return TakeDamageAction.FromDef(def);
        }
    }
}
