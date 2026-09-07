#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class BehaviourSpec<TAction>
    {
        public BehaviourSpec(
            BehaviourKind kind,
            TAction action = default!,
            IReadOnlyList<BehaviourSpec<TAction>>? children = null,
            int repeatCount = -1,
            float durationSeconds = 0f,
            bool useUnscaledTime = false,
            ParallelSuccessPolicy parallelSuccessPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy parallelFailurePolicy = ParallelFailurePolicy.Any,
            string? condition = null)
        {
            Kind = kind;
            Action = action;
            Children = children ?? Array.Empty<BehaviourSpec<TAction>>();
            RepeatCount = repeatCount;
            DurationSeconds = Math.Max(0f, durationSeconds);
            UseUnscaledTime = useUnscaledTime;
            ParallelSuccessPolicy = parallelSuccessPolicy;
            ParallelFailurePolicy = parallelFailurePolicy;
            Condition = condition ?? string.Empty;
        }

        public BehaviourKind Kind { get; }
        public TAction Action { get; }
        public IReadOnlyList<BehaviourSpec<TAction>> Children { get; }
        public int RepeatCount { get; }
        public float DurationSeconds { get; }
        public bool UseUnscaledTime { get; }
        public ParallelSuccessPolicy ParallelSuccessPolicy { get; }
        public ParallelFailurePolicy ParallelFailurePolicy { get; }
        public string Condition { get; }

        public static BehaviourSpec<TAction> Task(TAction action)
        {
            return new BehaviourSpec<TAction>(BehaviourKind.Action, action);
        }

        public static BehaviourSpec<TAction> Sequence(
            params BehaviourSpec<TAction>[] children)
        {
            return new BehaviourSpec<TAction>(BehaviourKind.Sequence, children: children);
        }

        public static BehaviourSpec<TAction> Selector(
            params BehaviourSpec<TAction>[] children)
        {
            return new BehaviourSpec<TAction>(BehaviourKind.Selector, children: children);
        }

        public static BehaviourSpec<TAction> Parallel(
            IReadOnlyList<BehaviourSpec<TAction>> children,
            ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any)
        {
            return new BehaviourSpec<TAction>(
                BehaviourKind.Parallel,
                children: children,
                parallelSuccessPolicy: successPolicy,
                parallelFailurePolicy: failurePolicy);
        }

        public static BehaviourSpec<TAction> ConditionNode(string condition)
        {
            return new BehaviourSpec<TAction>(
                BehaviourKind.Condition,
                condition: condition);
        }

        public static BehaviourSpec<TAction> Delay(
            float durationSeconds,
            bool useUnscaledTime = false)
        {
            return new BehaviourSpec<TAction>(
                BehaviourKind.Delay,
                durationSeconds: durationSeconds,
                useUnscaledTime: useUnscaledTime);
        }

        public static BehaviourSpec<TAction> Decorate(
            BehaviourKind kind,
            BehaviourSpec<TAction> child,
            int repeatCount = -1,
            float durationSeconds = 0f,
            bool useUnscaledTime = false)
        {
            return new BehaviourSpec<TAction>(
                kind,
                children: new[] { child },
                repeatCount: repeatCount,
                durationSeconds: durationSeconds,
                useUnscaledTime: useUnscaledTime);
        }
    }
}
