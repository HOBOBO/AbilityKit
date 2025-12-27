using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("debug_log", "输出日志", "行为/调试", 0)]
    public sealed class DebugLogActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return DebugLogAction.FromDef(def);
        }
    }
}
