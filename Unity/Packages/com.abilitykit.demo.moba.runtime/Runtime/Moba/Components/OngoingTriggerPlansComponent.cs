using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class OngoingTriggerPlansComponent : IComponent
    {
        public List<OngoingTriggerPlanEntry> Active;
        public int Revision;
    }

    public sealed class OngoingTriggerPlanEntry
    {
        public long OwnerKey;
        public int[] TriggerIds;
    }
}
