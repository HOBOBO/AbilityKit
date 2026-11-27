#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;

namespace AbilityKit.HFSM.Runtime
{
    /// <summary>
    /// 复制定义、解析绑定并建立有序执行图；每次编译均创建独立的行为实例和运行状态。
    /// </summary>
    internal static class StateMachineCompiler<TOwner>
    {
        internal static Dictionary<string, CompiledHfsmMachine<TOwner>> Compile(
            StateMachineDefinition definition,
            RuntimeBindings<TOwner> bindings)
        {
            var machines = new Dictionary<string, CompiledHfsmMachine<TOwner>>(StringComparer.Ordinal);
            for (var machineIndex = 0; machineIndex < definition.Machines.Count; machineIndex++)
            {
                var machineDefinition = definition.Machines[machineIndex];
                var machine = new CompiledHfsmMachine<TOwner>(machineDefinition);
                machines.Add(machine.Id, machine);
                for (var stateIndex = 0; stateIndex < machineDefinition.States.Count; stateIndex++)
                {
                    var stateDefinition = machineDefinition.States[stateIndex];
                    machine.StatesById.Add(
                        stateDefinition.Id,
                        new CompiledHfsmState<TOwner>(stateDefinition, CreateStateBehavior(stateDefinition, bindings),
                            CreateStateActions(stateDefinition.LogicEntryActionKeys, bindings),
                            CreateStateActions(stateDefinition.LogicExitActionKeys, bindings)));
                }
            }

            foreach (var machinePair in machines)
            {
                var machine = machinePair.Value;
                foreach (var statePair in machine.StatesById)
                {
                    if (!string.IsNullOrEmpty(statePair.Value.Definition.ChildMachineId))
                    {
                        var child = machines[statePair.Value.Definition.ChildMachineId];
                        statePair.Value.ChildMachine = child;
                        child.ParentMachine = machine;
                        child.ParentStateId = statePair.Key;
                    }
                }

                for (var index = 0; index < machine.Definition.Transitions.Count; index++)
                {
                    var definitionItem = machine.Definition.Transitions[index];
                    var transition = new CompiledHfsmTransition<TOwner>(
                        definitionItem,
                        bindings.CreateCondition(definitionItem.ConditionKey),
                        bindings.CreateAction(definitionItem.ActionKey));
                    machine.TransitionsById.Add(definitionItem.Id, transition);
                    AddTransition(machine, transition);
                }

                SortTransitions(machine.TickFromAny);
                foreach (var triggerPair in machine.TriggerFromAny) SortTransitions(triggerPair.Value);
                foreach (var statePair in machine.StatesById)
                {
                    SortTransitions(statePair.Value.TickTransitions);
                    foreach (var triggerPair in statePair.Value.TriggerTransitions) SortTransitions(triggerPair.Value);
                }
            }

            return machines;
        }

        private static IRuntimeState<TOwner> CreateStateBehavior(
            StateDefinition definition,
            RuntimeBindings<TOwner> bindings)
        {
            var keys = definition.ParallelBehaviorKeys ?? new List<string>();
            if (keys.Count == 0) return bindings.CreateState(definition.BehaviorKey);

            var states = new List<IRuntimeState<TOwner>>(keys.Count);
            for (var index = 0; index < keys.Count; index++)
                states.Add(bindings.CreateState(keys[index]));
            return new ParallelHfsmState<TOwner>(states, definition.ParallelExitPolicy);
        }

        private static List<IStateLifecycleAction<TOwner>> CreateStateActions(
            List<string> keys,
            RuntimeBindings<TOwner> bindings)
        {
            var actions = new List<IStateLifecycleAction<TOwner>>(keys?.Count ?? 0);
            if (keys == null) return actions;
            for (var index = 0; index < keys.Count; index++)
                actions.Add(bindings.CreateStateAction(keys[index]));
            return actions;
        }

