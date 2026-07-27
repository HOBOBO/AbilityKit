using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Game.Battle
{
    public interface IBattleSystemContribution<in TContext, out TSystem>
    {
        string Id { get; }

        int Order { get; }

        IReadOnlyList<string> RunsAfter { get; }

        IReadOnlyList<string> RunsBefore { get; }

        TSystem Create(TContext context);
    }

    public sealed class BattleSystemContribution<TContext, TSystem> : IBattleSystemContribution<TContext, TSystem>
    {
        private static readonly string[] EmptyDependencies = Array.Empty<string>();
        private readonly Func<TContext, TSystem> _factory;

        public BattleSystemContribution(
            string id,
            int order,
            Func<TContext, TSystem> factory,
            IReadOnlyList<string> runsAfter = null,
            IReadOnlyList<string> runsBefore = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Contribution id is required.", nameof(id));

            Id = id;
            Order = order;
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            RunsAfter = runsAfter ?? EmptyDependencies;
            RunsBefore = runsBefore ?? EmptyDependencies;
        }

        public string Id { get; }

        public int Order { get; }

        public IReadOnlyList<string> RunsAfter { get; }

        public IReadOnlyList<string> RunsBefore { get; }

        public TSystem Create(TContext context)
        {
            return _factory(context);
        }
    }

    public sealed class BattleSystemContributionPlan<TContext, TSystem>
    {
        private readonly IReadOnlyList<IBattleSystemContribution<TContext, TSystem>> _orderedContributions;

        internal BattleSystemContributionPlan(IReadOnlyList<IBattleSystemContribution<TContext, TSystem>> orderedContributions)
        {
            _orderedContributions = orderedContributions;
        }

        public IReadOnlyList<IBattleSystemContribution<TContext, TSystem>> OrderedContributions => _orderedContributions;

        public IReadOnlyList<TSystem> CreateSystems(TContext context)
        {
            var systems = new TSystem[_orderedContributions.Count];
            for (var i = 0; i < systems.Length; i++)
            {
                systems[i] = _orderedContributions[i].Create(context);
            }

            return systems;
        }
    }

    public static class BattleSystemContributionPlanner
    {
        public static BattleSystemContributionPlan<TContext, TSystem> Create<TContext, TSystem>(
            IEnumerable<IBattleSystemContribution<TContext, TSystem>> contributions)
        {
            if (contributions == null) throw new ArgumentNullException(nameof(contributions));

            var nodes = contributions
                .Select((contribution, sourceIndex) => new Node<TContext, TSystem>(contribution, sourceIndex))
                .ToArray();
            var byId = new Dictionary<string, Node<TContext, TSystem>>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node.Contribution == null)
                {
                    throw new InvalidOperationException($"Battle system contribution at index {node.SourceIndex} is null.");
                }

                if (!byId.TryAdd(node.Contribution.Id, node))
                {
                    throw new InvalidOperationException($"Duplicate battle system contribution id '{node.Contribution.Id}'.");
                }
            }

            foreach (var node in nodes)
            {
                AddDependencies(node, node.Contribution.RunsAfter, byId, dependencyIsPredecessor: true);
                AddDependencies(node, node.Contribution.RunsBefore, byId, dependencyIsPredecessor: false);
            }

            var ready = new List<Node<TContext, TSystem>>(nodes.Where(node => node.InDegree == 0));
            var ordered = new List<IBattleSystemContribution<TContext, TSystem>>(nodes.Length);
            while (ready.Count > 0)
            {
                ready.Sort(NodeComparer<TContext, TSystem>.Instance);
                var next = ready[0];
                ready.RemoveAt(0);
                ordered.Add(next.Contribution);

                foreach (var successor in next.Successors)
                {
                    successor.InDegree--;
                    if (successor.InDegree == 0)
                    {
                        ready.Add(successor);
                    }
                }
            }

            if (ordered.Count != nodes.Length)
            {
                throw new InvalidOperationException("Battle system contribution dependency cycle: " + FindCycle(nodes));
            }

            return new BattleSystemContributionPlan<TContext, TSystem>(ordered);
        }

        private static void AddDependencies<TContext, TSystem>(
            Node<TContext, TSystem> node,
            IReadOnlyList<string> dependencyIds,
            IReadOnlyDictionary<string, Node<TContext, TSystem>> byId,
            bool dependencyIsPredecessor)
        {
            if (dependencyIds == null) return;

            foreach (var dependencyId in dependencyIds)
            {
                if (string.IsNullOrWhiteSpace(dependencyId))
                {
                    throw new InvalidOperationException($"Battle system contribution '{node.Contribution.Id}' contains an empty dependency id.");
                }

                if (!byId.TryGetValue(dependencyId, out var dependency))
                {
                    throw new InvalidOperationException(
                        $"Battle system contribution '{node.Contribution.Id}' references missing contribution '{dependencyId}'.");
                }

                var predecessor = dependencyIsPredecessor ? dependency : node;
                var successor = dependencyIsPredecessor ? node : dependency;
                if (predecessor.Successors.Add(successor))
                {
                    successor.InDegree++;
                }
            }
        }

        private static string FindCycle<TContext, TSystem>(IReadOnlyList<Node<TContext, TSystem>> nodes)
        {
            var states = new Dictionary<Node<TContext, TSystem>, int>();
            var path = new List<Node<TContext, TSystem>>();
            foreach (var node in nodes)
            {
                if (TryFindCycle(node, states, path, out var cycle))
                {
                    return string.Join(" -> ", cycle.Select(item => item.Contribution.Id));
                }
            }

            return "unknown";
        }

        private static bool TryFindCycle<TContext, TSystem>(
            Node<TContext, TSystem> node,
            IDictionary<Node<TContext, TSystem>, int> states,
            IList<Node<TContext, TSystem>> path,
            out IReadOnlyList<Node<TContext, TSystem>> cycle)
        {
            if (states.TryGetValue(node, out var state))
            {
                if (state == 1)
                {
                    var start = path.IndexOf(node);
                    var result = path.Skip(start).ToList();
                    result.Add(node);
                    cycle = result;
                    return true;
                }

                cycle = Array.Empty<Node<TContext, TSystem>>();
                return false;
            }

            states[node] = 1;
            path.Add(node);
            foreach (var successor in node.Successors)
            {
                if (TryFindCycle(successor, states, path, out cycle)) return true;
            }

            path.RemoveAt(path.Count - 1);
            states[node] = 2;
            cycle = Array.Empty<Node<TContext, TSystem>>();
            return false;
        }

        private sealed class Node<TContext, TSystem>
        {
            public Node(IBattleSystemContribution<TContext, TSystem> contribution, int sourceIndex)
            {
                Contribution = contribution;
                SourceIndex = sourceIndex;
            }

            public IBattleSystemContribution<TContext, TSystem> Contribution { get; }

            public int SourceIndex { get; }

            public HashSet<Node<TContext, TSystem>> Successors { get; } = new HashSet<Node<TContext, TSystem>>();

            public int InDegree { get; set; }
        }

        private sealed class NodeComparer<TContext, TSystem> : IComparer<Node<TContext, TSystem>>
        {
            public static NodeComparer<TContext, TSystem> Instance { get; } = new NodeComparer<TContext, TSystem>();

            public int Compare(Node<TContext, TSystem> left, Node<TContext, TSystem> right)
            {
                var byOrder = left.Contribution.Order.CompareTo(right.Contribution.Order);
                return byOrder != 0 ? byOrder : left.SourceIndex.CompareTo(right.SourceIndex);
            }
        }
    }
}
