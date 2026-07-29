#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public enum HfsmRuntimeBehaviourKind
    {
        Action = 0,
        Sequence = 1,
        Selector = 2,
        Parallel = 3,
        Invert = 4,
        Repeat = 5,
        Timeout = 6,
        Condition = 7,
        Delay = 8,
    }

    public sealed class HfsmRuntimeBehaviourSpec<TAction>
    {
        public HfsmRuntimeBehaviourSpec(
            HfsmRuntimeBehaviourKind kind,
            TAction action = default!,
            IReadOnlyList<HfsmRuntimeBehaviourSpec<TAction>>? children = null,
            int repeatCount = -1,
            float durationSeconds = 0f,
            bool useUnscaledTime = false,
            ParallelSuccessPolicy parallelSuccessPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy parallelFailurePolicy = ParallelFailurePolicy.Any,
            string? condition = null)
        {
            Kind = kind;
            Action = action;
            Children = children ?? Array.Empty<HfsmRuntimeBehaviourSpec<TAction>>();
            RepeatCount = repeatCount;
            DurationSeconds = Math.Max(0f, durationSeconds);
            UseUnscaledTime = useUnscaledTime;
            ParallelSuccessPolicy = parallelSuccessPolicy;
            ParallelFailurePolicy = parallelFailurePolicy;
            Condition = condition ?? string.Empty;
        }

        public HfsmRuntimeBehaviourKind Kind { get; }
        public TAction Action { get; }
        public IReadOnlyList<HfsmRuntimeBehaviourSpec<TAction>> Children { get; }
        public int RepeatCount { get; }
        public float DurationSeconds { get; }
        public bool UseUnscaledTime { get; }
        public ParallelSuccessPolicy ParallelSuccessPolicy { get; }
        public ParallelFailurePolicy ParallelFailurePolicy { get; }
        public string Condition { get; }

        public static HfsmRuntimeBehaviourSpec<TAction> Task(TAction action)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(HfsmRuntimeBehaviourKind.Action, action);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> Sequence(
            params HfsmRuntimeBehaviourSpec<TAction>[] children)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(HfsmRuntimeBehaviourKind.Sequence, children: children);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> Selector(
            params HfsmRuntimeBehaviourSpec<TAction>[] children)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(HfsmRuntimeBehaviourKind.Selector, children: children);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> Parallel(
            IReadOnlyList<HfsmRuntimeBehaviourSpec<TAction>> children,
            ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(
                HfsmRuntimeBehaviourKind.Parallel,
                children: children,
                parallelSuccessPolicy: successPolicy,
                parallelFailurePolicy: failurePolicy);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> ConditionNode(string condition)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(
                HfsmRuntimeBehaviourKind.Condition,
                condition: condition);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> Delay(
            float durationSeconds,
            bool useUnscaledTime = false)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(
                HfsmRuntimeBehaviourKind.Delay,
                durationSeconds: durationSeconds,
                useUnscaledTime: useUnscaledTime);
        }

        public static HfsmRuntimeBehaviourSpec<TAction> Decorate(
            HfsmRuntimeBehaviourKind kind,
            HfsmRuntimeBehaviourSpec<TAction> child,
            int repeatCount = -1,
            float durationSeconds = 0f,
            bool useUnscaledTime = false)
        {
            return new HfsmRuntimeBehaviourSpec<TAction>(
                kind,
                children: new[] { child },
                repeatCount: repeatCount,
                durationSeconds: durationSeconds,
                useUnscaledTime: useUnscaledTime);
        }
    }

    internal static class HfsmRuntimeBehaviourFactory
    {
        public static IActionBehaviour Build<TBlackboard, TAction>(
            TBlackboard blackboard,
            HfsmRuntimeBehaviourSpec<TAction> spec,
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator,
            string stateId)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            switch (spec.Kind)
            {
                case HfsmRuntimeBehaviourKind.Action:
                    return actionFactory(blackboard, spec.Action)
                           ?? throw new InvalidOperationException(
                               $"HFSM action factory returned null for state '{stateId}'.");

                case HfsmRuntimeBehaviourKind.Condition:
                    return new ConditionBehaviour(
                        (in ActionBehaviourContext _) => conditionEvaluator(blackboard, spec.Condition));

                case HfsmRuntimeBehaviourKind.Delay:
                    return new DelayBehaviour(spec.DurationSeconds, spec.UseUnscaledTime);

                case HfsmRuntimeBehaviourKind.Sequence:
                {
                    var sequence = new SequenceBehaviour();
                    AddChildren(sequence.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return sequence;
                }

                case HfsmRuntimeBehaviourKind.Selector:
                {
                    var selector = new SelectorBehaviour();
                    AddChildren(selector.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return selector;
                }

                case HfsmRuntimeBehaviourKind.Parallel:
                {
                    var parallel = new ParallelBehaviour(
                        spec.ParallelSuccessPolicy,
                        spec.ParallelFailurePolicy);
                    AddChildren(parallel.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return parallel;
                }

                case HfsmRuntimeBehaviourKind.Invert:
                    return new InvertBehaviour(
                        BuildSingleChild(blackboard, spec, actionFactory, conditionEvaluator, stateId));

                case HfsmRuntimeBehaviourKind.Repeat:
                    return new RepeatBehaviour(
                        BuildSingleChild(blackboard, spec, actionFactory, conditionEvaluator, stateId),
                        spec.RepeatCount);

                case HfsmRuntimeBehaviourKind.Timeout:
                    return new TimeoutBehaviour(
                        BuildSingleChild(blackboard, spec, actionFactory, conditionEvaluator, stateId),
                        spec.DurationSeconds,
                        spec.UseUnscaledTime);

                default:
                    throw new InvalidOperationException(
                        $"HFSM state '{stateId}' uses unsupported behaviour kind '{spec.Kind}'.");
            }
        }

        private static IActionBehaviour BuildSingleChild<TBlackboard, TAction>(
            TBlackboard blackboard,
            HfsmRuntimeBehaviourSpec<TAction> spec,
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator,
            string stateId)
        {
            if (spec.Children.Count != 1 || spec.Children[0] == null)
            {
                throw new InvalidOperationException(
                    $"HFSM decorator '{spec.Kind}' in state '{stateId}' requires exactly one child.");
            }

            return Build(blackboard, spec.Children[0], actionFactory, conditionEvaluator, stateId);
        }

        private static void AddChildren<TBlackboard, TAction>(
            Func<IActionBehaviour, object> add,
            TBlackboard blackboard,
            HfsmRuntimeBehaviourSpec<TAction> spec,
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator,
            string stateId)
        {
            for (var i = 0; i < spec.Children.Count; i++)
            {
                var child = spec.Children[i]
                            ?? throw new InvalidOperationException(
                                $"HFSM behaviour '{spec.Kind}' in state '{stateId}' has a null child at index {i}.");
                add(Build(blackboard, child, actionFactory, conditionEvaluator, stateId));
            }
        }
    }
}
