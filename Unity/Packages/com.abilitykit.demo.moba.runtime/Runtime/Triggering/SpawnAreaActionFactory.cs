using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("spawn_area", "鐢熸垚鑼冨洿", "琛屼负/Area", 0)]
    [Preserve]
    public sealed class SpawnAreaActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return SpawnAreaAction.FromDef(def);
        }
    }
}