        private static void AddTransition(CompiledHfsmMachine<TOwner> machine, CompiledHfsmTransition<TOwner> transition)
        {
            var definition = transition.Definition;
            if (string.IsNullOrEmpty(definition.TriggerId))
            {
                var list = definition.FromAnyState
                    ? machine.TickFromAny
                    : machine.StatesById[definition.FromStateId].TickTransitions;
                list.Add(transition);
                return;
            }

            var triggers = definition.FromAnyState
                ? machine.TriggerFromAny
                : machine.StatesById[definition.FromStateId].TriggerTransitions;
            if (!triggers.TryGetValue(definition.TriggerId, out var triggerList))
            {
                triggerList = new List<CompiledHfsmTransition<TOwner>>();
                triggers.Add(definition.TriggerId, triggerList);
            }

            triggerList.Add(transition);
        }

        private static void SortTransitions(List<CompiledHfsmTransition<TOwner>> transitions)
        {
            transitions.Sort((left, right) => StateMachineDefinition.CompareTransitions(
                left.Definition,
                right.Definition));
        }

        internal static StateMachineDefinition CloneDefinition(StateMachineDefinition source)
        {
            var clone = new StateMachineDefinition
            {
                DefinitionId = source.DefinitionId ?? string.Empty,
                FormatVersion = source.FormatVersion,
                RootMachineId = source.RootMachineId ?? string.Empty,
                Parameters = (source.Parameters ?? new List<StateMachineParameterDefinition>())
                    .Select(parameter => new StateMachineParameterDefinition
                    {
                        Id = parameter.Id,
                        Name = parameter.Name ?? string.Empty,
                        DefaultValue = parameter.DefaultValue,
                        SupportedOperations = new List<int>(parameter.SupportedOperations ?? new List<int>()),
                    }).ToList(),
            };

            for (var machineIndex = 0; machineIndex < source.Machines.Count; machineIndex++)
            {
                var sourceMachine = source.Machines[machineIndex];
                var machine = new MachineDefinition
                {
                    Id = sourceMachine.Id ?? string.Empty,
                    InitialStateId = sourceMachine.InitialStateId ?? string.Empty,
                    RememberLastState = sourceMachine.RememberLastState,
                };

                for (var stateIndex = 0; stateIndex < sourceMachine.States.Count; stateIndex++)
                {
                    var state = sourceMachine.States[stateIndex];
                    machine.States.Add(new StateDefinition
                    {
                        Id = state.Id ?? string.Empty,
                        BehaviorKey = state.BehaviorKey ?? string.Empty,
                        LogicEntryActionKeys = state.LogicEntryActionKeys == null
                            ? new List<string>() : new List<string>(state.LogicEntryActionKeys),
                        LogicExitActionKeys = state.LogicExitActionKeys == null
                            ? new List<string>() : new List<string>(state.LogicExitActionKeys),
                        ChildMachineId = state.ChildMachineId ?? string.Empty,
                        RequiresExitApproval = state.RequiresExitApproval,
                        IsGhostState = state.IsGhostState,
                        ParallelBehaviorKeys = new List<string>(
                            state.ParallelBehaviorKeys ?? new List<string>()),
                        ParallelExitPolicy = state.ParallelExitPolicy,
                    });
                }

                for (var transitionIndex = 0; transitionIndex < sourceMachine.Transitions.Count; transitionIndex++)
                {
                    var transition = sourceMachine.Transitions[transitionIndex];
                    machine.Transitions.Add(new TransitionDefinition
                    {
                        Id = transition.Id ?? string.Empty,
                        FromAnyState = transition.FromAnyState,
                        FromStateId = transition.FromStateId ?? string.Empty,
                        ToStateId = transition.ToStateId ?? string.Empty,
                        TriggerId = transition.TriggerId ?? string.Empty,
                        ConditionKey = transition.ConditionKey ?? string.Empty,
                        ActionKey = transition.ActionKey ?? string.Empty,
                        Priority = transition.Priority,
                        ForceImmediate = transition.ForceImmediate,
                        ExitMachine = transition.ExitMachine,
                        MinimumActiveDurationRaw = transition.MinimumActiveDurationRaw,
                    });
                }

                clone.Machines.Add(machine);
            }

            return clone;
        }

    }

