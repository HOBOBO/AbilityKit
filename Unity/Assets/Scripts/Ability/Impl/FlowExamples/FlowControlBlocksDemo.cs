using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public static class FlowControlBlocksDemo
    {
        private sealed class Flag
        {
            public bool Value;
        }

        public static IFlowNode Build(bool useRace)
        {
            var flag = new Flag { Value = useRace };

            return new SequenceNode(
                new DoNode(onEnter: c => c.Set(flag)),
                new IfNode(
                    predicate: c => c.Get<Flag>().Value,
                    thenNode: new RaceNode(
                        new WaitSecondsNode(0.2f),
                        new TimeoutNode(0.1f, new WaitSecondsNode(1f))
                    ),
                    elseNode: new ParallelAllNode(
                        new WaitSecondsNode(0.1f),
                        new WaitSecondsNode(0.15f)
                    )
                ),
                new SwitchNode<int>(
                    select: _ => useRace ? 1 : 0,
                    cases: new Dictionary<int, IFlowNode>
                    {
                        { 0, new WaitSecondsNode(0.05f) },
                        { 1, new WaitSecondsNode(0.05f) }
                    },
                    defaultNode: new WaitSecondsNode(0.05f)
                )
            );
        }
    }
}
