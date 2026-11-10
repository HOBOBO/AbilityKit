using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor
{
    public enum GraphLayoutMode
    {
        Horizontal,
        Vertical,
        Grid,
        Hierarchy
    }

    /// <summary>Pure editor layout helpers. Positions remain editor metadata on graph nodes.</summary>
    public static class GraphLayoutUtility
    {
        public static void Apply(GraphAsset graph, IReadOnlyList<NodeBase> nodes,
            IReadOnlyList<TransitionEdge> edges, GraphLayoutMode mode)
        {
            if (graph == null || nodes == null || nodes.Count == 0) return;
            Undo.RecordObject(graph, "Auto Layout HFSM Graph");
            switch (mode)
            {
                case GraphLayoutMode.Vertical: LayoutRows(nodes, false); break;
                case GraphLayoutMode.Grid: LayoutGrid(nodes); break;
                case GraphLayoutMode.Hierarchy: LayoutHierarchy(nodes, edges); break;
                default: LayoutRows(nodes, true); break;
            }
            EditorUtility.SetDirty(graph);
        }

        private static void LayoutRows(IReadOnlyList<NodeBase> nodes, bool horizontal)
        {
            const float gap = 50f;
            const float margin = 80f;
            float cursor = margin;
            foreach (var node in nodes)
            {
                if (horizontal)
                {
                    node.Position = new Vector2(cursor, margin);
                    cursor += node.Size.x + gap;
                }
                else
                {
                    node.Position = new Vector2(margin, cursor);
                    cursor += node.Size.y + gap;
                }
            }
        }

        private static void LayoutGrid(IReadOnlyList<NodeBase> nodes)
        {
            const float gap = 40f;
            const float margin = 80f;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(nodes.Count)));
            float maxWidth = nodes.Max(n => n.Size.x);
            float maxHeight = nodes.Max(n => n.Size.y);
            for (int i = 0; i < nodes.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                nodes[i].Position = new Vector2(
                    margin + column * (maxWidth + gap),
                    margin + row * (maxHeight + gap));
            }
        }

        private static void LayoutHierarchy(IReadOnlyList<NodeBase> nodes, IReadOnlyList<TransitionEdge> edges)
        {
            var byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var incoming = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
            var outgoing = nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
            if (edges != null)
            {
                foreach (var edge in edges)
                {
                    if (!byId.ContainsKey(edge.SourceNodeId) || !byId.ContainsKey(edge.TargetNodeId)) continue;
                    outgoing[edge.SourceNodeId].Add(edge.TargetNodeId);
                    incoming[edge.TargetNodeId]++;
                }
            }

            var level = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
            var queue = new Queue<string>(incoming.Where(p => p.Value == 0).Select(p => p.Key));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id)) continue;
                foreach (var target in outgoing[id])
                {
                    level[target] = Mathf.Max(level[target], level[id] + 1);
                    if (--incoming[target] == 0) queue.Enqueue(target);
                }
            }
            // Cycles and disconnected nodes are placed after the reachable DAG levels.
            int fallback = level.Count == 0 ? 0 : level.Values.Max() + 1;
            foreach (var node in nodes)
                if (!visited.Contains(node.Id)) level[node.Id] = fallback;

            const float gapX = 50f;
            const float gapY = 70f;
            const float margin = 80f;
            foreach (var group in nodes.GroupBy(n => level[n.Id]).OrderBy(g => g.Key))
            {
                float x = margin;
                foreach (var node in group.OrderBy(n => n.DisplayName, StringComparer.Ordinal))
                {
                    node.Position = new Vector2(x, margin + group.Key * (80f + gapY));
                    x += node.Size.x + gapX;
                }
            }
        }
    }
}
