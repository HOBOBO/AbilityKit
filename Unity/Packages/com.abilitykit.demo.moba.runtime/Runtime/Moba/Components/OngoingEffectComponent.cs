using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class OngoingEffectsComponent : IComponent
    {
        public List<OngoingEffectRuntime> Active;
    }

    public sealed class OngoingEffectRuntime
    {
        public long InstanceId;
        public int OngoingEffectId;
        public int SourceActorId;

        public int RemainingMs;
        public int NextTickMs;

        public long OwnerKey;

        public bool Applied;
    }
}
