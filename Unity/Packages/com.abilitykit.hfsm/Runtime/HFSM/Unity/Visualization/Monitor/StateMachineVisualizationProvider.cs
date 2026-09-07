using System;
using System.Collections.Generic;
using UnityEngine;
using AbilityKit.HFSM.Actions;
using AbilityKit.HFSM.Inspection;

namespace AbilityKit.HFSM.Visualization
{
    /// <summary>
    /// Optional parameter adapter for runtime types that own a parameter context.
    /// </summary>
    public interface IVisualizationParameterSource
    {
        IEnumerable<ParameterInfo> GetVisualizationParameters();
    }

    /// <summary>
    /// Strongly typed inspection provider for every StateMachine generic shape.
    /// It traverses the public Visitor/API surface and never reflects over runtime objects.
    /// </summary>
    public class StateMachineVisualizationProvider : IVisualizationProvider
    {
        private const int DefaultHistoryCapacity = 256;

        private readonly WeakReference _root;
        private readonly WeakReference _parameterSource;
        private readonly FsmSnapshot _snapshot = new FsmSnapshot();
        private readonly List<StateTransitionRecord> _history = new List<StateTransitionRecord>();
        private readonly int _historyCapacity;
        private bool _hasSnapshot;

        public StateMachineVisualizationProvider(
            IStateMachineInspectionSource root,
            IVisualizationParameterSource parameterSource = null,
            int historyCapacity = DefaultHistoryCapacity)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _root = new WeakReference(root);
            _parameterSource = parameterSource == null ? null : new WeakReference(parameterSource);
            _historyCapacity = Math.Max(1, historyCapacity);
        }

        public int HistoryCapacity => _historyCapacity;

        public FsmSnapshot GetSnapshot()
        {
            _snapshot.states.Clear();
            _snapshot.transitions.Clear();
            _snapshot.parameters.Clear();
            _snapshot.behaviorNodes.Clear();
            _snapshot.activeStatePaths.Clear();
            _snapshot.pendingStatePaths.Clear();
            _snapshot.exitingStatePaths.Clear();
            _snapshot.history.Clear();
            _snapshot.snapshotTime = Time.time;

            var root = _root.Target as IStateMachineInspectionSource;
            if (root == null)
            {
                _hasSnapshot = true;
                return _snapshot;
            }

            root.AcceptVisitor(new RootVisitor(this));

            var parameterSource = _parameterSource?.Target as IVisualizationParameterSource;
            if (parameterSource != null)
                AddParameters(parameterSource.GetVisualizationParameters());

            foreach (var record in GetHistory(_historyCapacity))
                _snapshot.history.Add(record);

            FinalizeStateActivity();
            _hasSnapshot = true;
            return _snapshot;
        }

        public IEnumerable<string> GetActiveStatePaths()
        {
            GetSnapshot();
            return _snapshot.activeStatePaths;
        }

        public IEnumerable<ParameterInfo> GetParameters()
        {
            EnsureSnapshot();
            return _snapshot.parameters;
        }

        public IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure()
        {
            EnsureSnapshot();
            foreach (var state in _snapshot.states)
                yield return (state.name, state.parentPath, state.isStateMachine);
        }

        public IEnumerable<TransitionInfo> GetTransitions()
        {
            EnsureSnapshot();
            return _snapshot.transitions;
        }

        public void RecordTransition(string fromPath, string toPath, string trigger)
        {
            if (_history.Count == _historyCapacity)
                _history.RemoveAt(0);

            _history.Add(new StateTransitionRecord
            {
                fromPath = fromPath ?? string.Empty,
                toPath = toPath ?? string.Empty,
                trigger = trigger ?? string.Empty,
                timestamp = Time.time,
                timeAgo = 0f
            });
        }

