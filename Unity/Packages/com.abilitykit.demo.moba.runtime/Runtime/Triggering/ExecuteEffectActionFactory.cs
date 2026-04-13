using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("effect_execute", "鎵ц鏁堟灉", "琛屼负/鏁堟灉", 0)]
    [Preserve]
    public sealed class ExecuteEffectActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ExecuteEffectAction.FromDef(def);
        }
    }
}
