#nullable enable
using System.Collections.Generic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{
    public sealed class RuntimeSnapshot
    {
        public const int CurrentSnapshotVersion = 2;

        public int SnapshotVersion { get; set; } = CurrentSnapshotVersion;

        public long DefinitionHash { get; set; }

        public bool Initialized { get; set; }

        public int Frame { get; set; }

        public long TimeRaw { get; set; }

        public List<MachineRuntimeSnapshot> Machines { get; set; } =
            new List<MachineRuntimeSnapshot>();

        public List<StateRuntimeSnapshot> States { get; set; } =
            new List<StateRuntimeSnapshot>();
    }
}
