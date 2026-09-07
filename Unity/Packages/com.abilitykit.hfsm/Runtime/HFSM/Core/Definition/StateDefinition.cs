#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;


namespace AbilityKit.HFSM.Definition
{

    public sealed class StateDefinition
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>An empty key selects the built-in no-op state behavior.</summary>
        public string BehaviorKey { get; set; } = string.Empty;

        /// <summary>An optional nested machine. A machine may have only one parent state.</summary>
        public string ChildMachineId { get; set; } = string.Empty;

        /// <summary>
        /// When true, a non-forced transition becomes pending until the state behavior approves exit.
        /// </summary>
        public bool RequiresExitApproval { get; set; }
    }
}
