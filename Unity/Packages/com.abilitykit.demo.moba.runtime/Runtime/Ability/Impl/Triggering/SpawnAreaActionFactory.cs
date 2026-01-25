using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("spawn_area", "生成范围", "行为/Area", 0)]
    [Preserve]
    public sealed class SpawnAreaActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SpawnAreaAction.FromDef(def);
        }
    }
}
