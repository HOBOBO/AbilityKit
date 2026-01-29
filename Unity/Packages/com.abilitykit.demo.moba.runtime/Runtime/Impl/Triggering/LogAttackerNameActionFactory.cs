using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("log_attacker", "输出攻击者名字", "行为/调试", 10)]
    [Preserve]
    public sealed class LogAttackerNameActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return LogAttackerNameAction.FromDef(def);
        }
    }
}
