#nullable enable
using System;
using System.Collections.Generic;

namespace AbilityKit.HFSM
{
    public sealed class HfsmValidationIssue
    {
        public HfsmValidationIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} at {Path}: {Message}";
    }

    public sealed class HfsmValidationResult
    {
        internal HfsmValidationResult(List<HfsmValidationIssue> issues)
        {
            Issues = issues.AsReadOnly();
        }

        public bool IsValid => Issues.Count == 0;

        public IReadOnlyList<HfsmValidationIssue> Issues { get; }
    }

    public sealed class HfsmDefinitionException : InvalidOperationException
    {
        public HfsmDefinitionException(IReadOnlyList<HfsmValidationIssue> issues)
            : base(BuildMessage(issues))
        {
            Issues = issues ?? Array.Empty<HfsmValidationIssue>();
        }

        public IReadOnlyList<HfsmValidationIssue> Issues { get; }

        private static string BuildMessage(IReadOnlyList<HfsmValidationIssue>? issues)
        {
            if (issues == null || issues.Count == 0) return "The HFSM definition is invalid.";
            return $"The HFSM definition is invalid: {issues[0]}";
        }
    }

    public static class HfsmDefinitionValidator
    {
        public static HfsmValidationResult Validate(HfsmDefinition? definition)
        {
            var issues = new List<HfsmValidationIssue>();
            if (definition == null)
            {
                Add(issues, "HFSM001", "$", "Definition is null.");
                return new HfsmValidationResult(issues);
            }

            if (definition.FormatVersion != HfsmDefinition.CurrentFormatVersion)
            {
                Add(issues, "HFSM002", "$.formatVersion",
                    $"Unsupported format version {definition.FormatVersion}; expected {HfsmDefinition.CurrentFormatVersion}.");
            }

            if (string.IsNullOrWhiteSpace(definition.RootMachineId))
            {
                Add(issues, "HFSM003", "$.rootMachineId", "Root machine id is required.");
            }

            var machines = definition.Machines ?? new List<HfsmMachineDefinition>();
            var machinesById = new Dictionary<string, HfsmMachineDefinition>(StringComparer.Ordinal);
            for (var machineIndex = 0; machineIndex < machines.Count; machineIndex++)
            {
                var path = $"$.machines[{machineIndex}]";
                var machine = machines[machineIndex];
                if (machine == null)
                {
                    Add(issues, "HFSM004", path, "Machine is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(machine.Id))
                {
                    Add(issues, "HFSM005", path + ".id", "Machine id is required.");
                }
                else if (!machinesById.TryAdd(machine.Id, machine))
                {
                    Add(issues, "HFSM006", path + ".id", $"Duplicate machine id '{machine.Id}'.");
                }

                ValidateMachine(machine, path, issues);
            }

            if (!string.IsNullOrWhiteSpace(definition.RootMachineId) &&
                !machinesById.ContainsKey(definition.RootMachineId))
            {
                Add(issues, "HFSM007", "$.rootMachineId",
                    $"Root machine '{definition.RootMachineId}' does not exist.");
            }

            ValidateHierarchy(definition, machinesById, issues);
            return new HfsmValidationResult(issues);
        }

        public static void ValidateOrThrow(HfsmDefinition? definition)
        {
            var result = Validate(definition);
            if (!result.IsValid) throw new HfsmDefinitionException(result.Issues);
        }

        private static void ValidateMachine(
            HfsmMachineDefinition machine,
            string machinePath,
            List<HfsmValidationIssue> issues)
        {
            var states = machine.States ?? new List<HfsmStateDefinition>();
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                var path = $"{machinePath}.states[{stateIndex}]";
                var state = states[stateIndex];
                if (state == null)
                {
                    Add(issues, "HFSM010", path, "State is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.Id))
                {
                    Add(issues, "HFSM011", path + ".id", "State id is required.");
                }
                else if (!stateIds.Add(state.Id))
                {
                    Add(issues, "HFSM012", path + ".id", $"Duplicate state id '{state.Id}'.");
                }
            }

            if (states.Count == 0)
            {
                Add(issues, "HFSM013", machinePath + ".states", "A machine must contain at least one state.");
            }

            if (string.IsNullOrWhiteSpace(machine.InitialStateId) || !stateIds.Contains(machine.InitialStateId))
            {
                Add(issues, "HFSM014", machinePath + ".initialStateId",
                    $"Initial state '{machine.InitialStateId}' does not exist in machine '{machine.Id}'.");
            }

            var transitionIds = new HashSet<string>(StringComparer.Ordinal);
            var transitions = machine.Transitions ?? new List<HfsmTransitionDefinition>();
            for (var transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
            {
                var path = $"{machinePath}.transitions[{transitionIndex}]";
                var transition = transitions[transitionIndex];
                if (transition == null)
                {
                    Add(issues, "HFSM020", path, "Transition is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(transition.Id))
                {
                    Add(issues, "HFSM021", path + ".id", "Transition id is required.");
                }
                else if (!transitionIds.Add(transition.Id))
                {
                    Add(issues, "HFSM022", path + ".id", $"Duplicate transition id '{transition.Id}'.");
                }

                if (!transition.FromAnyState &&
                    (string.IsNullOrWhiteSpace(transition.FromStateId) || !stateIds.Contains(transition.FromStateId)))
                {
                    Add(issues, "HFSM023", path + ".fromStateId",
                        $"Source state '{transition.FromStateId}' does not exist in machine '{machine.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(transition.ToStateId) || !stateIds.Contains(transition.ToStateId))
                {
                    Add(issues, "HFSM024", path + ".toStateId",
                        $"Target state '{transition.ToStateId}' does not exist in machine '{machine.Id}'.");
                }

                if (transition.MinimumActiveDurationRaw < 0)
                {
                    Add(issues, "HFSM025", path + ".minimumActiveDurationRaw",
                        "Minimum active duration cannot be negative.");
                }
            }
        }

        private static void ValidateHierarchy(
            HfsmDefinition definition,
            Dictionary<string, HfsmMachineDefinition> machinesById,
            List<HfsmValidationIssue> issues)
        {
            var parentByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var machinePair in machinesById)
            {
                var states = machinePair.Value.States ?? new List<HfsmStateDefinition>();
                for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
                {
                    var state = states[stateIndex];
                    if (state == null || string.IsNullOrWhiteSpace(state.ChildMachineId)) continue;

                    var path = $"$.machines['{machinePair.Key}'].states[{stateIndex}].childMachineId";
                    if (!machinesById.ContainsKey(state.ChildMachineId))
                    {
                        Add(issues, "HFSM030", path,
                            $"Child machine '{state.ChildMachineId}' does not exist.");
                        continue;
                    }

                    if (string.Equals(state.ChildMachineId, definition.RootMachineId, StringComparison.Ordinal))
                    {
                        Add(issues, "HFSM031", path, "The root machine cannot be nested under a state.");
                    }

                    if (parentByMachine.TryGetValue(state.ChildMachineId, out var existingParent))
                    {
                        Add(issues, "HFSM032", path,
                            $"Child machine '{state.ChildMachineId}' already belongs to '{existingParent}'.");
                    }
                    else
                    {
                        parentByMachine[state.ChildMachineId] = $"{machinePair.Key}/{state.Id}";
                    }
                }
            }

            if (!machinesById.ContainsKey(definition.RootMachineId)) return;

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            Visit(definition.RootMachineId, machinesById, visiting, visited, issues);
            foreach (var machineId in machinesById.Keys)
            {
                if (!visited.Contains(machineId))
                {
                    Add(issues, "HFSM034", $"$.machines['{machineId}']",
                        $"Machine '{machineId}' is unreachable from root '{definition.RootMachineId}'.");
                }
            }
        }

        private static void Visit(
            string machineId,
            Dictionary<string, HfsmMachineDefinition> machinesById,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<HfsmValidationIssue> issues)
        {
            if (visited.Contains(machineId)) return;
            if (!visiting.Add(machineId))
            {
                Add(issues, "HFSM033", $"$.machines['{machineId}']", "Machine hierarchy contains a cycle.");
                return;
            }

            var states = machinesById[machineId].States ?? new List<HfsmStateDefinition>();
            for (var index = 0; index < states.Count; index++)
            {
                var childId = states[index]?.ChildMachineId;
                if (!string.IsNullOrWhiteSpace(childId) && machinesById.ContainsKey(childId))
                {
                    Visit(childId, machinesById, visiting, visited, issues);
                }
            }

            visiting.Remove(machineId);
            visited.Add(machineId);
        }

        private static void Add(List<HfsmValidationIssue> issues, string code, string path, string message)
        {
            issues.Add(new HfsmValidationIssue(code, path, message));
        }
    }
}
