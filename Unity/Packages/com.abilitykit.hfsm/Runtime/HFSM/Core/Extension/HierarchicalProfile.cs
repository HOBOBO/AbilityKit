#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class HierarchicalProfile<TAction>
    {
        public HierarchicalProfile(
            string id,
            string startState,
            IReadOnlyList<NodeSpec<TAction>>? states,
            IReadOnlyList<TransitionSpec>? transitions)
        {
            Id = id ?? string.Empty;
            StartState = startState ?? string.Empty;
            States = states ?? Array.Empty<NodeSpec<TAction>>();
            Transitions = transitions ?? Array.Empty<TransitionSpec>();
        }

        public string Id { get; }

        public string StartState { get; }

        public IReadOnlyList<NodeSpec<TAction>> States { get; }

        public IReadOnlyList<TransitionSpec> Transitions { get; }
    }
}
