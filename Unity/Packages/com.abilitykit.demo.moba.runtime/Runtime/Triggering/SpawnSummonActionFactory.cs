using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("spawn_summon", "鍒涢€犲彫鍞ょ墿", "琛屼负/Combat", 0)]
    [Preserve]
    public sealed class SpawnSummonActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SpawnSummonAction.FromDef(def);
        }
    }
}
