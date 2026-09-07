#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class NodeSpec<TAction>
    {
        public NodeSpec(
            string id,
            string startState,
            IReadOnlyList<NodeSpec<TAction>>? children,
            IReadOnlyList<TransitionSpec>? transitions,
            bool rememberLastState = false)
        {
            Id = id ?? string.Empty;
            Kind = NodeKind.StateMachine;
            StartState = startState ?? string.Empty;
            Children = children ?? Array.Empty<NodeSpec<TAction>>();
            Transitions = transitions ?? Array.Empty<TransitionSpec>();
            RememberLastState = rememberLastState;
        }

        public NodeSpec(
            string id,
            BehaviourSpec<TAction> behaviourRoot,
            ActionStateCompletionPolicy completionPolicy = ActionStateCompletionPolicy.Hold,
            bool needsExitTime = false)
        {
            Id = id ?? string.Empty;
            Kind = NodeKind.ActionState;
            BehaviourRoot = behaviourRoot ?? throw new ArgumentNullException(nameof(behaviourRoot));
            CompletionPolicy = completionPolicy;
            NeedsExitTime = needsExitTime;
            Children = Array.Empty<NodeSpec<TAction>>();
            Transitions = Array.Empty<TransitionSpec>();
            StartState = string.Empty;
        }

        public string Id { get; }

        public NodeKind Kind { get; }

        public string StartState { get; } = string.Empty;

        public BehaviourSpec<TAction>? BehaviourRoot { get; }

        public ActionStateCompletionPolicy CompletionPolicy { get; }

        public bool NeedsExitTime { get; }

        public IReadOnlyList<NodeSpec<TAction>> Children { get; }

        public IReadOnlyList<TransitionSpec> Transitions { get; }

        public bool RememberLastState { get; }
    }
}
