using System;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Graph.Compilation
{
    public sealed class StateMachineGraphBinding<TStateId, TEvent>
    {
        public StateMachineGraphBinding(
            Func<GraphNodeProgram, TStateId> stateIdSelector,
            Func<string, TEvent> eventIdSelector,
            IHfsmEvaluationContext evaluationContext = null,
            Func<string, Action> transitionActionResolver = null)
        {
            StateIdSelector = stateIdSelector ?? throw new ArgumentNullException(nameof(stateIdSelector));
            EventIdSelector = eventIdSelector ?? throw new ArgumentNullException(nameof(eventIdSelector));
            EvaluationContext = evaluationContext;
            TransitionActionResolver = transitionActionResolver;
        }

        public Func<GraphNodeProgram, TStateId> StateIdSelector { get; }
        public Func<string, TEvent> EventIdSelector { get; }
        public IHfsmEvaluationContext EvaluationContext { get; }
        public Func<string, Action> TransitionActionResolver { get; }

        public static StateMachineGraphBinding<TStateId, TEvent> CreateNameBinding(
            IHfsmEvaluationContext evaluationContext = null,
            Func<string, Action> transitionActionResolver = null)
        {
            if (typeof(TStateId) != typeof(string) || typeof(TEvent) != typeof(string))
            {
                throw new InvalidOperationException(
                    "The built-in graph binding is only available when both state and event IDs are strings. " +
                    "Supply an explicit StateMachineGraphBinding for other ID types.");
            }

            return new StateMachineGraphBinding<TStateId, TEvent>(
                node => (TStateId)(object)node.RuntimeName,
                triggerId => (TEvent)(object)triggerId,
                evaluationContext,
                transitionActionResolver);
        }
    }
}
