#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    internal static class BehaviourFactory
    {
        public static IActionBehaviour Build<TBlackboard, TAction>(
            TBlackboard blackboard,
            BehaviourSpec<TAction> spec,
            Func<TBlackboard, TAction, IActionBehaviour> actionFactory,
            Func<TBlackboard, string, bool> conditionEvaluator,
            string stateId)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            switch (spec.Kind)
            {
                case BehaviourKind.Action:
                    return actionFactory(blackboard, spec.Action)
                           ?? throw new InvalidOperationException(
                               $"HFSM action factory returned null for state '{stateId}'.");

                case BehaviourKind.Condition:
                    return new ConditionBehaviour(
                        (in ActionBehaviourContext _) => conditionEvaluator(blackboard, spec.Condition));

                case BehaviourKind.Delay:
                    return new DelayBehaviour(spec.DurationSeconds, spec.UseUnscaledTime);

                case BehaviourKind.Sequence:
                {
                    var sequence = new SequenceBehaviour();
                    AddChildren(sequence.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return sequence;
                }

                case BehaviourKind.Selector:
                {
                    var selector = new SelectorBehaviour();
                    AddChildren(selector.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return selector;
                }

                case BehaviourKind.Parallel:
                {
                    var parallel = new ParallelBehaviour(
                        spec.ParallelSuccessPolicy,
                        spec.ParallelFailurePolicy);
                    AddChildren(parallel.Add, blackboard, spec, actionFactory, conditionEvaluator, stateId);
                    return parallel;
                }

                case BehaviourKind.Invert:
                    return new InvertBehaviour(
                        BuildSingleChild(blackboard, spec, actionFactory, conditionEvaluator, stateId));

                case BehaviourKind.Repeat:
                    return new RepeatBehaviour(
                        BuildSingleChild(blackboard, spec, actionFactory, conditionEvaluator, stateId),
                        spec.RepeatCount);

                case BehaviourKind.Timeout:
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
            BehaviourSpec<TAction> spec,
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
            BehaviourSpec<TAction> spec,
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
