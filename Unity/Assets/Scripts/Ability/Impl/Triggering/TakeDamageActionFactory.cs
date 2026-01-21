using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("take_damage", "受到伤害(生成型)", "行为/Combat", 0)]
    [Preserve]
    public sealed class TakeDamageActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return TakeDamageAction.FromDef(def);
        }
    }
}
