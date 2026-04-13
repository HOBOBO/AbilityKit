using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("give_damage", "閫犳垚浼ゅ", "琛屼负/Combat", 0)]
    [Preserve]
    public sealed class GiveDamageActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return GiveDamageAction.FromDef(def);
        }
    }
}
