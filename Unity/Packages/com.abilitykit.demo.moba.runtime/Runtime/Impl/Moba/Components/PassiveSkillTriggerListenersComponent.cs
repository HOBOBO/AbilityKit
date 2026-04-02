using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Components
{
    [Actor]
    public sealed class PassiveSkillTriggerListenersComponent : IComponent
    {
        public List<PassiveSkillTriggerListenerRuntime> Active;
    }

    public sealed class PassiveSkillTriggerListenerRuntime
    {
        public int PassiveSkillId;
        public long SourceContextId;
    }
}
