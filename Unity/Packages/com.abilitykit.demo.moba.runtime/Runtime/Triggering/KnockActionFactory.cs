using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("knock", "鍑婚/鍑婚€€", "琛屼负/Combat", 0)]
    [Preserve]
    public sealed class KnockActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return KnockAction.FromDef(def);
        }
    }
}
