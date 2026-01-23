using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("spawn_summon", "创造召唤物", "行为/Combat", 0)]
    [Preserve]
    public sealed class SpawnSummonActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SpawnSummonAction.FromDef(def);
        }
    }
}
