#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;


namespace AbilityKit.HFSM.Definition
{

    public sealed class MachineDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string InitialStateId { get; set; } = string.Empty;

        public bool RememberLastState { get; set; }

        public List<StateDefinition> States { get; set; } = new List<StateDefinition>();

        public List<TransitionDefinition> Transitions { get; set; } = new List<TransitionDefinition>();
    }
}
