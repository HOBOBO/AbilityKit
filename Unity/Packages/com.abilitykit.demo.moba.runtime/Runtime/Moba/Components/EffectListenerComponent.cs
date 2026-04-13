using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    public enum ListenScope
    {
        SelfOnly = 0,
        OthersOnly = 1,
        Any = 2,
    }

    [Actor]
    public sealed class EffectListenersComponent : IComponent
    {
        public List<EffectListenerRuntime> Active;
    }

    public sealed class EffectListenerRuntime
    {
        public string EventId;
        public int OwnerActorId;
        public ListenScope Scope;

        public int ExecuteEffectId;
        public EffectExecuteMode ExecuteMode;

        public long SourceContextId;
    }
}
