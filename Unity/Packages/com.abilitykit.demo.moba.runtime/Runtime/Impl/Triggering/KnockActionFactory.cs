using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("knock", "击飞/击退", "行为/Combat", 0)]
    [Preserve]
    public sealed class KnockActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return KnockAction.FromDef(def);
        }
    }
}
