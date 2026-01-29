using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class PassiveSkillTriggerListenersComponent : IComponent
    {
        public List<PassiveSkillTriggerListenerRuntime> Active;
    }

    public sealed class PassiveSkillTriggerListenerRuntime
    {
        public int PassiveSkillId;
        public int TriggerId;
        public string EventId;

        public long SourceContextId;

        public List<PassiveSkillTriggerEntryRuntime> Entries;

        public IEventSubscription Sub;
    }

    public sealed class PassiveSkillTriggerEntryRuntime
    {
        public AbilityKit.Ability.Triggering.Definitions.TriggerDef Def;
        public System.Collections.Generic.IReadOnlyDictionary<string, object> InitialLocalVars;
    }
}
