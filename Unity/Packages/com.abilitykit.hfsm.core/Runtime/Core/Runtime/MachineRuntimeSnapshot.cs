#nullable enable
using System.Collections.Generic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class MachineRuntimeSnapshot
    {
        public string MachineId { get; set; } = string.Empty;

        public string ActiveStateId { get; set; } = string.Empty;

        public string RememberedStateId { get; set; } = string.Empty;

        public string PendingTransitionId { get; set; } = string.Empty;

        public string PendingTriggerId { get; set; } = string.Empty;

        public long ActiveSinceRaw { get; set; }
    }
}
