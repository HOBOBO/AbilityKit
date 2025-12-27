using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;

namespace AbilityKit.Ability.Flow.Stages
{
    public sealed class OrderedStagedFlowRootProvider<TArgs> : IFlowRootProvider<TArgs>
    {
        private readonly IStagedFlowProvider<TArgs> _core;
        private readonly IReadOnlyList<IFlowStageContributor<TArgs>> _contributors;
        private readonly IReadOnlyList<FlowStageKey> _tryStages;
        private readonly IReadOnlyList<FlowStageKey> _finallyStages;

        public OrderedStagedFlowRootProvider(
            IStagedFlowProvider<TArgs> core,
            IReadOnlyList<IFlowStageContributor<TArgs>> contributors,
            IReadOnlyList<FlowStageKey> tryStages,
            IReadOnlyList<FlowStageKey> finallyStages)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _tryStages = tryStages ?? throw new ArgumentNullException(nameof(tryStages));
            _finallyStages = finallyStages ?? throw new ArgumentNullException(nameof(finallyStages));

            _contributors = (contributors ?? Array.Empty<IFlowStageContributor<TArgs>>())
                .Where(c => c != null)
                .OrderBy(c => c.Order)
                .ToArray();
        }

        public IFlowNode CreateRoot(TArgs args)
        {
            var tryNode = BuildStages(_tryStages, args);
            var finallyNode = BuildStages(_finallyStages, args);
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
