#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;


namespace AbilityKit.HFSM.Definition
{

    public enum ParallelExitPolicy
    {
        Any = 0,
        All = 1,
    }

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

        /// <summary>A ghost state immediately evaluates its local tick transitions after entry.</summary>
        public bool IsGhostState { get; set; }

        /// <summary>
        /// Stable behavior bindings executed as independent states in parallel. When populated,
        /// <see cref="BehaviorKey"/> and <see cref="ChildMachineId"/> must be empty.
        /// </summary>
        public List<string> ParallelBehaviorKeys { get; set; } = new List<string>();

        public ParallelExitPolicy ParallelExitPolicy { get; set; } = ParallelExitPolicy.Any;
    }
}
