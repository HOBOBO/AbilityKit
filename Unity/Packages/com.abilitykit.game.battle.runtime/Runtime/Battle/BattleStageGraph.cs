using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleStageDefinition
    {
        public BattleStageDefinition(string id, int order = 0, IReadOnlyList<string> prerequisites = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stage id is required.", nameof(id));

            Id = id;
            Order = order;
            Prerequisites = prerequisites ?? Array.Empty<string>();
        }

        public string Id { get; }

        public int Order { get; }

        public IReadOnlyList<string> Prerequisites { get; }
    }

    public sealed class BattleStageGraph
    {
        private readonly IReadOnlyList<BattleStageDefinition> _orderedStages;
        private readonly IReadOnlyDictionary<string, BattleStageDefinition> _stagesById;

        private BattleStageGraph(
            IReadOnlyList<BattleStageDefinition> orderedStages,
            IReadOnlyDictionary<string, BattleStageDefinition> stagesById)
        {
            _orderedStages = orderedStages;
            _stagesById = stagesById;
        }

        public IReadOnlyList<BattleStageDefinition> OrderedStages => _orderedStages;

        public static BattleStageGraph Create(IEnumerable<BattleStageDefinition> stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));

            var nodes = stages.Select((stage, sourceIndex) => new Node(stage, sourceIndex)).ToArray();
            var byId = new Dictionary<string, Node>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node.Stage == null)
                {
                    throw new InvalidOperationException($"Battle stage at index {node.SourceIndex} is null.");
                }

                if (!byId.TryAdd(node.Stage.Id, node))
                {
                    throw new InvalidOperationException($"Duplicate battle stage id '{node.Stage.Id}'.");
                }
            }

            foreach (var node in nodes)
            {
                var uniquePrerequisites = new HashSet<string>(StringComparer.Ordinal);
                foreach (var prerequisiteId in node.Stage.Prerequisites)
                {
                    if (string.IsNullOrWhiteSpace(prerequisiteId))
                    {
                        throw new InvalidOperationException($"Battle stage '{node.Stage.Id}' contains an empty prerequisite id.");
                    }

                    if (!uniquePrerequisites.Add(prerequisiteId)) continue;
                    if (!byId.TryGetValue(prerequisiteId, out var prerequisite))
                    {
                        throw new InvalidOperationException(
                            $"Battle stage '{node.Stage.Id}' references missing prerequisite '{prerequisiteId}'.");
                    }

                    prerequisite.Successors.Add(node);
                    node.InDegree++;
                }
            }

            var ready = new List<Node>(nodes.Where(node => node.InDegree == 0));
            var ordered = new List<BattleStageDefinition>(nodes.Length);
            while (ready.Count > 0)
            {
                ready.Sort(NodeComparer.Instance);
                var next = ready[0];
                ready.RemoveAt(0);
                ordered.Add(next.Stage);
                foreach (var successor in next.Successors)
                {
                    successor.InDegree--;
                    if (successor.InDegree == 0) ready.Add(successor);
                }
            }

            if (ordered.Count != nodes.Length)
            {
                var blocked = nodes.Where(node => node.InDegree > 0).Select(node => node.Stage.Id);
                throw new InvalidOperationException(
                    "Battle stage dependency cycle contains: " + string.Join(",", blocked));
            }

            return new BattleStageGraph(
                ordered,
                ordered.ToDictionary(stage => stage.Id, StringComparer.Ordinal));
        }

        public bool TryGetStage(string id, out BattleStageDefinition stage)
        {
            return _stagesById.TryGetValue(id, out stage);
        }

        public IReadOnlyList<BattleStageDefinition> GetAvailableStages(ISet<string> completedStageIds)
        {
            if (completedStageIds == null) throw new ArgumentNullException(nameof(completedStageIds));

            var available = new List<BattleStageDefinition>();
            foreach (var stage in _orderedStages)
            {
                if (completedStageIds.Contains(stage.Id)) continue;

                var isAvailable = true;
                foreach (var prerequisite in stage.Prerequisites)
                {
                    if (completedStageIds.Contains(prerequisite)) continue;
                    isAvailable = false;
                    break;
                }

                if (isAvailable) available.Add(stage);
            }

            return available;
        }

        private sealed class Node
        {
            public Node(BattleStageDefinition stage, int sourceIndex)
            {
                Stage = stage;
                SourceIndex = sourceIndex;
            }

            public BattleStageDefinition Stage { get; }

            public int SourceIndex { get; }

            public List<Node> Successors { get; } = new List<Node>();

            public int InDegree { get; set; }
        }

        private sealed class NodeComparer : IComparer<Node>
        {
            public static NodeComparer Instance { get; } = new NodeComparer();

            public int Compare(Node left, Node right)
            {
                var byOrder = left.Stage.Order.CompareTo(right.Stage.Order);
                return byOrder != 0 ? byOrder : left.SourceIndex.CompareTo(right.SourceIndex);
            }
        }
    }
}
