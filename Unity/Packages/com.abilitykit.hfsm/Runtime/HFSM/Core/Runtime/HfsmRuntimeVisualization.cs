#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Visualization;

namespace AbilityKit.HFSM.Runtime
{
    /// <summary>
    /// Projects a deterministic <see cref="StateMachineRuntime{TOwner}"/> onto the editor
    /// visualization model <see cref="FsmSnapshot"/>: static structure comes from the definition,
    /// live state from <see cref="RuntimeSnapshot"/>, and transition history / enter counts from the
    /// runtime's <see cref="IRuntimeObserver"/> events. Kept in Core so it stays Unity-free and
    /// directly testable in .NET.
    /// </summary>
    /// <remarks>
    /// Path convention (shared with <see cref="StateMachineRuntime{TOwner}.GetActivePath"/>):
    /// a state node's path is <c>{machineId}/{stateId}</c> and its parent is the owning machine id;
    /// a machine node's path is its machine id and its parent is the state that owns it.
    /// </remarks>
    internal sealed class HfsmRuntimeVisualization : IVisualizationProvider, IRuntimeObserver
    {
        private const int DefaultHistoryCapacity = 256;

        private readonly StateMachineDefinition _definition;
        private readonly Func<RuntimeSnapshot?> _capture;
        private readonly Func<long> _timeRaw;
        private readonly Dictionary<string, MachineDefinition> _machineById =
            new Dictionary<string, MachineDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, TransitionDefinition> _transitionByKey =
            new Dictionary<string, TransitionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _ownerStatePathByMachineId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _enterCountByPath =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _lastTransitionTimeByPath =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastExitedStateIdByMachine =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<StateTransitionRecord> _history = new List<StateTransitionRecord>();
        private readonly FsmSnapshot _snapshot = new FsmSnapshot();

        internal HfsmRuntimeVisualization(
            StateMachineDefinition definition,
            Func<RuntimeSnapshot?> capture,
            Func<long> timeRaw)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _timeRaw = timeRaw ?? throw new ArgumentNullException(nameof(timeRaw));
            IndexStaticStructure();
        }

        internal int HistoryCapacity { get; set; } = DefaultHistoryCapacity;

        public FsmSnapshot GetSnapshot()
        {
            RuntimeSnapshot? runtime = null;
            try
            {
                runtime = _capture();
            }
            catch (InvalidOperationException)
            {
                // An uninitialized or faulted runtime has no live state; the static structure still renders.
            }

            var now = Seconds(_timeRaw());

            _snapshot.states.Clear();
            _snapshot.transitions.Clear();
            _snapshot.parameters.Clear();
            _snapshot.behaviorNodes.Clear();
            _snapshot.activeStatePaths.Clear();
            _snapshot.pendingStatePaths.Clear();
            _snapshot.exitingStatePaths.Clear();
            _snapshot.history.Clear();
            _snapshot.snapshotTime = now;
            _snapshot.frame = runtime?.Frame ?? 0;
            _snapshot.timeRaw = runtime?.TimeRaw ?? 0L;
            _snapshot.definitionHash = runtime?.DefinitionHash ?? 0L;

            var activeStateIdByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
            var pendingTransitionIdByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
            var activeSinceRawByMachine = new Dictionary<string, long>(StringComparer.Ordinal);
            if (runtime != null && runtime.Machines != null)
            {
                for (var i = 0; i < runtime.Machines.Count; i++)
                {
                    var machine = runtime.Machines[i];
                    if (machine == null || string.IsNullOrEmpty(machine.MachineId)) continue;
                    activeStateIdByMachine[machine.MachineId] = machine.ActiveStateId ?? string.Empty;
                    pendingTransitionIdByMachine[machine.MachineId] = machine.PendingTransitionId ?? string.Empty;
                    activeSinceRawByMachine[machine.MachineId] = machine.ActiveSinceRaw;
                }
            }

            var activeMachineIds = ResolveActiveMachines(activeStateIdByMachine, out var activePaths);
            var pendingTargets = ResolvePendingTargets(activeStateIdByMachine, pendingTransitionIdByMachine, out var exitingPaths);

            for (var i = 0; i < activePaths.Count; i++)
                _snapshot.activeStatePaths.Add(activePaths[i]);
            for (var i = 0; i < pendingTargets.Count; i++)
                _snapshot.pendingStatePaths.Add(pendingTargets[i]);
            for (var i = 0; i < exitingPaths.Count; i++)
                _snapshot.exitingStatePaths.Add(exitingPaths[i]);

            AddStateNodes(now, activePaths, pendingTargets, exitingPaths, activeSinceRawByMachine, activeStateIdByMachine);
            AddTransitionNodes(activeMachineIds, activeStateIdByMachine);

            for (var i = 0; i < _history.Count; i++)
                _snapshot.history.Add(_history[i]);

            return _snapshot;
        }

        public IEnumerable<string> GetActiveStatePaths() => GetSnapshot().activeStatePaths;

        public IEnumerable<ParameterInfo> GetParameters() => GetSnapshot().parameters;

