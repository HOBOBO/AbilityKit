#nullable enable
using System.Collections.Generic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class StateRuntimeSnapshot
    {
        public string MachineId { get; set; } = string.Empty;

        public string StateId { get; set; } = string.Empty;

        public int PayloadVersion { get; set; }

        public string Payload { get; set; } = string.Empty;
    }
}
