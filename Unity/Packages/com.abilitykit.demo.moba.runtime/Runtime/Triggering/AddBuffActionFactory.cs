using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("add_buff", "娣诲姞Buff", "琛屼负/Buff", 0)]
    [Preserve]
    public sealed class AddBuffActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return AddBuffAction.FromDef(def);
        }
    }
}