    /// <summary>
    /// 单次运行拥有的机器图及活动游标，不在不同运行时之间共享。
    /// </summary>
    internal sealed class CompiledHfsmMachine<TOwner>
    {
        public CompiledHfsmMachine(MachineDefinition definition)
        {
            Definition = definition;
        }

        public string Id => Definition.Id;
        public MachineDefinition Definition { get; }
        public CompiledHfsmMachine<TOwner>? ParentMachine { get; set; }
        public string ParentStateId { get; set; } = string.Empty;
        public Dictionary<string, CompiledHfsmState<TOwner>> StatesById { get; } =
            new Dictionary<string, CompiledHfsmState<TOwner>>(StringComparer.Ordinal);
        public Dictionary<string, CompiledHfsmTransition<TOwner>> TransitionsById { get; } =
            new Dictionary<string, CompiledHfsmTransition<TOwner>>(StringComparer.Ordinal);
        public List<CompiledHfsmTransition<TOwner>> TickFromAny { get; } = new List<CompiledHfsmTransition<TOwner>>();
        public Dictionary<string, List<CompiledHfsmTransition<TOwner>>> TriggerFromAny { get; } =
            new Dictionary<string, List<CompiledHfsmTransition<TOwner>>>(StringComparer.Ordinal);
        public string ActiveStateId { get; set; } = string.Empty;
        public string RememberedStateId { get; set; } = string.Empty;
        public string PendingTransitionId { get; set; } = string.Empty;
        public string PendingTriggerId { get; set; } = string.Empty;
        public long ActiveSinceRaw { get; set; }
    }

    /// <summary>
    /// 编译后的状态绑定，保持定义顺序及行为实例归属。
    /// </summary>
    internal sealed class CompiledHfsmState<TOwner>
    {
        public CompiledHfsmState(StateDefinition definition, IRuntimeState<TOwner> behavior,
            List<IStateLifecycleAction<TOwner>> entryActions,
            List<IStateLifecycleAction<TOwner>> exitActions)
        {
            Definition = definition;
            Behavior = behavior;
            EntryActions = entryActions;
            ExitActions = exitActions;
        }

        public string Id => Definition.Id;
        public StateDefinition Definition { get; }
        public IRuntimeState<TOwner> Behavior { get; }
        public List<IStateLifecycleAction<TOwner>> EntryActions { get; }
        public List<IStateLifecycleAction<TOwner>> ExitActions { get; }
        public CompiledHfsmMachine<TOwner>? ChildMachine { get; set; }
        public List<CompiledHfsmTransition<TOwner>> TickTransitions { get; } = new List<CompiledHfsmTransition<TOwner>>();
        public Dictionary<string, List<CompiledHfsmTransition<TOwner>>> TriggerTransitions { get; } =
            new Dictionary<string, List<CompiledHfsmTransition<TOwner>>>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 已解析的迁移条件与动作，优先级排序由编译器统一完成。
    /// </summary>
    internal sealed class CompiledHfsmTransition<TOwner>
    {
        public CompiledHfsmTransition(
            TransitionDefinition definition,
            ITransitionCondition<TOwner>? condition,
            ITransitionAction<TOwner>? action)
        {
            Definition = definition;
            Condition = condition;
            Action = action;
        }

        public TransitionDefinition Definition { get; }
        public ITransitionCondition<TOwner>? Condition { get; }
        public ITransitionAction<TOwner>? Action { get; }
    }

    /// <summary>
    /// 按定义顺序驱动并行子行为，并沿用既有退出策略及快照格式。
    /// </summary>
    internal sealed class ParallelHfsmState<TOwner> : RuntimeStateBase<TOwner>, IStateSnapshotParticipant
    {
        private readonly List<IRuntimeState<TOwner>> _states;
        private readonly ParallelExitPolicy _exitPolicy;

        public ParallelHfsmState(
            List<IRuntimeState<TOwner>> states,
            ParallelExitPolicy exitPolicy)
        {
            _states = states;
            _exitPolicy = exitPolicy;
        }

        public int SnapshotVersion => 1;

