#nullable enable
using System.Collections.Generic;

namespace AbilityKit.HFSM
{
    public sealed class HfsmRuntimeSnapshot
    {
        public const int CurrentSnapshotVersion = 2;

        public int SnapshotVersion { get; set; } = CurrentSnapshotVersion;

        public long DefinitionHash { get; set; }

        public bool Initialized { get; set; }

        public int Frame { get; set; }

        public long TimeRaw { get; set; }

        public List<HfsmMachineRuntimeSnapshot> Machines { get; set; } =
            new List<HfsmMachineRuntimeSnapshot>();

        public List<HfsmStateRuntimeSnapshot> States { get; set; } =
            new List<HfsmStateRuntimeSnapshot>();
    }

    public sealed class HfsmMachineRuntimeSnapshot
    {
        public string MachineId { get; set; } = string.Empty;

        public string ActiveStateId { get; set; } = string.Empty;

        public string RememberedStateId { get; set; } = string.Empty;

        public string PendingTransitionId { get; set; } = string.Empty;

        public string PendingTriggerId { get; set; } = string.Empty;

        public long ActiveSinceRaw { get; set; }
    }

    public sealed class HfsmStateRuntimeSnapshot
    {
        public string MachineId { get; set; } = string.Empty;

        public string StateId { get; set; } = string.Empty;

        public int PayloadVersion { get; set; }

        public string Payload { get; set; } = string.Empty;
    }
}
