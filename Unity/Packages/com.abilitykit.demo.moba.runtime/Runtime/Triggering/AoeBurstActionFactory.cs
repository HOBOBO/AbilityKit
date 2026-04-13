using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("aoe_burst", "鑼冨洿鐬彂(閲嶆煡鐩爣)", "琛屼负/Area", 0)]
    [Preserve]
    public sealed class AoeBurstActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return AoeBurstAction.FromDef(def);
        }
    }
}