        public IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure()
        {
            var states = GetSnapshot().states;
            for (var i = 0; i < states.Count; i++)
                yield return (states[i].name, states[i].parentPath, states[i].isStateMachine);
        }

        public IEnumerable<TransitionInfo> GetTransitions() => GetSnapshot().transitions;

        public void RecordTransition(string fromPath, string toPath, string trigger)
        {
            RecordTransitionCore(fromPath, toPath, trigger, Seconds(_timeRaw()));
        }

        public IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50)
        {
            var now = Seconds(_timeRaw());
            var count = Math.Min(Math.Max(0, maxCount), _history.Count);
            for (var index = _history.Count - count; index < _history.Count; index++)
            {
                var record = _history[index];
                record.timeAgo = now - record.timestamp;
                yield return record;
            }
        }

        void IRuntimeObserver.OnRuntimeEvent(in RuntimeEvent runtimeEvent)
        {
            var now = Seconds(runtimeEvent.Tick.TimeRaw);
            switch (runtimeEvent.Type)
            {
                case RuntimeEventType.StateEntered:
                {
                    var path = StatePath(runtimeEvent.MachineId, runtimeEvent.StateId);
                    _enterCountByPath[path] = (_enterCountByPath.TryGetValue(path, out var count) ? count : 0) + 1;
                    break;
                }

                case RuntimeEventType.StateExited:
                    _lastExitedStateIdByMachine[runtimeEvent.MachineId] = runtimeEvent.StateId;
                    break;

                case RuntimeEventType.TransitionCompleted:
                {
                    var toPath = StatePath(runtimeEvent.MachineId, runtimeEvent.StateId);
                    var fromStateId = _lastExitedStateIdByMachine.TryGetValue(runtimeEvent.MachineId, out var from)
                        ? from
                        : string.Empty;
                    var fromPath = string.IsNullOrEmpty(fromStateId)
                        ? string.Empty
                        : StatePath(runtimeEvent.MachineId, fromStateId);
                    RecordTransitionCore(fromPath, toPath, runtimeEvent.TriggerId, now);
                    _lastTransitionTimeByPath[toPath] = now;
                    break;
                }
            }
        }

        private void RecordTransitionCore(string fromPath, string toPath, string trigger, float timestamp)
        {
            if (_history.Count >= Math.Max(1, HistoryCapacity))
                _history.RemoveAt(0);

            _history.Add(new StateTransitionRecord
            {
                fromPath = fromPath ?? string.Empty,
                toPath = toPath ?? string.Empty,
                trigger = trigger ?? string.Empty,
                timestamp = timestamp,
                timeAgo = 0f,
            });
        }

        private void AddStateNodes(
            float now,
            List<string> activePaths,
            List<string> pendingTargets,
            List<string> exitingPaths,
            Dictionary<string, long> activeSinceRawByMachine,
            Dictionary<string, string> activeStateIdByMachine)
        {
            var machines = _definition.Machines;
            if (machines == null) return;

            for (var i = 0; i < machines.Count; i++)
            {
                var machine = machines[i];
                if (machine == null || string.IsNullOrEmpty(machine.Id)) continue;

                var machineParent = _ownerStatePathByMachineId.TryGetValue(machine.Id, out var owner)
                    ? owner
                    : string.Empty;
                _snapshot.states.Add(new StateNodeInfo
                {
                    name = machine.Id,
                    path = machine.Id,
                    parentPath = machineParent,
                    isStateMachine = true,
                    nestingLevel = Depth(machineParent),
                });

                if (machine.States == null) continue;
                var activeStateId = activeStateIdByMachine.TryGetValue(machine.Id, out var active)
                    ? active
                    : string.Empty;
                var hasActiveSince = activeSinceRawByMachine.TryGetValue(machine.Id, out var activeSinceRaw);

                for (var s = 0; s < machine.States.Count; s++)
                {
                    var state = machine.States[s];
                    if (state == null || string.IsNullOrEmpty(state.Id)) continue;

                    var path = StatePath(machine.Id, state.Id);
                    var isActive = activePaths.Contains(path);
                    var duration = 0f;
                    if (isActive && hasActiveSince)
                        duration = Math.Max(0f, now - Seconds(activeSinceRaw));

                    _snapshot.states.Add(new StateNodeInfo
                    {
                        name = state.Id,
                        path = path,
                        parentPath = machine.Id,
                        isStateMachine = !string.IsNullOrEmpty(state.ChildMachineId),
                        isActive = isActive,
                        isEntering = pendingTargets.Contains(path),
                        isExiting = exitingPaths.Contains(path),
                        activeDuration = duration,
                        enterCount = _enterCountByPath.TryGetValue(path, out var count) ? count : 0,
                        nestingLevel = Depth(path),
                    });
                }
            }
        }

        private void AddTransitionNodes(
            HashSet<string> activeMachineIds,
            Dictionary<string, string> activeStateIdByMachine)
        {
            var machines = _definition.Machines;
            if (machines == null) return;

            for (var i = 0; i < machines.Count; i++)
            {
                var machine = machines[i];
                if (machine == null || string.IsNullOrEmpty(machine.Id) || machine.Transitions == null) continue;

                var machineActive = activeMachineIds.Contains(machine.Id);
                var activeStateId = activeStateIdByMachine.TryGetValue(machine.Id, out var active)
                    ? active
                    : string.Empty;

                for (var t = 0; t < machine.Transitions.Count; t++)
                {
                    var transition = machine.Transitions[t];
                    if (transition == null) continue;

                    var toPath = transition.ExitMachine ? string.Empty : StatePath(machine.Id, transition.ToStateId);
                    _snapshot.transitions.Add(new TransitionInfo
                    {
                        fromPath = transition.FromAnyState ? string.Empty : StatePath(machine.Id, transition.FromStateId),
                        toPath = toPath,
                        conditionDescription = DescribeCondition(transition),
                        isFromAny = transition.FromAnyState,
                        forceInstantly = transition.ForceImmediate,
                        canTransition = machineActive
                            && (transition.FromAnyState
                                || string.Equals(activeStateId, transition.FromStateId, StringComparison.Ordinal)),
                        lastTransitionTime = _lastTransitionTimeByPath.TryGetValue(toPath, out var last) ? last : 0f,
                    });
                }
            }
        }

        /// <summary>Walks the active state chain from the root machine (mirrors GetActivePath).</summary>
        private HashSet<string> ResolveActiveMachines(
            Dictionary<string, string> activeStateIdByMachine,
            out List<string> activePaths)
        {
            var activeMachines = new HashSet<string>(StringComparer.Ordinal);
            activePaths = new List<string>();

            var machineId = _definition.RootMachineId;
            while (!string.IsNullOrEmpty(machineId)
                && activeStateIdByMachine.TryGetValue(machineId, out var stateId)
                && !string.IsNullOrEmpty(stateId))
            {
                activeMachines.Add(machineId);
                activePaths.Add(StatePath(machineId, stateId));
                machineId = _machineById.TryGetValue(machineId, out var machine)
                    ? ChildMachineIdOf(machine, stateId)
                    : string.Empty;
            }

            return activeMachines;
        }

        private List<string> ResolvePendingTargets(
            Dictionary<string, string> activeStateIdByMachine,
            Dictionary<string, string> pendingTransitionIdByMachine,
            out List<string> exitingPaths)
        {
            var pendingTargets = new List<string>();
            exitingPaths = new List<string>();

            foreach (var pair in pendingTransitionIdByMachine)
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                if (!_transitionByKey.TryGetValue(pair.Key + "/" + pair.Value, out var transition)) continue;
                if (transition.ExitMachine) continue;

                pendingTargets.Add(StatePath(pair.Key, transition.ToStateId));
                if (activeStateIdByMachine.TryGetValue(pair.Key, out var activeStateId)
                    && !string.IsNullOrEmpty(activeStateId))
                {
                    exitingPaths.Add(StatePath(pair.Key, activeStateId));
                }
            }

            return pendingTargets;
        }

        private string ChildMachineIdOf(MachineDefinition machine, string stateId)
        {
            if (machine.States == null) return string.Empty;
            for (var i = 0; i < machine.States.Count; i++)
            {
                var state = machine.States[i];
                if (state != null && string.Equals(state.Id, stateId, StringComparison.Ordinal))
                    return state.ChildMachineId ?? string.Empty;
            }

            return string.Empty;
        }

        private void IndexStaticStructure()
        {
            var machines = _definition.Machines;
            if (machines == null) return;

            for (var i = 0; i < machines.Count; i++)
            {
                var machine = machines[i];
                if (machine == null || string.IsNullOrEmpty(machine.Id)) continue;
                _machineById[machine.Id] = machine;

                if (machine.Transitions != null)
                {
                    for (var t = 0; t < machine.Transitions.Count; t++)
                    {
                        var transition = machine.Transitions[t];
                        if (transition == null || string.IsNullOrEmpty(transition.Id)) continue;
                        _transitionByKey[machine.Id + "/" + transition.Id] = transition;
                    }
                }

                if (machine.States == null) continue;
                for (var s = 0; s < machine.States.Count; s++)
                {
                    var state = machine.States[s];
                    if (state == null || string.IsNullOrEmpty(state.ChildMachineId)) continue;
                    _ownerStatePathByMachineId[state.ChildMachineId] = StatePath(machine.Id, state.Id);
                }
            }
        }

        private static string DescribeCondition(TransitionDefinition transition)
        {
            if (!string.IsNullOrEmpty(transition.TriggerId))
                return string.IsNullOrEmpty(transition.ConditionKey)
                    ? "trigger:" + transition.TriggerId
                    : transition.ConditionKey + " [trigger:" + transition.TriggerId + "]";
            return string.IsNullOrEmpty(transition.ConditionKey) ? "tick" : transition.ConditionKey;
        }

        private static string StatePath(string machineId, string stateId) => machineId + "/" + stateId;

        private static float Seconds(long timeRaw) => Fixed64.FromRaw(timeRaw).ToSingle();

        private static int Depth(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            var level = 0;
            for (var i = 0; i < path.Length; i++)
                if (path[i] == '/') level++;
            return level;
        }
    }
}
