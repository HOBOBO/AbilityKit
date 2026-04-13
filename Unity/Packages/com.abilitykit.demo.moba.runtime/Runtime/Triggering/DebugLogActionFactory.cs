using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("debug_log", "杈撳嚭鏃ュ織", "琛屼负/璋冭瘯", 0)]
    [Preserve]
    public sealed class DebugLogActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return DebugLogAction.FromDef(def);
        }
    }
}
