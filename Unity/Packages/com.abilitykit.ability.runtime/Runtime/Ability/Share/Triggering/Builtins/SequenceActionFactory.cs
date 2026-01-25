using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    [TriggerActionType("seq", "顺序组", "行为/流程", 0)]
    [Preserve]
    public sealed class SequenceActionFactory : IActionFactory
    {
        private readonly TriggerRegistry _registry;

        public SequenceActionFactory(TriggerRegistry registry)
        {
            _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
        }

        public ITriggerAction Create(ActionDef def)
        {
            return SequenceAction.FromDef(def, _registry);
        }
    }
}
