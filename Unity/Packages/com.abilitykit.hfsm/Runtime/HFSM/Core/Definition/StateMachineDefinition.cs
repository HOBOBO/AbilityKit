#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;


namespace AbilityKit.HFSM.Definition
{
    /// <summary>
    /// Unity-independent HFSM runtime definition. Editor-only layout and CLR type names do not
    /// belong in this model. Runtime snapshots are bound to <see cref="ComputeDefinitionHash"/>.
    /// </summary>
    public sealed class StateMachineDefinition
    {
        public const int CurrentFormatVersion = 1;

        public string DefinitionId { get; set; } = string.Empty;

        public int FormatVersion { get; set; } = CurrentFormatVersion;

        public string RootMachineId { get; set; } = string.Empty;

        public List<MachineDefinition> Machines { get; set; } = new List<MachineDefinition>();

        /// <summary>
        /// Computes a stable semantic hash. Display names, comments and source list ordering are
        /// intentionally excluded; transition ordering is defined by priority and transition id.
        /// </summary>
        public long ComputeDefinitionHash()
        {
            var hash = DeterministicHash.Combine(DeterministicHash.OffsetBasis, (long)FormatVersion);
            hash = DeterministicHash.Combine(hash, HashString(RootMachineId));

            var machines = new List<MachineDefinition>(Machines ?? new List<MachineDefinition>());
            machines.Sort((left, right) => string.CompareOrdinal(left?.Id, right?.Id));
            for (var machineIndex = 0; machineIndex < machines.Count; machineIndex++)
            {
                var machine = machines[machineIndex];
                if (machine == null)
                {
                    hash = DeterministicHash.Combine(hash, -1L);
                    continue;
                }

                hash = DeterministicHash.Combine(hash, HashString(machine.Id));
                hash = DeterministicHash.Combine(hash, HashString(machine.InitialStateId));
                hash = DeterministicHash.Combine(hash, machine.RememberLastState ? 1L : 0L);

                var states = new List<StateDefinition>(machine.States ?? new List<StateDefinition>());
                states.Sort((left, right) => string.CompareOrdinal(left?.Id, right?.Id));
                hash = DeterministicHash.Combine(hash, (long)states.Count);
                for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
                {
                    var state = states[stateIndex];
                    if (state == null)
                    {
                        hash = DeterministicHash.Combine(hash, -1L);
                        continue;
                    }

                    hash = DeterministicHash.Combine(hash, HashString(state.Id));
                    hash = DeterministicHash.Combine(hash, HashString(state.BehaviorKey));
                    hash = DeterministicHash.Combine(hash, HashString(state.ChildMachineId));
                    hash = DeterministicHash.Combine(hash, state.RequiresExitApproval ? 1L : 0L);
                }

                var transitions = new List<TransitionDefinition>(
                    machine.Transitions ?? new List<TransitionDefinition>());
                transitions.Sort(CompareTransitions);
                hash = DeterministicHash.Combine(hash, (long)transitions.Count);
                for (var transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
                {
                    var transition = transitions[transitionIndex];
                    if (transition == null)
                    {
                        hash = DeterministicHash.Combine(hash, -1L);
                        continue;
                    }

                    hash = DeterministicHash.Combine(hash, HashString(transition.Id));
                    hash = DeterministicHash.Combine(hash, transition.FromAnyState ? 1L : 0L);
                    hash = DeterministicHash.Combine(hash, HashString(transition.FromStateId));
                    hash = DeterministicHash.Combine(hash, HashString(transition.ToStateId));
                    hash = DeterministicHash.Combine(hash, HashString(transition.TriggerId));
                    hash = DeterministicHash.Combine(hash, HashString(transition.ConditionKey));
                    hash = DeterministicHash.Combine(hash, HashString(transition.ActionKey));
                    hash = DeterministicHash.Combine(hash, (long)transition.Priority);
                    hash = DeterministicHash.Combine(hash, transition.ForceImmediate ? 1L : 0L);
                    hash = DeterministicHash.Combine(hash, transition.MinimumActiveDurationRaw);
                }
            }

            return hash;
        }

        internal static int CompareTransitions(TransitionDefinition? left, TransitionDefinition? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            var priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.Id, right.Id);
        }

        internal static long HashString(string? value)
        {
            var hash = DeterministicHash.OffsetBasis;
            var text = value ?? string.Empty;
            for (var index = 0; index < text.Length; index++)
            {
                hash = DeterministicHash.Combine(hash, (long)text[index]);
            }

            return hash;
        }
    }
}
