#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public enum HfsmRuntimeNodeKind
    {
        ActionState = 0,
        StateMachine = 1,
    }

    public sealed class HfsmRuntimeNodeSpec<TAction>
    {
        public HfsmRuntimeNodeSpec(
            string id,
            IReadOnlyList<TAction>? actions,
            float intervalSeconds = 0f)
        {
            Id = id ?? string.Empty;
            Kind = HfsmRuntimeNodeKind.ActionState;
            IntervalSeconds = intervalSeconds < 0f ? 0f : intervalSeconds;
            Actions = actions ?? Array.Empty<TAction>();
            Children = Array.Empty<HfsmRuntimeNodeSpec<TAction>>();
            Transitions = Array.Empty<HfsmRuntimeTransitionSpec>();
            StartState = string.Empty;
        }

        public HfsmRuntimeNodeSpec(
            string id,
            string startState,
            IReadOnlyList<HfsmRuntimeNodeSpec<TAction>>? children,
            IReadOnlyList<HfsmRuntimeTransitionSpec>? transitions,
            bool rememberLastState = false)
        {
            Id = id ?? string.Empty;
            Kind = HfsmRuntimeNodeKind.StateMachine;
            StartState = startState ?? string.Empty;
            Children = children ?? Array.Empty<HfsmRuntimeNodeSpec<TAction>>();
            Transitions = transitions ?? Array.Empty<HfsmRuntimeTransitionSpec>();
            RememberLastState = rememberLastState;
            Actions = Array.Empty<TAction>();
        }

        public string Id { get; }

        public HfsmRuntimeNodeKind Kind { get; }

        public string StartState { get; } = string.Empty;

        public float IntervalSeconds { get; }

        public IReadOnlyList<TAction> Actions { get; }

        public IReadOnlyList<HfsmRuntimeNodeSpec<TAction>> Children { get; }

        public IReadOnlyList<HfsmRuntimeTransitionSpec> Transitions { get; }

        public bool RememberLastState { get; }
    }

    public sealed class HfsmHierarchicalRuntimeProfile<TAction>
    {
        public HfsmHierarchicalRuntimeProfile(
            string id,
            string startState,
            IReadOnlyList<HfsmRuntimeNodeSpec<TAction>>? states,
            IReadOnlyList<HfsmRuntimeTransitionSpec>? transitions)
        {
            Id = id ?? string.Empty;
            StartState = startState ?? string.Empty;
            States = states ?? Array.Empty<HfsmRuntimeNodeSpec<TAction>>();
            Transitions = transitions ?? Array.Empty<HfsmRuntimeTransitionSpec>();
        }

        public string Id { get; }

        public string StartState { get; }

        public IReadOnlyList<HfsmRuntimeNodeSpec<TAction>> States { get; }

        public IReadOnlyList<HfsmRuntimeTransitionSpec> Transitions { get; }
    }

    public sealed class HfsmHierarchicalRuntimeProfileBuilder<TBlackboard, TAction>
    {
        private readonly Func<TBlackboard, TAction, IActionBehaviour> _actionFactory;
        private readonly Func<TBlackboard, string, bool> _conditionEvaluator;

        public HfsmHierarchicalRuntimeProfileBuilder(
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator)
        {
            _actionFactory = actionFactory ?? throw new ArgumentNullException(nameof(actionFactory));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        }

        public StateMachine<string> Build(
            IActionTimeSource timeSource,
            TBlackboard blackboard,
            HfsmHierarchicalRuntimeProfile<TAction> profile)
        {
            if (timeSource == null) throw new ArgumentNullException(nameof(timeSource));
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var root = BuildStateMachine(
                timeSource,
                blackboard,
                profile.StartState,
                profile.States,
                profile.Transitions,
                rememberLastState: false,
                path: string.IsNullOrWhiteSpace(profile.Id) ? "root" : profile.Id);
            root.Init();
            return root;
        }

        private StateMachine<string> BuildStateMachine(
            IActionTimeSource timeSource,
            TBlackboard blackboard,
            string configuredStartState,
            IReadOnlyList<HfsmRuntimeNodeSpec<TAction>> states,
            IReadOnlyList<HfsmRuntimeTransitionSpec> transitions,
            bool rememberLastState,
            string path)
        {
            var fsm = new StateMachine<string>(needsExitTime: false, rememberLastState: rememberLastState);
            var stateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var firstState = string.Empty;

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i] ?? throw new InvalidOperationException($"HFSM node at '{path}[{i}]' is null.");
                if (string.IsNullOrWhiteSpace(state.Id))
                {
                    throw new InvalidOperationException($"HFSM node at '{path}[{i}]' must have a non-empty id.");
                }

                if (!stateIds.Add(state.Id))
                {
                    throw new InvalidOperationException($"HFSM state id '{state.Id}' is duplicated in '{path}'.");
                }

                if (firstState.Length == 0) firstState = state.Id;

                StateBase<string> runtimeState;
                if (state.Kind == HfsmRuntimeNodeKind.StateMachine)
                {
                    runtimeState = BuildStateMachine(
                        timeSource,
                        blackboard,
                        state.StartState,
                        state.Children,
                        state.Transitions,
                        state.RememberLastState,
                        path + "/" + state.Id);
                }
                else
                {
                    runtimeState = CreateActionState(timeSource, blackboard, state);
                }

                fsm.AddState(state.Id, runtimeState);
            }

            if (stateIds.Count == 0)
            {
                throw new InvalidOperationException($"HFSM state machine '{path}' must contain at least one state.");
            }

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (!stateIds.Contains(transition.From))
                {
                    throw new InvalidOperationException($"HFSM transition source '{transition.From}' does not exist in '{path}'.");
                }

                if (!stateIds.Contains(transition.To))
                {
                    throw new InvalidOperationException($"HFSM transition target '{transition.To}' does not exist in '{path}'.");
                }

                fsm.AddTransition(new Transition<string>(
                    transition.From,
                    transition.To,
                    _ => _conditionEvaluator(blackboard, transition.Condition)));
            }

            var startState = string.IsNullOrWhiteSpace(configuredStartState) ? firstState : configuredStartState;
            if (!stateIds.Contains(startState))
            {
                throw new InvalidOperationException($"HFSM start state '{startState}' does not exist in '{path}'.");
            }

            fsm.SetStartState(startState);
            return fsm;
        }

        private CompositeActionState<string, string> CreateActionState(
            IActionTimeSource timeSource,
            TBlackboard blackboard,
            HfsmRuntimeNodeSpec<TAction> state)
        {
            var sequence = new SequenceBehaviour();
            for (var i = 0; i < state.Actions.Count; i++)
            {
                var action = _actionFactory(blackboard, state.Actions[i]);
                if (action == null)
                {
                    throw new InvalidOperationException($"HFSM action factory returned null for state '{state.Id}'.");
                }

                sequence.Add(action);
            }

            if (state.IntervalSeconds > 0f)
            {
                sequence.Add(new DelayBehaviour(state.IntervalSeconds));
            }

            return new CompositeActionState<string>(needsExitTime: false)
                .SetTimeSource(timeSource)
                .SetLoop(true)
                .SetRoot(sequence);
        }
    }
}
