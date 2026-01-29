#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    public static class PipelineGraphSyncUtility
    {
        public static void SyncLinearFromPipelinePhases(PipelineGraphAsset asset, object pipeline, string graphId)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            var phases = TryGetPhases(pipeline);
            if (phases == null)
            {
                Debug.LogError($"[PipelineGraphSyncUtility] Failed to read phases from pipeline: {pipeline.GetType().FullName}");
                return;
            }

            Undo.RecordObject(asset, "Sync Pipeline Graph Asset");

            var oldNodes = asset.Nodes ?? new List<PipelineGraphAsset.Node>(0);
            var posByKey = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            for (int i = 0; i < oldNodes.Count; i++)
            {
                var n = oldNodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.RuntimeKey)) continue;
                posByKey[n.RuntimeKey] = n.Position;
            }

            // Build expanded graph (supports composite/parallel/conditional) but keeps linear fallback.
            var nodesByKey = new Dictionary<string, PipelineGraphAsset.Node>(StringComparer.Ordinal);
            var edges = new List<PipelineGraphAsset.Edge>(Mathf.Max(0, phases.Count - 1));

            PipelineGraphAsset.Node prevTop = null;
            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                if (p == null) continue;

                var top = GetOrCreateNode(nodesByKey, posByKey, p, new Vector2(i * 260f, 0f));
                if (prevTop != null)
                {
                    edges.Add(MakeEdge(prevTop.NodeId, "out", top.NodeId, "in"));
                }
                prevTop = top;

                ExpandPhaseGraph(nodesByKey, posByKey, edges, p, parent: top, depth: 0);
            }

            var nodes = new List<PipelineGraphAsset.Node>(nodesByKey.Count);
            foreach (var kv in nodesByKey) nodes.Add(kv.Value);

            EnsurePorts(nodes, edges);

            asset.GraphId = !string.IsNullOrEmpty(asset.GraphId) ? asset.GraphId : (graphId ?? string.Empty);
            asset.Nodes = nodes;
            asset.Edges = edges;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PipelineGraphSyncUtility] Synced graph asset. Nodes={nodes.Count} Edges={edges.Count}");
        }

        private static void ExpandPhaseGraph(
            Dictionary<string, PipelineGraphAsset.Node> nodesByKey,
            Dictionary<string, Vector2> posByKey,
            List<PipelineGraphAsset.Edge> edges,
            object phase,
            PipelineGraphAsset.Node parent,
            int depth)
        {
            if (phase == null) return;
            if (parent == null) return;
            if (depth > 6) return; // avoid pathological graphs

            var kind = GetPhaseKind(phase);
            var children = GetChildren(phase, kind);
            if (children == null || children.Count == 0) return;

            // Layout: place children below parent; spread X for parallel/conditional.
            var basePos = posByKey.TryGetValue(parent.RuntimeKey ?? string.Empty, out var pp)
                ? pp
                : parent.Position;

            if (kind == PhaseKind.Sequence)
            {
                PipelineGraphAsset.Node prev = null;
                for (int i = 0; i < children.Count; i++)
                {
                    var c = children[i];
                    if (c == null) continue;
                    var suggested = basePos + new Vector2(0f, (depth + 1) * 90f + i * 70f);
                    var childNode = GetOrCreateNode(nodesByKey, posByKey, c, suggested);

                    if (i == 0)
                        edges.Add(MakeEdge(parent.NodeId, "out", childNode.NodeId, "in"));
                    if (prev != null)
                        edges.Add(MakeEdge(prev.NodeId, "out", childNode.NodeId, "in"));

                    prev = childNode;
                    ExpandPhaseGraph(nodesByKey, posByKey, edges, c, childNode, depth + 1);
                }
                return;
            }

            // Parallel / Conditional: connect parent -> each child.
            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                if (c == null) continue;
                var suggested = basePos + new Vector2((i - (children.Count - 1) * 0.5f) * 240f, (depth + 1) * 110f);
                var childNode = GetOrCreateNode(nodesByKey, posByKey, c, suggested);

                var fromPort = kind == PhaseKind.Parallel ? $"par[{i}]" : $"branch[{i}]";
                edges.Add(MakeEdge(parent.NodeId, fromPort, childNode.NodeId, "in"));

                ExpandPhaseGraph(nodesByKey, posByKey, edges, c, childNode, depth + 1);
            }
        }

        private enum PhaseKind
        {
            Unknown,
            Sequence,
            Parallel,
            Conditional,
        }

        private static PhaseKind GetPhaseKind(object phase)
        {
            if (phase == null) return PhaseKind.Unknown;
            var n = phase.GetType().Name;
            if (n.IndexOf("Conditional", StringComparison.OrdinalIgnoreCase) >= 0) return PhaseKind.Conditional;
            if (n.IndexOf("Parallel", StringComparison.OrdinalIgnoreCase) >= 0) return PhaseKind.Parallel;
            if (n.IndexOf("Sequence", StringComparison.OrdinalIgnoreCase) >= 0) return PhaseKind.Sequence;
            // Any composite defaults to sequence-ish.
            var pIsComposite = phase.GetType().GetProperty("IsComposite", BindingFlags.Instance | BindingFlags.Public);
            if (pIsComposite != null)
            {
                var v = pIsComposite.GetValue(phase);
                if (v is bool b && b) return PhaseKind.Sequence;
            }
            return PhaseKind.Unknown;
        }

        private static List<object> GetChildren(object phase, PhaseKind kind)
        {
            if (phase == null) return null;

            // ConditionalPhase in this codebase keeps branches in a private field `_branches`.
            if (kind == PhaseKind.Conditional)
            {
                var f = phase.GetType().GetField("_branches", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null)
                {
                    var v = f.GetValue(phase) as IEnumerable;
                    if (v != null)
                    {
                        var res = new List<object>(8);
                        foreach (var b in v)
                        {
                            if (b == null) continue;
                            var p = b.GetType().GetProperty("Phase", BindingFlags.Instance | BindingFlags.Public);
                            if (p == null) continue;
                            var pv = p.GetValue(b);
                            if (pv != null) res.Add(pv);
                        }
                        return res;
                    }
                }
            }

            // Generic path: SubPhases property.
            var pSub = phase.GetType().GetProperty("SubPhases", BindingFlags.Instance | BindingFlags.Public);
            if (pSub != null)
            {
                var v = pSub.GetValue(phase) as IEnumerable;
                if (v != null)
                {
                    var res = new List<object>(8);
                    foreach (var it in v)
                    {
                        if (it != null) res.Add(it);
                    }
                    return res;
                }
            }
            return null;
        }

        private static PipelineGraphAsset.Edge MakeEdge(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            return new PipelineGraphAsset.Edge
            {
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                ToNodeId = toNodeId,
                ToPortId = toPortId,
            };
        }

        private static PipelineGraphAsset.Node GetOrCreateNode(
            Dictionary<string, PipelineGraphAsset.Node> nodesByKey,
            Dictionary<string, Vector2> posByKey,
            object phase,
            Vector2 suggestedPos)
        {
            var runtimeKey = GetPhaseIdString(phase);
            if (string.IsNullOrEmpty(runtimeKey)) runtimeKey = phase.GetType().Name;

            if (nodesByKey.TryGetValue(runtimeKey, out var existing) && existing != null)
            {
                return existing;
            }

            var nodeId = $"phase:{runtimeKey}";
            var displayName = phase.GetType().Name;

            var pos = posByKey.TryGetValue(runtimeKey, out var oldPos)
                ? oldPos
                : suggestedPos;

            var n = new PipelineGraphAsset.Node
            {
                NodeId = nodeId,
                RuntimeKey = runtimeKey,
                DisplayName = displayName,
                NodeType = phase.GetType().FullName,
                Position = pos,
                InPorts = new List<PipelineGraphAsset.Port> { new PipelineGraphAsset.Port { PortId = "in", DisplayName = "In" } },
                OutPorts = new List<PipelineGraphAsset.Port> { new PipelineGraphAsset.Port { PortId = "out", DisplayName = "Out" } },
            };
            nodesByKey[runtimeKey] = n;
            return n;
        }

        private static void EnsurePorts(List<PipelineGraphAsset.Node> nodes, List<PipelineGraphAsset.Edge> edges)
        {
            if (nodes == null || nodes.Count == 0) return;
            if (edges == null || edges.Count == 0) return;

            var byId = new Dictionary<string, PipelineGraphAsset.Node>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.NodeId)) continue;
                byId[n.NodeId] = n;
                n.InPorts ??= new List<PipelineGraphAsset.Port>(1);
                n.OutPorts ??= new List<PipelineGraphAsset.Port>(1);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e == null) continue;

                if (!string.IsNullOrEmpty(e.FromNodeId) && byId.TryGetValue(e.FromNodeId, out var from))
                {
                    EnsurePort(from.OutPorts, e.FromPortId, e.FromPortId);
                }

                if (!string.IsNullOrEmpty(e.ToNodeId) && byId.TryGetValue(e.ToNodeId, out var to))
                {
                    EnsurePort(to.InPorts, e.ToPortId, e.ToPortId);
                }
            }
        }

        private static void EnsurePort(List<PipelineGraphAsset.Port> ports, string portId, string display)
        {
            if (ports == null) return;
            if (string.IsNullOrEmpty(portId)) portId = "out";
            for (int i = 0; i < ports.Count; i++)
            {
                var p = ports[i];
                if (p == null) continue;
                if (string.Equals(p.PortId, portId, StringComparison.Ordinal)) return;
            }
            ports.Add(new PipelineGraphAsset.Port { PortId = portId, DisplayName = display });
        }

        private static List<object> TryGetPhases(object pipeline)
        {
            var t = pipeline.GetType();

            var f = t.GetField("_phases", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return null;
            var enumerable = f.GetValue(pipeline) as IEnumerable;
            if (enumerable == null) return null;

            var result = new List<object>(16);
            foreach (var it in enumerable)
            {
                if (it != null) result.Add(it);
            }
            return result;
        }

        private static string GetPhaseIdString(object phase)
        {
            if (phase == null) return string.Empty;
            var p = phase.GetType().GetProperty("PhaseId", BindingFlags.Instance | BindingFlags.Public);
            if (p == null) return phase.GetType().Name;
            var v = p.GetValue(phase);
            return v != null ? v.ToString() : string.Empty;
        }
    }
}

#endif
