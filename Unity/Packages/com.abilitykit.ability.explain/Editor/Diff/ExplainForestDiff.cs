using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    internal static class ExplainForestDiff
    {
        public static Dictionary<string, ExplainDiffKind> BuildDiffMap(ExplainForest @base, ExplainForest current)
        {
            var baseIndex = BuildNodeIndex(@base);
            var currentIndex = BuildNodeIndex(current);

            var map = new Dictionary<string, ExplainDiffKind>(StringComparer.Ordinal);

            foreach (var kv in currentIndex)
            {
                if (!baseIndex.TryGetValue(kv.Key, out var baseSnap))
                {
                    map[kv.Key] = ExplainDiffKind.Added;
                    continue;
                }

                if (!kv.Value.Equals(baseSnap))
                {
                    map[kv.Key] = ExplainDiffKind.Changed;
                }
            }

            foreach (var kv in baseIndex)
            {
                if (!currentIndex.ContainsKey(kv.Key))
                {
                    map[kv.Key] = ExplainDiffKind.Removed;
                }
            }

            return map;
        }

        public static ExplainForest BuildForestWithRemoved(ExplainForest @base, ExplainForest current, Dictionary<string, ExplainDiffKind> diffMap)
        {
            if (@base == null || current == null || diffMap == null) return current;

            var removedRoots = CollectRemovedAsRoots(@base, diffMap);
            if (removedRoots == null || removedRoots.Count <= 0) return current;

            var next = new ExplainForest();
            if (current.Roots != null) next.Roots.AddRange(current.Roots);
            if (current.Discovered != null) next.Discovered = new List<ExplainTreeDiscovery>(current.Discovered);

            next.Roots.Add(new ExplainTreeRoot
            {
                Kind = "diff_removed",
                Key = default,
                Title = "Removed",
                Root = new ExplainNode
                {
                    NodeId = Guid.NewGuid().ToString("N"),
                    Kind = "diff_removed_root",
                    Title = "Removed",
                    Children = removedRoots
                }
            });

            return next;
        }

        private static List<ExplainNode> CollectRemovedAsRoots(ExplainForest @base, Dictionary<string, ExplainDiffKind> diffMap)
        {
            if (@base?.Roots == null) return null;

            var removed = new List<ExplainNode>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var r in @base.Roots)
            {
                if (r?.Root == null) continue;
                Walk(r.Root);
            }

            return removed;

            void Walk(ExplainNode n)
            {
                if (n == null) return;
                if (!string.IsNullOrEmpty(n.NodeId)
                    && diffMap.TryGetValue(n.NodeId, out var k)
                    && k == ExplainDiffKind.Removed
                    && seen.Add(n.NodeId))
                {
                    removed.Add(n);
                }

                if (n.Children == null) return;
                foreach (var c in n.Children) Walk(c);
            }
        }

        private static Dictionary<string, NodeSnapshot> BuildNodeIndex(ExplainForest forest)
        {
            var dict = new Dictionary<string, NodeSnapshot>(StringComparer.Ordinal);
            if (forest?.Roots == null) return dict;

            foreach (var r in forest.Roots)
            {
                if (r?.Root == null) continue;
                Walk(r.Root);
            }

            return dict;

            void Walk(ExplainNode n)
            {
                if (n == null) return;
                if (!string.IsNullOrEmpty(n.NodeId) && !dict.ContainsKey(n.NodeId))
                {
                    dict[n.NodeId] = NodeSnapshot.From(n);
                }

                if (n.Children == null) return;
                foreach (var c in n.Children) Walk(c);
            }
        }

        private readonly struct NodeSnapshot : IEquatable<NodeSnapshot>
        {
            private readonly string _title;
            private readonly ExplainSeverity _severity;
            private readonly string _summary;

            private NodeSnapshot(string title, ExplainSeverity severity, string summary)
            {
                _title = title ?? string.Empty;
                _severity = severity;
                _summary = summary ?? string.Empty;
            }

            public static NodeSnapshot From(ExplainNode n)
            {
                var summary = n?.SummaryLines != null && n.SummaryLines.Count > 0
                    ? string.Join("\n", n.SummaryLines)
                    : string.Empty;
                return new NodeSnapshot(n?.Title, n != null ? n.Severity : ExplainSeverity.None, summary);
            }

            public bool Equals(NodeSnapshot other)
            {
                return _severity == other._severity
                       && string.Equals(_title, other._title, StringComparison.Ordinal)
                       && string.Equals(_summary, other._summary, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is NodeSnapshot other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_title, (int)_severity, _summary);
        }
    }
}
