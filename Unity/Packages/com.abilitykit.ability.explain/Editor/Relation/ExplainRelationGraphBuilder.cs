using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    internal static class ExplainRelationGraphBuilder
    {
        public static ExplainRelationGraph Build(ExplainForest forest, Dictionary<string, ExplainTreeRoot> expandedRoots = null)
        {
            if (forest == null) return null;

            var g = new ExplainRelationGraph();

            if (forest.Roots != null)
            {
                for (var i = 0; i < forest.Roots.Count; i++)
                {
                    var r = forest.Roots[i];
                    if (r?.Root == null) continue;
                    WalkNode(r.Root, depth: 0, parentNodeId: null, g);
                }
            }

            if (forest.Discovered != null && forest.Discovered.Count > 0)
            {
                g.DiscoveredStartIndex = g.Nodes.Count;
                for (var i = 0; i < forest.Discovered.Count; i++)
                {
                    var d = forest.Discovered[i];
                    if (d == null) continue;

                    var k = d.Key;
                    if (string.IsNullOrEmpty(k.Type) || string.IsNullOrEmpty(k.Id)) continue;

                    var entryId = $"discovered:{k.Type}:{k.Id}";
                    if (!g.NodeIndex.TryGetValue(entryId, out var entry))
                    {
                        entry = new ExplainRelationNode
                        {
                            NodeId = entryId,
                            ParentNodeId = null,
                            Title = $"自发现 {k.Type}#{k.Id}",
                            Kind = "discovered_entry",
                            Depth = 0
                        };
                        g.Nodes.Add(entry);
                        g.NodeIndex[entryId] = entry;
                    }

                    AddEntityOnly(k, entryId, entry, g);

                    if (expandedRoots != null && expandedRoots.TryGetValue(k.ToString(), out var expandedRoot)
                        && expandedRoot != null && expandedRoot.Root != null)
                    {
                        WalkDiscoveredTree(expandedRoot.Root, entryId, g);
                    }
                }
            }

            return g;
        }

        private static void WalkDiscoveredTree(ExplainNode root, string entryNodeId, ExplainRelationGraph g)
        {
            if (root == null || string.IsNullOrEmpty(entryNodeId) || g == null) return;

            void Walk(ExplainNode n, int depth, string parentId)
            {
                if (n == null) return;

                var nodeId = string.IsNullOrEmpty(n.NodeId) ? Guid.NewGuid().ToString("N") : n.NodeId;
                var relNodeId = $"{entryNodeId}:{nodeId}";

                if (!g.NodeIndex.TryGetValue(relNodeId, out var rel))
                {
                    rel = new ExplainRelationNode
                    {
                        NodeId = relNodeId,
                        ParentNodeId = parentId,
                        Title = n.Title,
                        Kind = n.Kind,
                        Depth = depth
                    };
                    g.Nodes.Add(rel);
                    g.NodeIndex[relNodeId] = rel;

                    if (!string.IsNullOrEmpty(parentId) && g.NodeIndex.TryGetValue(parentId, out var p))
                    {
                        if (p.ChildrenNodeIds != null && !p.ChildrenNodeIds.Contains(relNodeId)) p.ChildrenNodeIds.Add(relNodeId);
                    }
                }

                AddReferences(n, relNodeId, rel, g);

                if (n.Children == null) return;
                for (var i = 0; i < n.Children.Count; i++)
                {
                    Walk(n.Children[i], depth + 1, relNodeId);
                }
            }

            Walk(root, depth: 1, parentId: entryNodeId);
        }

        private static void AddReferences(ExplainNode n, string nodeId, ExplainRelationNode node, ExplainRelationGraph g)
        {
            if (n == null || node == null || g == null) return;

            if (n.Source != null && n.Source.Kind == "table_row")
            {
                var k = new PipelineItemKey(n.Source.TableName, n.Source.RowId);
                AddEntityOnly(k, nodeId, node, g);
            }

            if (n.Actions != null)
            {
                for (var i = 0; i < n.Actions.Count; i++)
                {
                    var a = n.Actions[i];
                    var t = a != null ? a.NavigateTo : null;
                    if (t != null && t.Kind == "open_table_row")
                    {
                        var k = new PipelineItemKey(t.TableName, t.RowId);
                        AddEntityOnly(k, nodeId, node, g);
                    }
                }
            }
        }

        private static void AddEntityOnly(PipelineItemKey k, string nodeId, ExplainRelationNode node, ExplainRelationGraph g)
        {
            if (g == null || node == null) return;
            if (string.IsNullOrEmpty(k.Type) || string.IsNullOrEmpty(k.Id)) return;

            var keyStr = k.ToString();
            if (!g.EntityIndex.TryGetValue(keyStr, out var ent))
            {
                ent = new ExplainRelationEntity { Key = k };
                g.EntityIndex[keyStr] = ent;
            }

            if (!node.ReferencedEntities.Exists(e => e.Type == k.Type && e.Id == k.Id))
            {
                node.ReferencedEntities.Add(k);
            }

            if (!ent.ReferencedByNodeIds.Contains(nodeId))
            {
                ent.ReferencedByNodeIds.Add(nodeId);
            }
        }

        private static void WalkNode(ExplainNode n, int depth, string parentNodeId, ExplainRelationGraph g)
        {
            if (n == null || g == null) return;

            var id = n.NodeId;
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
            }

            if (!g.NodeIndex.TryGetValue(id, out var node))
            {
                node = new ExplainRelationNode
                {
                    NodeId = id,
                    ParentNodeId = parentNodeId,
                    Title = n.Title,
                    Kind = n.Kind,
                    Depth = depth
                };
                g.Nodes.Add(node);
                g.NodeIndex[id] = node;
            }

            if (node.ParentNodeId != parentNodeId)
            {
                node.ParentNodeId = parentNodeId;
            }

            if (!string.IsNullOrEmpty(parentNodeId) && g.NodeIndex.TryGetValue(parentNodeId, out var parent) && parent != null)
            {
                if (!parent.ChildrenNodeIds.Contains(node.NodeId)) parent.ChildrenNodeIds.Add(node.NodeId);
            }

            ExtractRefs(n, node, g);

            if (n.Children == null) return;
            for (var i = 0; i < n.Children.Count; i++)
            {
                WalkNode(n.Children[i], depth + 1, parentNodeId: node.NodeId, g);
            }
        }

        private static void ExtractRefs(ExplainNode n, ExplainRelationNode node, ExplainRelationGraph g)
        {
            if (n == null || node == null || g == null) return;

            void AddEntity(PipelineItemKey k)
            {
                if (string.IsNullOrEmpty(k.Type) || string.IsNullOrEmpty(k.Id)) return;

                var keyStr = k.ToString();
                for (var i = 0; i < node.ReferencedEntities.Count; i++)
                {
                    var e = node.ReferencedEntities[i];
                    if (e.Type == k.Type && e.Id == k.Id) return;
                }

                node.ReferencedEntities.Add(k);

                if (!g.EntityIndex.TryGetValue(keyStr, out var ent))
                {
                    ent = new ExplainRelationEntity { Key = k };
                    g.EntityIndex[keyStr] = ent;
                }

                if (!ent.ReferencedByNodeIds.Contains(node.NodeId))
                {
                    ent.ReferencedByNodeIds.Add(node.NodeId);
                }
            }

            if (n.Source != null && n.Source.Kind == "table_row")
            {
                AddEntity(new PipelineItemKey(n.Source.TableName, n.Source.RowId));
            }

            if (n.Actions != null)
            {
                for (var i = 0; i < n.Actions.Count; i++)
                {
                    var t = n.Actions[i]?.NavigateTo;
                    if (t == null) continue;

                    if (t.Kind == "open_table_row")
                    {
                        AddEntity(new PipelineItemKey(t.TableName, t.RowId));
                        continue;
                    }

                    if (t.Kind == "open_editor" && t.Extra != null && t.Extra.TryGetValue("type", out var type) && t.Extra.TryGetValue("id", out var id))
                    {
                        AddEntity(new PipelineItemKey(type, id));
                    }
                }
            }
        }
    }
}
