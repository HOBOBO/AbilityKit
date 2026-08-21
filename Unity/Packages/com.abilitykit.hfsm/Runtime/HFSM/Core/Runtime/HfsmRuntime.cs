#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.HFSM
{
    /// <summary>
    /// Deterministic hierarchical state-machine runtime. The compiled runtime owns copies of all
    /// semantic definition values, so later authoring-model mutations cannot change execution.
    /// </summary>
    public sealed class HfsmRuntime<TOwner>
    {
        private readonly TOwner _owner;
        private readonly HfsmDefinition _definition;
        private readonly long _definitionHash;
        private readonly Dictionary<string, CompiledMachine> _machines;
        private readonly CompiledMachine _root;
        private readonly List<IHfsmRuntimeObserver> _observers = new List<IHfsmRuntimeObserver>();
        private bool _initialized;
        private bool _faulted;
        private int _currentFrame;
        private long _currentTimeRaw;

        public HfsmRuntime(
            TOwner owner,
            HfsmDefinition definition,
            HfsmRuntimeBindings<TOwner> bindings)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            HfsmDefinitionValidator.ValidateOrThrow(definition);
            _owner = owner;
            _definition = CloneDefinition(definition);
            _definitionHash = _definition.ComputeDefinitionHash();
            _machines = Compile(_definition, bindings);
            _root = _machines[_definition.RootMachineId];
        }

        public long DefinitionHash => _definitionHash;

        public bool IsInitialized => _initialized;

        public bool IsFaulted => _faulted;

        public int CurrentFrame => _currentFrame;

        public Fixed64 CurrentTime => Fixed64.FromRaw(_currentTimeRaw);

        public void AddObserver(IHfsmRuntimeObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }

        public bool RemoveObserver(IHfsmRuntimeObserver observer)
        {
            return observer != null && _observers.Remove(observer);
        }

        public void Initialize(int frame, Fixed64 time)
        {
            EnsureNotFaulted();
            if (_initialized) throw new InvalidOperationException("The HFSM runtime is already initialized.");
            ValidateClock(frame, time.RawValue, allowSameFrame: true);

            _currentFrame = frame;
            _currentTimeRaw = time.RawValue;
            var context = new HfsmTickContext(frame, time, Fixed64.Zero);
            try
            {
                _initialized = true;
                EnterMachine(_root, in context);
                Notify(HfsmRuntimeEventType.Initialized, in context, _root.Id, _root.ActiveStateId, string.Empty);
            }
            catch
            {
                Fault(in context);
                throw;
            }
        }

        public void Tick(int frame, Fixed64 time)
        {
            EnsureOperational();
            ValidateClock(frame, time.RawValue, allowSameFrame: false);

            var delta = Fixed64.FromRaw(checked(time.RawValue - _currentTimeRaw));
            _currentFrame = frame;
            _currentTimeRaw = time.RawValue;
            var context = new HfsmTickContext(frame, time, delta);
            try
            {
                TickMachine(_root, in context);
            }
            catch
            {
                Fault(in context);
                throw;
            }
        }

        /// <summary>
        /// Dispatches a trigger from the root toward the active leaf. Returns true when a
        /// transition accepted the trigger, including when it became pending.
        /// </summary>
        public bool Trigger(string triggerId)
        {
            EnsureOperational();
            if (string.IsNullOrWhiteSpace(triggerId))
                throw new ArgumentException("HFSM trigger id is required.", nameof(triggerId));

            var context = new HfsmTickContext(
                _currentFrame,
                Fixed64.FromRaw(_currentTimeRaw),
                Fixed64.Zero);
            try
            {
                return TriggerMachine(_root, triggerId, in context);
            }
            catch
            {
                Fault(in context);
                throw;
            }
        }

        public void Shutdown()
        {
            EnsureOperational();
            var context = new HfsmTickContext(
                _currentFrame,
                Fixed64.FromRaw(_currentTimeRaw),
                Fixed64.Zero);
            try
            {
                ExitMachine(_root, in context);
                _initialized = false;
                Notify(HfsmRuntimeEventType.Shutdown, in context, _root.Id, string.Empty, string.Empty);
            }
            catch
            {
                Fault(in context);
                throw;
            }
        }

        public IReadOnlyList<string> GetActivePath()
        {
            if (!_initialized) return Array.Empty<string>();

            var path = new List<string>();
            var machine = _root;
            while (!string.IsNullOrEmpty(machine.ActiveStateId))
            {
                path.Add(machine.Id + "/" + machine.ActiveStateId);
                var state = machine.StatesById[machine.ActiveStateId];
                if (state.ChildMachine == null) break;
                machine = state.ChildMachine;
            }

            return path.AsReadOnly();
        }

        public HfsmRuntimeSnapshot CaptureSnapshot()
        {
            EnsureNotFaulted();
            var snapshot = new HfsmRuntimeSnapshot
            {
                DefinitionHash = _definitionHash,
                Initialized = _initialized,
                Frame = _currentFrame,
                TimeRaw = _currentTimeRaw,
            };

            var machineIds = new List<string>(_machines.Keys);
            machineIds.Sort(StringComparer.Ordinal);
            for (var machineIndex = 0; machineIndex < machineIds.Count; machineIndex++)
            {
                var machine = _machines[machineIds[machineIndex]];
                snapshot.Machines.Add(new HfsmMachineRuntimeSnapshot
                {
                    MachineId = machine.Id,
                    ActiveStateId = machine.ActiveStateId,
                    RememberedStateId = machine.RememberedStateId,
                    PendingTransitionId = machine.PendingTransitionId,
                    ActiveSinceRaw = machine.ActiveSinceRaw,
                });

                var stateIds = new List<string>(machine.StatesById.Keys);
                stateIds.Sort(StringComparer.Ordinal);
                for (var stateIndex = 0; stateIndex < stateIds.Count; stateIndex++)
                {
                    var state = machine.StatesById[stateIds[stateIndex]];
                    if (state.Behavior is not IHfsmStateSnapshotParticipant participant) continue;
                    snapshot.States.Add(new HfsmStateRuntimeSnapshot
                    {
                        MachineId = machine.Id,
                        StateId = state.Id,
                        PayloadVersion = participant.SnapshotVersion,
                        Payload = participant.CaptureSnapshot() ?? string.Empty,
                    });
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Restores runtime structure without enter/exit callbacks. All structural and participant
        /// payload validation runs before mutation. A participant throwing during Restore faults the runtime.
        /// </summary>
        public void RestoreSnapshot(HfsmRuntimeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var restore = ValidateSnapshot(snapshot);
            var context = new HfsmTickContext(
                snapshot.Frame,
                Fixed64.FromRaw(snapshot.TimeRaw),
                Fixed64.Zero);

            try
            {
                for (var index = 0; index < restore.StatePayloads.Count; index++)
                {
                    var payload = restore.StatePayloads[index];
                    payload.Participant.RestoreSnapshot(payload.Snapshot.PayloadVersion, payload.Snapshot.Payload);
                }

                foreach (var pair in restore.Machines)
                {
                    var machine = _machines[pair.Key];
                    var source = pair.Value;
                    machine.ActiveStateId = source.ActiveStateId;
                    machine.RememberedStateId = source.RememberedStateId;
                    machine.PendingTransitionId = source.PendingTransitionId;
                    machine.ActiveSinceRaw = source.ActiveSinceRaw;
                }

                _currentFrame = snapshot.Frame;
                _currentTimeRaw = snapshot.TimeRaw;
                _initialized = snapshot.Initialized;
                _faulted = false;
                Notify(HfsmRuntimeEventType.Restored, in context, _root.Id, _root.ActiveStateId, string.Empty);
            }
            catch
            {
                Fault(in context);
                throw;
            }
        }

        private void TickMachine(CompiledMachine machine, in HfsmTickContext context)
        {
            var state = machine.StatesById[machine.ActiveStateId];
            if (!string.IsNullOrEmpty(machine.PendingTransitionId))
            {
                var forced = SelectTransition(machine, state, triggerId: string.Empty, forceOnly: true, in context);
                if (forced != null)
                {
                    PerformTransition(machine, state, forced, string.Empty, in context);
                    state = machine.StatesById[machine.ActiveStateId];
                }
                else
                {
                    // Preserve UnityHFSM's delayed-exit frame semantics: the source state gets its
                    // final logic step before approval is checked, and the target is not ticked yet.
                    state.Behavior.OnTick(_owner, in context);
                    if (state.ChildMachine != null) TickMachine(state.ChildMachine, in context);

                    if (state.Behavior.CanExit(_owner, in context))
                    {
                        var pending = machine.TransitionsById[machine.PendingTransitionId];
                        PerformTransition(machine, state, pending, string.Empty, in context);
                    }

                    return;
                }
            }
            else
            {
                var transition = SelectTransition(machine, state, triggerId: string.Empty, forceOnly: false, in context);
                if (transition != null)
                {
                    RequestTransition(machine, state, transition, string.Empty, in context);
                    state = machine.StatesById[machine.ActiveStateId];
                }
            }

            state.Behavior.OnTick(_owner, in context);
            if (state.ChildMachine != null) TickMachine(state.ChildMachine, in context);
        }

        private bool TriggerMachine(CompiledMachine machine, string triggerId, in HfsmTickContext context)
        {
            var state = machine.StatesById[machine.ActiveStateId];
            var forceOnly = !string.IsNullOrEmpty(machine.PendingTransitionId);
            var transition = SelectTransition(machine, state, triggerId, forceOnly, in context);
            if (transition != null)
            {
                RequestTransition(machine, state, transition, triggerId, in context);
                return true;
            }

            return state.ChildMachine != null && TriggerMachine(state.ChildMachine, triggerId, in context);
        }

        private CompiledTransition? SelectTransition(
            CompiledMachine machine,
            CompiledState state,
            string triggerId,
            bool forceOnly,
            in HfsmTickContext context)
        {
            List<CompiledTransition>? any;
            List<CompiledTransition>? local;
            if (string.IsNullOrEmpty(triggerId))
            {
                any = machine.TickFromAny;
                local = state.TickTransitions;
            }
            else
            {
                machine.TriggerFromAny.TryGetValue(triggerId, out any);
                state.TriggerTransitions.TryGetValue(triggerId, out local);
            }

            return SelectFromList(machine, state, any, triggerId, forceOnly, in context)
                ?? SelectFromList(machine, state, local, triggerId, forceOnly, in context);
        }

        private CompiledTransition? SelectFromList(
            CompiledMachine machine,
            CompiledState state,
            List<CompiledTransition>? transitions,
            string triggerId,
            bool forceOnly,
            in HfsmTickContext context)
        {
            if (transitions == null) return null;
            for (var index = 0; index < transitions.Count; index++)
            {
                var transition = transitions[index];
                if (forceOnly && !transition.Definition.ForceImmediate) continue;

                var activeDurationRaw = checked(context.TimeRaw - machine.ActiveSinceRaw);
                if (activeDurationRaw < transition.Definition.MinimumActiveDurationRaw) continue;

                var transitionContext = new HfsmTransitionContext(
                    context,
                    machine.Id,
                    state.Id,
                    transition.Definition,
                    triggerId,
                    machine.ActiveSinceRaw);
                if (transition.Condition == null || transition.Condition.Evaluate(_owner, in transitionContext))
                    return transition;
            }

            return null;
        }

        private void RequestTransition(
            CompiledMachine machine,
            CompiledState state,
            CompiledTransition transition,
            string triggerId,
            in HfsmTickContext context)
        {
            if (transition.Definition.ForceImmediate ||
                !state.Definition.RequiresExitApproval)
            {
                PerformTransition(machine, state, transition, triggerId, in context);
                return;
            }

            machine.PendingTransitionId = transition.Definition.Id;
            Notify(HfsmRuntimeEventType.ExitRequested, in context, machine.Id, state.Id, transition.Definition.Id);
            state.Behavior.OnExitRequested(_owner, in context);
            if (state.Behavior.CanExit(_owner, in context))
            {
                PerformTransition(machine, state, transition, triggerId, in context);
            }
        }

        private void PerformTransition(
            CompiledMachine machine,
            CompiledState source,
            CompiledTransition transition,
            string triggerId,
            in HfsmTickContext context)
        {
            var transitionContext = new HfsmTransitionContext(
                context,
                machine.Id,
                source.Id,
                transition.Definition,
                triggerId,
                machine.ActiveSinceRaw);

            transition.Action?.BeforeTransition(_owner, in transitionContext);
            if (source.ChildMachine != null) ExitMachine(source.ChildMachine, in context);
            source.Behavior.OnExit(_owner, in context);
            Notify(HfsmRuntimeEventType.StateExited, in context, machine.Id, source.Id, transition.Definition.Id);

            machine.PendingTransitionId = string.Empty;
            machine.ActiveStateId = transition.Definition.ToStateId;
            machine.RememberedStateId = machine.ActiveStateId;
            machine.ActiveSinceRaw = context.TimeRaw;

            var target = machine.StatesById[machine.ActiveStateId];
            target.Behavior.OnEnter(_owner, in context);
            Notify(HfsmRuntimeEventType.StateEntered, in context, machine.Id, target.Id, transition.Definition.Id);
            if (target.ChildMachine != null) EnterMachine(target.ChildMachine, in context);

            transition.Action?.AfterTransition(_owner, in transitionContext);
            Notify(HfsmRuntimeEventType.TransitionCompleted, in context,
                machine.Id, target.Id, transition.Definition.Id);
        }

        private void EnterMachine(CompiledMachine machine, in HfsmTickContext context)
        {
            var stateId = machine.Definition.RememberLastState &&
                          !string.IsNullOrEmpty(machine.RememberedStateId)
                ? machine.RememberedStateId
                : machine.Definition.InitialStateId;

            machine.ActiveStateId = stateId;
            machine.PendingTransitionId = string.Empty;
            machine.ActiveSinceRaw = context.TimeRaw;
            var state = machine.StatesById[stateId];
            state.Behavior.OnEnter(_owner, in context);
            Notify(HfsmRuntimeEventType.StateEntered, in context, machine.Id, state.Id, string.Empty);
            if (state.ChildMachine != null) EnterMachine(state.ChildMachine, in context);
        }

        private void ExitMachine(CompiledMachine machine, in HfsmTickContext context)
        {
            if (string.IsNullOrEmpty(machine.ActiveStateId)) return;
            var state = machine.StatesById[machine.ActiveStateId];
            if (state.ChildMachine != null) ExitMachine(state.ChildMachine, in context);
            state.Behavior.OnExit(_owner, in context);
            Notify(HfsmRuntimeEventType.StateExited, in context, machine.Id, state.Id, string.Empty);
            machine.RememberedStateId = state.Id;
            machine.ActiveStateId = string.Empty;
            machine.PendingTransitionId = string.Empty;
            machine.ActiveSinceRaw = 0L;
        }

        private SnapshotRestorePlan ValidateSnapshot(HfsmRuntimeSnapshot snapshot)
        {
            if (snapshot.SnapshotVersion != HfsmRuntimeSnapshot.CurrentSnapshotVersion)
                throw new InvalidOperationException($"Unsupported HFSM snapshot version '{snapshot.SnapshotVersion}'.");
            if (snapshot.DefinitionHash != _definitionHash)
                throw new InvalidOperationException("HFSM snapshot definition hash does not match the runtime definition.");
            if (snapshot.Frame < 0 || snapshot.TimeRaw < 0)
                throw new InvalidOperationException("HFSM snapshot clock values cannot be negative.");

            var snapshots = snapshot.Machines ?? new List<HfsmMachineRuntimeSnapshot>();
            if (snapshots.Count != _machines.Count)
                throw new InvalidOperationException("HFSM snapshot machine count does not match the definition.");

            var machines = new Dictionary<string, HfsmMachineRuntimeSnapshot>(StringComparer.Ordinal);
            for (var index = 0; index < snapshots.Count; index++)
            {
                var item = snapshots[index];
                if (item == null || string.IsNullOrEmpty(item.MachineId) || !machines.TryAdd(item.MachineId, item))
                    throw new InvalidOperationException("HFSM snapshot contains a null or duplicate machine.");
                if (!_machines.ContainsKey(item.MachineId))
                    throw new InvalidOperationException($"HFSM snapshot contains unknown machine '{item.MachineId}'.");
            }

            foreach (var pair in _machines)
            {
                var runtime = pair.Value;
                var item = machines[pair.Key];
                if (!string.IsNullOrEmpty(item.ActiveStateId) && !runtime.StatesById.ContainsKey(item.ActiveStateId))
                    throw new InvalidOperationException($"HFSM snapshot machine '{pair.Key}' has an unknown active state.");
                if (!string.IsNullOrEmpty(item.RememberedStateId) && !runtime.StatesById.ContainsKey(item.RememberedStateId))
                    throw new InvalidOperationException($"HFSM snapshot machine '{pair.Key}' has an unknown remembered state.");
                if (!string.IsNullOrEmpty(item.PendingTransitionId))
                {
                    if (string.IsNullOrEmpty(item.ActiveStateId) ||
                        !runtime.TransitionsById.TryGetValue(item.PendingTransitionId, out var pending) ||
                        pending.Definition.ForceImmediate ||
                        (!pending.Definition.FromAnyState &&
                         !string.Equals(pending.Definition.FromStateId, item.ActiveStateId, StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException($"HFSM snapshot machine '{pair.Key}' has an invalid pending transition.");
                    }
                }

                if (string.IsNullOrEmpty(item.ActiveStateId))
                {
                    if (!string.IsNullOrEmpty(item.PendingTransitionId) || item.ActiveSinceRaw != 0L)
                        throw new InvalidOperationException($"Inactive HFSM snapshot machine '{pair.Key}' contains active state data.");
                }
                else if (item.ActiveSinceRaw < 0 || item.ActiveSinceRaw > snapshot.TimeRaw)
                {
                    throw new InvalidOperationException($"HFSM snapshot machine '{pair.Key}' has an invalid active timestamp.");
                }
            }

            ValidateSnapshotHierarchy(_root, expectedActive: snapshot.Initialized, machines);

            var statePayloads = snapshot.States ?? new List<HfsmStateRuntimeSnapshot>();
            var payloadsByKey = new Dictionary<string, HfsmStateRuntimeSnapshot>(StringComparer.Ordinal);
            for (var index = 0; index < statePayloads.Count; index++)
            {
                var payload = statePayloads[index];
                if (payload == null || !payloadsByKey.TryAdd(StateKey(payload.MachineId, payload.StateId), payload))
                    throw new InvalidOperationException("HFSM snapshot contains a null or duplicate state payload.");
            }

            var payloadPlan = new List<StatePayloadRestore>();
            foreach (var machinePair in _machines)
            {
                foreach (var statePair in machinePair.Value.StatesById)
                {
                    var key = StateKey(machinePair.Key, statePair.Key);
                    if (statePair.Value.Behavior is IHfsmStateSnapshotParticipant participant)
                    {
                        if (!payloadsByKey.Remove(key, out var payload))
                            throw new InvalidOperationException($"HFSM snapshot is missing state payload '{machinePair.Key}/{statePair.Key}'.");
                        participant.ValidateSnapshot(payload.PayloadVersion, payload.Payload ?? string.Empty);
                        payload.Payload ??= string.Empty;
                        payloadPlan.Add(new StatePayloadRestore(participant, payload));
                    }
                }
            }

            if (payloadsByKey.Count != 0)
                throw new InvalidOperationException("HFSM snapshot contains a payload for a non-stateful or unknown state.");

            return new SnapshotRestorePlan(machines, payloadPlan);
        }

        private static void ValidateSnapshotHierarchy(
            CompiledMachine machine,
            bool expectedActive,
            Dictionary<string, HfsmMachineRuntimeSnapshot> snapshots)
        {
            var machineSnapshot = snapshots[machine.Id];
            var isActive = !string.IsNullOrEmpty(machineSnapshot.ActiveStateId);
            if (isActive != expectedActive)
                throw new InvalidOperationException($"HFSM snapshot hierarchy activity mismatch at machine '{machine.Id}'.");

            foreach (var statePair in machine.StatesById)
            {
                if (statePair.Value.ChildMachine == null) continue;
                var childShouldBeActive = isActive &&
                                          string.Equals(machineSnapshot.ActiveStateId, statePair.Key, StringComparison.Ordinal);
                ValidateSnapshotHierarchy(statePair.Value.ChildMachine, childShouldBeActive, snapshots);
            }
        }

        private void ValidateClock(int frame, long timeRaw, bool allowSameFrame)
        {
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (timeRaw < 0) throw new ArgumentOutOfRangeException(nameof(timeRaw));
            if (!_initialized) return;
            if (allowSameFrame ? frame < _currentFrame : frame <= _currentFrame)
                throw new InvalidOperationException("HFSM frames must advance monotonically.");
            if (timeRaw < _currentTimeRaw)
                throw new InvalidOperationException("HFSM time cannot move backwards.");
        }

        private void EnsureOperational()
        {
            EnsureNotFaulted();
            if (!_initialized) throw new InvalidOperationException("The HFSM runtime is not initialized.");
        }

        private void EnsureNotFaulted()
        {
            if (_faulted) throw new HfsmRuntimeFaultedException();
        }

        private void Fault(in HfsmTickContext context)
        {
            _faulted = true;
            Notify(HfsmRuntimeEventType.Faulted, in context, _root.Id, _root.ActiveStateId, string.Empty);
        }

        private void Notify(
            HfsmRuntimeEventType type,
            in HfsmTickContext context,
            string machineId,
            string stateId,
            string transitionId)
        {
            if (_observers.Count == 0) return;
            var runtimeEvent = new HfsmRuntimeEvent(type, context, machineId, stateId, transitionId);
            var observers = _observers.ToArray();
            for (var index = 0; index < observers.Length; index++)
            {
                try
                {
                    observers[index].OnRuntimeEvent(in runtimeEvent);
                }
                catch
                {
                    // Diagnostics must not affect deterministic simulation.
                }
            }
        }

        private static Dictionary<string, CompiledMachine> Compile(
            HfsmDefinition definition,
            HfsmRuntimeBindings<TOwner> bindings)
        {
            var machines = new Dictionary<string, CompiledMachine>(StringComparer.Ordinal);
            for (var machineIndex = 0; machineIndex < definition.Machines.Count; machineIndex++)
            {
                var machineDefinition = definition.Machines[machineIndex];
                var machine = new CompiledMachine(machineDefinition);
                machines.Add(machine.Id, machine);
                for (var stateIndex = 0; stateIndex < machineDefinition.States.Count; stateIndex++)
                {
                    var stateDefinition = machineDefinition.States[stateIndex];
                    machine.StatesById.Add(
                        stateDefinition.Id,
                        new CompiledState(stateDefinition, bindings.CreateState(stateDefinition.BehaviorKey)));
                }
            }

            foreach (var machinePair in machines)
            {
                var machine = machinePair.Value;
                foreach (var statePair in machine.StatesById)
                {
                    if (!string.IsNullOrEmpty(statePair.Value.Definition.ChildMachineId))
                        statePair.Value.ChildMachine = machines[statePair.Value.Definition.ChildMachineId];
                }

                for (var index = 0; index < machine.Definition.Transitions.Count; index++)
                {
                    var definitionItem = machine.Definition.Transitions[index];
                    var transition = new CompiledTransition(
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

        private static void AddTransition(CompiledMachine machine, CompiledTransition transition)
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
                triggerList = new List<CompiledTransition>();
                triggers.Add(definition.TriggerId, triggerList);
            }

            triggerList.Add(transition);
        }

        private static void SortTransitions(List<CompiledTransition> transitions)
        {
            transitions.Sort((left, right) => HfsmDefinition.CompareTransitions(
                left.Definition,
                right.Definition));
        }

        private static HfsmDefinition CloneDefinition(HfsmDefinition source)
        {
            var clone = new HfsmDefinition
            {
                DefinitionId = source.DefinitionId ?? string.Empty,
                FormatVersion = source.FormatVersion,
                RootMachineId = source.RootMachineId ?? string.Empty,
            };

            for (var machineIndex = 0; machineIndex < source.Machines.Count; machineIndex++)
            {
                var sourceMachine = source.Machines[machineIndex];
                var machine = new HfsmMachineDefinition
                {
                    Id = sourceMachine.Id ?? string.Empty,
                    InitialStateId = sourceMachine.InitialStateId ?? string.Empty,
                    RememberLastState = sourceMachine.RememberLastState,
                };

                for (var stateIndex = 0; stateIndex < sourceMachine.States.Count; stateIndex++)
                {
                    var state = sourceMachine.States[stateIndex];
                    machine.States.Add(new HfsmStateDefinition
                    {
                        Id = state.Id ?? string.Empty,
                        BehaviorKey = state.BehaviorKey ?? string.Empty,
                        ChildMachineId = state.ChildMachineId ?? string.Empty,
                        RequiresExitApproval = state.RequiresExitApproval,
                    });
                }

                for (var transitionIndex = 0; transitionIndex < sourceMachine.Transitions.Count; transitionIndex++)
                {
                    var transition = sourceMachine.Transitions[transitionIndex];
                    machine.Transitions.Add(new HfsmTransitionDefinition
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
                        MinimumActiveDurationRaw = transition.MinimumActiveDurationRaw,
                    });
                }

                clone.Machines.Add(machine);
            }

            return clone;
        }

        private static string StateKey(string machineId, string stateId) => machineId + "\0" + stateId;

        private sealed class CompiledMachine
        {
            public CompiledMachine(HfsmMachineDefinition definition)
            {
                Definition = definition;
            }

            public string Id => Definition.Id;
            public HfsmMachineDefinition Definition { get; }
            public Dictionary<string, CompiledState> StatesById { get; } =
                new Dictionary<string, CompiledState>(StringComparer.Ordinal);
            public Dictionary<string, CompiledTransition> TransitionsById { get; } =
                new Dictionary<string, CompiledTransition>(StringComparer.Ordinal);
            public List<CompiledTransition> TickFromAny { get; } = new List<CompiledTransition>();
            public Dictionary<string, List<CompiledTransition>> TriggerFromAny { get; } =
                new Dictionary<string, List<CompiledTransition>>(StringComparer.Ordinal);
            public string ActiveStateId { get; set; } = string.Empty;
            public string RememberedStateId { get; set; } = string.Empty;
            public string PendingTransitionId { get; set; } = string.Empty;
            public long ActiveSinceRaw { get; set; }
        }

        private sealed class CompiledState
        {
            public CompiledState(HfsmStateDefinition definition, IHfsmState<TOwner> behavior)
            {
                Definition = definition;
                Behavior = behavior;
            }

            public string Id => Definition.Id;
            public HfsmStateDefinition Definition { get; }
            public IHfsmState<TOwner> Behavior { get; }
            public CompiledMachine? ChildMachine { get; set; }
            public List<CompiledTransition> TickTransitions { get; } = new List<CompiledTransition>();
            public Dictionary<string, List<CompiledTransition>> TriggerTransitions { get; } =
                new Dictionary<string, List<CompiledTransition>>(StringComparer.Ordinal);
        }

        private sealed class CompiledTransition
        {
            public CompiledTransition(
                HfsmTransitionDefinition definition,
                IHfsmTransitionCondition<TOwner>? condition,
                IHfsmTransitionAction<TOwner>? action)
            {
                Definition = definition;
                Condition = condition;
                Action = action;
            }

            public HfsmTransitionDefinition Definition { get; }
            public IHfsmTransitionCondition<TOwner>? Condition { get; }
            public IHfsmTransitionAction<TOwner>? Action { get; }
        }

        private sealed class StatePayloadRestore
        {
            public StatePayloadRestore(
                IHfsmStateSnapshotParticipant participant,
                HfsmStateRuntimeSnapshot snapshot)
            {
                Participant = participant;
                Snapshot = snapshot;
            }

            public IHfsmStateSnapshotParticipant Participant { get; }
            public HfsmStateRuntimeSnapshot Snapshot { get; }
        }

        private sealed class SnapshotRestorePlan
        {
            public SnapshotRestorePlan(
                Dictionary<string, HfsmMachineRuntimeSnapshot> machines,
                List<StatePayloadRestore> statePayloads)
            {
                Machines = machines;
                StatePayloads = statePayloads;
            }

            public Dictionary<string, HfsmMachineRuntimeSnapshot> Machines { get; }
            public List<StatePayloadRestore> StatePayloads { get; }
        }
    }
}
