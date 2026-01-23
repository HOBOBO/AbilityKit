using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("aoe_burst", "范围瞬发(重查目标)", "行为/Area", 0)]
    [Preserve]
    public sealed class AoeBurstActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return AoeBurstAction.FromDef(def);
        }
    }
}
