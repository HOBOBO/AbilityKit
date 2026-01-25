using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;

namespace AbilityKit.Ability.Flow.Stages
{
    public sealed class StagedFlowRootProvider<TArgs> : IFlowRootProvider<TArgs>
    {
        private readonly IStagedFlowProvider<TArgs> _core;
        private readonly IReadOnlyList<IFlowStageContributor<TArgs>> _contributors;

        public StagedFlowRootProvider(IStagedFlowProvider<TArgs> core, IReadOnlyList<IFlowStageContributor<TArgs>> contributors)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _contributors = (contributors ?? Array.Empty<IFlowStageContributor<TArgs>>())
                .Where(c => c != null)
                .OrderBy(c => c.Order)
                .ToArray();
        }

        public IFlowNode CreateRoot(TArgs args)
        {
            var tryNode = BuildStages(FlowStages.DefaultTryOrder, args);
            var finallyNode = BuildStages(FlowStages.DefaultFinallyOrder, args);
            return new FinallyNode(tryNode, finallyNode);
        }

        private IFlowNode BuildStages(IReadOnlyList<FlowStageKey> stages, TArgs args)
        {
            var nodes = new List<IFlowNode>();

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                AddStageNodes(stage, args, nodes);
            }

            if (nodes.Count == 0) return new DoNode();
            if (nodes.Count == 1) return nodes[0];
            return new SequenceNode(nodes);
        }

        private void AddStageNodes(FlowStageKey stage, TArgs args, List<IFlowNode> into)
        {
            for (int i = 0; i < _contributors.Count; i++)
            {
                var c = _contributors[i];
                if (!c.CanContribute(stage)) continue;

                var n = c.CreateNode(stage, args);
                if (n != null) into.Add(n);
            }

            var coreNode = _core.CreateStage(stage, args);
            if (coreNode != null) into.Add(coreNode);
        }
    }
}