        public IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50)
        {
            var count = Math.Min(Math.Max(0, maxCount), _history.Count);
            var start = _history.Count - count;
            var now = Time.time;
            for (var index = start; index < _history.Count; index++)
            {
                var record = _history[index];
                record.timeAgo = now - record.timestamp;
                yield return record;
            }
        }

        private void Capture<TOwnId, TStateId, TEvent>(StateMachine<TOwnId, TStateId, TEvent> root)
        {
            StateMachineWalker.Walk(root, new HierarchyVisitor(this));
        }

        private void EnsureSnapshot()
        {
            if (!_hasSnapshot)
                GetSnapshot();
        }

        private void AddParameters(IEnumerable<ParameterInfo> parameters)
        {
            if (parameters == null) return;
            foreach (var parameter in parameters)
                _snapshot.parameters.Add(parameter);
        }

        private void FinalizeStateActivity()
        {
            for (var index = 0; index < _snapshot.states.Count; index++)
            {
                var state = _snapshot.states[index];
                state.isActive = _snapshot.activeStatePaths.Contains(state.path);
                state.isEntering = _snapshot.pendingStatePaths.Contains(state.path);
                state.isExiting = _snapshot.exitingStatePaths.Contains(state.path);
                _snapshot.states[index] = state;
            }
        }

        private static string NormalizePath(StateMachinePath path)
        {
            var value = path == null ? string.Empty : path.ToString();
            if (value == RootStateMachinePath.name)
                return string.Empty;
            const string rootPrefix = "Root/";
            if (value.StartsWith(rootPrefix, StringComparison.Ordinal))
                value = value.Substring(rootPrefix.Length);
            return value.TrimStart('/');
        }

        private static string ParentPath(string path)
        {
            var separator = path.LastIndexOf('/');
            return separator < 0 ? string.Empty : path.Substring(0, separator);
        }

        private static int NestingLevel(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            var level = 0;
            for (var index = 0; index < path.Length; index++)
                if (path[index] == '/') level++;
            return level;
        }

        private sealed class RootVisitor : IStateVisitor
        {
            private readonly StateMachineVisualizationProvider _provider;
            public RootVisitor(StateMachineVisualizationProvider provider) { _provider = provider; }

            public void VisitStateMachine<TOwnId, TStateId, TEvent>(StateMachine<TOwnId, TStateId, TEvent> fsm)
            {
                _provider.Capture(fsm);
            }

            public void VisitRegularState<TStateId>(StateBase<TStateId> state)
            {
            }
        }

        private sealed class HierarchyVisitor : IStateMachineHierarchyVisitor
        {
            private readonly StateMachineVisualizationProvider _provider;
            public HierarchyVisitor(StateMachineVisualizationProvider provider) { _provider = provider; }

            public void VisitStateMachine<TOwnId, TStateId, TEvent>(
                StateMachinePath fsmPath,
                StateMachine<TOwnId, TStateId, TEvent> fsm)
            {
                var path = NormalizePath(fsmPath);
                if (!string.IsNullOrEmpty(path))
                {
                    _provider._snapshot.states.Add(new StateNodeInfo
                    {
                        name = ReferenceEquals(fsm.name, null) ? string.Empty : fsm.name.ToString(),
                        path = path,
                        parentPath = ParentPath(path),
                        isStateMachine = true,
                        nestingLevel = NestingLevel(path)
                    });
                }

                AddTransitions(fsmPath, fsm);
                if (!fsm.IsActive) return;
                try
                {
                    var activeName = fsm.ActiveStateName;
                    var activeNameText = ReferenceEquals(activeName, null) ? string.Empty : activeName.ToString();
                    var activePath = string.IsNullOrEmpty(path)
                        ? activeNameText
                        : path + "/" + activeNameText;
                    if (!string.IsNullOrEmpty(activePath))
                        _provider._snapshot.activeStatePaths.Add(activePath);

                    if (!fsm.HasPendingTransition)
                        return;

                    if (!string.IsNullOrEmpty(activePath))
                        _provider._snapshot.exitingStatePaths.Add(activePath);

                    if (fsm.IsPendingExitTransition)
                        return;

                    var pendingName = fsm.PendingStateName;
                    var pendingNameText = ReferenceEquals(pendingName, null)
                        ? string.Empty
                        : pendingName.ToString();
                    var pendingPath = string.IsNullOrEmpty(path)
                        ? pendingNameText
                        : Join(path, pendingNameText);
                    if (!string.IsNullOrEmpty(pendingPath))
                        _provider._snapshot.pendingStatePaths.Add(pendingPath);
                }
                catch (InvalidOperationException)
                {
                    // Uninitialized machines report IsActive=false; custom implementations may still throw.
                }
            }

            public void ExitStateMachine<TOwnId, TStateId, TEvent>(
                StateMachinePath fsmPath,
                StateMachine<TOwnId, TStateId, TEvent> fsm)
            {
            }

            public void VisitRegularState<TStateId>(StateMachinePath statePath, StateBase<TStateId> state)
            {
                var path = NormalizePath(statePath);
                _provider._snapshot.states.Add(new StateNodeInfo
                {
                    name = ReferenceEquals(state.name, null) ? string.Empty : state.name.ToString(),
                    path = path,
                    parentPath = ParentPath(path),
                    isStateMachine = false,
                    nestingLevel = NestingLevel(path)
                });

                if (state is IActionRuntimeStateProvider actionState)
                {
                    foreach (var runtimeState in actionState.GetActionRuntimeStates())
                    {
                        if (runtimeState == null) continue;
                        _provider._snapshot.behaviorNodes.Add(new BehaviorNodeInfo
                        {
                            statePath = path,
                            id = runtimeState.RuntimeId,
                            parentId = runtimeState.ParentRuntimeId,
                            name = runtimeState.Name,
                            typeName = runtimeState.TypeName,
                            status = (BehaviorNodeStatus)(int)runtimeState.RuntimeStatus,
                            isActive = runtimeState.IsActive,
                            executionCount = runtimeState.ExecutionCount,
                            elapsedTime = runtimeState.ElapsedTime
                        });
                    }
                }
            }

            private void AddTransitions<TOwnId, TStateId, TEvent>(
                StateMachinePath machinePath,
                StateMachine<TOwnId, TStateId, TEvent> machine)
            {
                var prefix = NormalizePath(machinePath);
                foreach (var transition in machine.GetAllTransitions())
                    AddTransition(prefix, machine, transition, string.Empty, false);
                foreach (var transition in machine.GetAllTransitionsFromAny())
                    AddTransition(prefix, machine, transition, string.Empty, true);
                foreach (var pair in machine.GetAllTriggerTransitions())
                    foreach (var transition in pair.Value)
                        AddTransition(prefix, machine, transition, ReferenceEquals(pair.Key, null) ? string.Empty : pair.Key.ToString(), false);
                foreach (var pair in machine.GetAllTriggerTransitionsFromAny())
                    foreach (var transition in pair.Value)
                        AddTransition(prefix, machine, transition, ReferenceEquals(pair.Key, null) ? string.Empty : pair.Key.ToString(), true);
            }

            private void AddTransition<TOwnId, TStateId, TEvent>(
                string prefix,
                StateMachine<TOwnId, TStateId, TEvent> machine,
                TransitionBase<TStateId> transition,
                string trigger,
                bool fromAny)
            {
                var from = ReferenceEquals(transition.from, null) ? string.Empty : transition.from.ToString();
                var to = transition.isExitTransition || ReferenceEquals(transition.to, null) ? string.Empty : transition.to.ToString();
                var fromPath = string.IsNullOrEmpty(from) ? prefix : Join(prefix, from);
                var toPath = string.IsNullOrEmpty(to) ? string.Empty : Join(prefix, to);
                _provider._snapshot.transitions.Add(new TransitionInfo
                {
                    fromPath = fromPath,
                    toPath = toPath,
                    conditionDescription = string.IsNullOrEmpty(trigger)
                        ? transition.GetType().Name
                        : transition.GetType().Name + " [trigger: " + trigger + "]",
                    isFromAny = fromAny,
                    forceInstantly = transition.forceInstantly,
                    canTransition = IsEligible(machine, transition, fromAny),
                    lastTransitionTime = 0f
                });
            }

            private static string Join(string prefix, string value)
            {
                return string.IsNullOrEmpty(prefix) ? value : prefix + "/" + value;
            }

            private static bool IsEligible<TOwnId, TStateId, TEvent>(
                StateMachine<TOwnId, TStateId, TEvent> machine,
                TransitionBase<TStateId> transition,
                bool fromAny)
            {
                if (!machine.IsActive) return false;
                if (fromAny)
                    return true;
                try
                {
                    return EqualityComparer<TStateId>.Default.Equals(machine.ActiveStateName, transition.from);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Strongly typed convenience facade for callers that know the machine's generic arguments.
    /// </summary>
    public sealed class StateMachineVisualizationProvider<TOwnId, TStateId, TEvent>
        : StateMachineVisualizationProvider
    {
        public StateMachineVisualizationProvider(
            StateMachine<TOwnId, TStateId, TEvent> root,
            IVisualizationParameterSource parameterSource = null,
            int historyCapacity = 256)
            : base(root, parameterSource, historyCapacity)
        {
        }
    }
}
