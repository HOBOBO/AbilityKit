#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class HierarchicalProfileBuilder<TBlackboard, TAction>
    {
        private readonly Func<TBlackboard, TAction, IActionBehaviour> _actionFactory;
        private readonly Func<TBlackboard, string, bool> _conditionEvaluator;

        public HierarchicalProfileBuilder(
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator)
        {
            _actionFactory = actionFactory ?? throw new ArgumentNullException(nameof(actionFactory));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        }

        public StateMachine<string> Build(
            IActionTimeSource timeSource,
            TBlackboard blackboard,
            HierarchicalProfile<TAction> profile)
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
            IReadOnlyList<NodeSpec<TAction>> states,
            IReadOnlyList<TransitionSpec> transitions,
            bool rememberLastState,
            string path)
        {
            var fsm = new StateMachine<string>(needsExitTime: false, rememberLastState: rememberLastState);
            var stateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionStates = new Dictionary<string, CompositeActionState<string, string>>(
                StringComparer.OrdinalIgnoreCase);
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

                StateBase<string> runtimeState;
                if (state.Kind == NodeKind.StateMachine)
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
                    var actionState = CreateActionState(timeSource, blackboard, state);
                    actionStates.Add(state.Id, actionState);
                    runtimeState = actionState;
                }

                fsm.AddState(state.Id, runtimeState);
            }

            if (stateIds.Count == 0)
            {
                throw new InvalidOperationException($"HFSM state machine '{path}' must contain at least one state.");
            }

            var transitionOrder = GetTransitionOrder(transitions);
            for (var i = 0; i < transitionOrder.Count; i++)
            {
                var transition = transitions[transitionOrder[i]];
                if (!stateIds.Contains(transition.From))
                {
                    throw new InvalidOperationException($"HFSM transition source '{transition.From}' does not exist in '{path}'.");
                }

                if (!stateIds.Contains(transition.To))
                {
                    throw new InvalidOperationException($"HFSM transition target '{transition.To}' does not exist in '{path}'.");
                }

                actionStates.TryGetValue(transition.From, out var sourceActionState);
                if (transition.Mode != TransitionMode.Condition && sourceActionState == null)
                {
                    throw new InvalidOperationException(
                        $"HFSM result transition source '{transition.From}' in '{path}' must be an action state.");
                }

                fsm.AddTransition(new Transition<string>(
                    transition.From,
                    transition.To,
                    _ => ShouldTransition(blackboard, transition, sourceActionState),
                    forceInstantly: transition.ForceInstantly));
            }

            if (string.IsNullOrWhiteSpace(configuredStartState))
            {
                throw new InvalidOperationException($"HFSM state machine '{path}' requires an explicit start state.");
            }

            if (!stateIds.Contains(configuredStartState))
            {
                throw new InvalidOperationException($"HFSM start state '{configuredStartState}' does not exist in '{path}'.");
            }

            fsm.SetStartState(configuredStartState);
            return fsm;
        }

        private CompositeActionState<string, string> CreateActionState(
            IActionTimeSource timeSource,
            TBlackboard blackboard,
            NodeSpec<TAction> state)
        {
            if (state.BehaviourRoot == null)
                throw new InvalidOperationException($"HFSM action state '{state.Id}' requires a behavior root.");

            var root = BehaviourFactory.Build(
                blackboard,
                state.BehaviourRoot,
                _actionFactory,
                _conditionEvaluator,
                state.Id);

            return new CompositeActionState<string>(needsExitTime: state.NeedsExitTime)
                .SetTimeSource(timeSource)
                .SetCompletionPolicy(state.CompletionPolicy)
                .SetRoot(root);
        }

        private bool ShouldTransition(
            TBlackboard blackboard,
            in TransitionSpec transition,
            CompositeActionState<string, string>? sourceState)
        {
            var resultMatches = transition.Mode switch
            {
                TransitionMode.Condition => true,
                TransitionMode.OnSucceeded =>
                    sourceState?.IsCompleted == true
                    && sourceState.LastStatus == ActionBehaviourStatus.Success,
                TransitionMode.OnFailed =>
                    sourceState?.IsCompleted == true
                    && sourceState.LastStatus == ActionBehaviourStatus.Failure,
                TransitionMode.OnFinished =>
                    sourceState?.IsCompleted == true
                    && sourceState.LastStatus != ActionBehaviourStatus.Running,
                _ => false,
            };

            return resultMatches
                   && (string.IsNullOrWhiteSpace(transition.Condition)
                       || _conditionEvaluator(blackboard, transition.Condition));
        }

        private static List<int> GetTransitionOrder(IReadOnlyList<TransitionSpec> transitions)
        {
            var order = new List<int>(transitions.Count);
            for (var i = 0; i < transitions.Count; i++) order.Add(i);
            order.Sort((left, right) =>
            {
                var priority = transitions[right].Priority.CompareTo(transitions[left].Priority);
                return priority != 0 ? priority : left.CompareTo(right);
            });
            return order;
        }
    }
}
