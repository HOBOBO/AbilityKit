using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("emit", "发射器", "行为/Emitter", 0)]
    [Preserve]
    public sealed class EmitActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return EmitAction.FromDef(def);
        }
    }
}
