using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("log_attacker", "输出攻击者名字", "行为/调试", 10)]
    public sealed class LogAttackerNameActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return LogAttackerNameAction.FromDef(def);
        }
    }
}
