using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("add_buff", "添加Buff", "行为/Buff", 0)]
    [Preserve]
    public sealed class AddBuffActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return AddBuffAction.FromDef(def);
        }
    }
}