        public override void OnEnter(TOwner owner, in TickContext context)
        {
            for (var index = 0; index < _states.Count; index++)
                _states[index].OnEnter(owner, in context);
        }

        public override void OnTick(TOwner owner, in TickContext context)
        {
            for (var index = 0; index < _states.Count; index++)
                _states[index].OnTick(owner, in context);
        }

        public override void OnExitRequested(TOwner owner, in TickContext context)
        {
            for (var index = 0; index < _states.Count; index++)
                _states[index].OnExitRequested(owner, in context);
        }

        public override bool CanExit(TOwner owner, in TickContext context)
        {
            if (_exitPolicy == ParallelExitPolicy.All)
            {
                for (var index = 0; index < _states.Count; index++)
                    if (!_states[index].CanExit(owner, in context)) return false;
                return true;
            }

            for (var index = 0; index < _states.Count; index++)
                if (_states[index].CanExit(owner, in context)) return true;
            return false;
        }

        public override void OnExit(TOwner owner, in TickContext context)
        {
            for (var index = 0; index < _states.Count; index++)
                _states[index].OnExit(owner, in context);
        }

        public string CaptureSnapshot()
        {
            var builder = new StringBuilder();
            for (var index = 0; index < _states.Count; index++)
            {
                if (index > 0) builder.Append(';');
                if (_states[index] is not IStateSnapshotParticipant participant)
                {
                    builder.Append('-');
                    continue;
                }

                var payload = participant.CaptureSnapshot() ?? string.Empty;
                builder.Append(participant.SnapshotVersion);
                builder.Append('|');
                builder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)));
            }
            return builder.ToString();
        }

        public void ValidateSnapshot(int version, string payload)
        {
            var entries = ParseSnapshot(version, payload);
            for (var index = 0; index < _states.Count; index++)
            {
                if (_states[index] is IStateSnapshotParticipant participant)
                {
                    var entry = entries[index];
                    participant.ValidateSnapshot(entry.Version, entry.Payload);
                }
            }
        }

        public void RestoreSnapshot(int version, string payload)
        {
            var entries = ParseSnapshot(version, payload);
            for (var index = 0; index < _states.Count; index++)
            {
                if (_states[index] is IStateSnapshotParticipant participant)
                {
                    var entry = entries[index];
                    participant.RestoreSnapshot(entry.Version, entry.Payload);
                }
            }
        }

        private List<ParallelSnapshotEntry> ParseSnapshot(int version, string payload)
        {
            if (version != SnapshotVersion)
                throw new InvalidOperationException("Unsupported parallel HFSM state snapshot version.");

            var parts = (payload ?? string.Empty).Split(';');
            if (parts.Length != _states.Count)
                throw new InvalidOperationException("Parallel HFSM state snapshot item count does not match.");

            var result = new List<ParallelSnapshotEntry>(_states.Count);
            for (var index = 0; index < parts.Length; index++)
            {
                var participant = _states[index] as IStateSnapshotParticipant;
                if (participant == null)
                {
                    if (!string.Equals(parts[index], "-", StringComparison.Ordinal))
                        throw new InvalidOperationException("Parallel HFSM snapshot contains payload for a stateless child.");
                    result.Add(default);
                    continue;
                }

                var separator = parts[index].IndexOf('|');
                if (separator <= 0 || !int.TryParse(
                        parts[index].Substring(0, separator),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var childVersion))
                {
                    throw new InvalidOperationException("Parallel HFSM snapshot child header is invalid.");
                }

                try
                {
                    var childPayload = Encoding.UTF8.GetString(
                        Convert.FromBase64String(parts[index].Substring(separator + 1)));
                    result.Add(new ParallelSnapshotEntry(childVersion, childPayload));
                }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException("Parallel HFSM snapshot child payload is invalid.", exception);
                }
            }
            return result;
        }

        private readonly struct ParallelSnapshotEntry
        {
            public ParallelSnapshotEntry(int version, string payload)
            {
                Version = version;
                Payload = payload;
            }

            public int Version { get; }
            public string Payload { get; }
        }
    }

}
