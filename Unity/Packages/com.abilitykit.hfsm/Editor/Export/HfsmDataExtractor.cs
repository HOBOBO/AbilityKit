using System;
using System.Collections.Generic;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// HFSM 数据提取器实现
    /// </summary>
    public class HfsmDataExtractor : IHfsmDataExtractor
    {
        private ExportOptions _options;

        public ExportedGraph Extract(object graph, ExportOptions options)
        {
            _options = options ?? ExportOptions.Default;

            var hfsmGraph = graph as HfsmGraphAsset;
            if (hfsmGraph == null)
            {
                throw new ArgumentException("Graph must be of type HfsmGraphAsset");
            }

            var exportedGraph = new ExportedGraph
            {
                version = _options.version,
                graphName = hfsmGraph.GraphName,
                exportedAt = DateTime.UtcNow.ToString("O"),
                rootStateMachineId = hfsmGraph.RootStateMachineId
            };

            ExtractParameters(hfsmGraph, exportedGraph);
            ExtractNodes(hfsmGraph, exportedGraph);
            ExtractEdges(hfsmGraph, exportedGraph);

            return exportedGraph;
        }

        private void ExtractParameters(HfsmGraphAsset graph, ExportedGraph exportedGraph)
        {
            if (graph.Parameters == null)
                return;

            foreach (var param in graph.Parameters)
            {
                var exportedParam = new ExportedParameter
                {
                    name = param.Name,
                    type = param.ParameterType.ToString(),
                    defaultValue = param.GetSerializedDefaultValue()
                };
                exportedGraph.parameters.Add(exportedParam);
            }
        }

        private void ExtractNodes(HfsmGraphAsset graph, ExportedGraph exportedGraph)
        {
            if (graph.Nodes == null)
                return;

            foreach (var node in graph.Nodes)
            {
                ExportedNode exportedNode = null;

                switch (node)
                {
                    case HfsmStateNode stateNode:
                        exportedNode = ExtractStateNode(stateNode);
                        break;

                    case HfsmStateMachineNode smNode:
                        exportedNode = ExtractStateMachineNode(smNode);
                        break;
                }

                if (exportedNode != null)
                {
                    // 基础属性
                    exportedNode.name = node.GetName();
                    exportedNode.parentStateMachineId = node.ParentStateMachineId;
                    exportedNode.isDefault = node.isDefault;

                    if (_options.includeNodeIds)
                    {
                        exportedNode.id = node.Id;
                    }

                    if (_options.includeEditorMetadata)
                    {
                        exportedNode.positionX = node.Position.x;
                        exportedNode.positionY = node.Position.y;
                        exportedNode.sizeWidth = node.Size.x;
                        exportedNode.sizeHeight = node.Size.y;
                    }

                    exportedGraph.nodes.Add(exportedNode);
                }
            }
        }

        private ExportedNode ExtractStateNode(HfsmStateNode stateNode)
        {
            var exported = new ExportedNode
            {
                nodeType = "State",
                needsExitTime = stateNode.NeedsExitTime,
                isGhostState = stateNode.IsGhostState,
                hasBehaviors = stateNode.HasBehaviors
            };

            if (_options.includeBehaviors && stateNode.BehaviorItems != null)
            {
                exported.behaviors = ExtractBehaviors(stateNode.BehaviorItems);
            }

            return exported;
        }

        private ExportedNode ExtractStateMachineNode(HfsmStateMachineNode smNode)
        {
            var exported = new ExportedNode
            {
                nodeType = "StateMachine",
                defaultStateId = smNode.DefaultStateId,
                rememberLastState = smNode.RememberLastState
            };

            if (smNode.ChildNodeIds != null)
            {
                exported.childNodeIds.AddRange(smNode.ChildNodeIds);
            }

            if (smNode.TransitionIds != null)
            {
                exported.transitionIds.AddRange(smNode.TransitionIds);
            }

            if (smNode.AnyStateTransitionIds != null)
            {
                exported.anyStateTransitionIds.AddRange(smNode.AnyStateTransitionIds);
            }

            return exported;
        }

        private List<ExportedBehaviorItem> ExtractBehaviors(IReadOnlyList<HfsmBehaviorItem> items)
        {
            var result = new List<ExportedBehaviorItem>();

            if (items == null)
                return result;

            foreach (var item in items)
            {
                var exported = new ExportedBehaviorItem
                {
                    id = item.id,
                    name = item.displayName,
                    type = item.Type.ToString(),
                    parentId = item.parentId ?? "",
                    isExpanded = item.isExpanded
                };

                if (item.childIds != null)
                {
                    exported.childIds.AddRange(item.childIds);
                }

                if (item.parameters != null)
                {
                    foreach (var param in item.parameters)
                    {
                        exported.parameters.Add(ExportedBehaviorParameter.FromBehaviorParameter(param));
                    }
                }

                result.Add(exported);
            }

            return result;
        }

        private void ExtractEdges(HfsmGraphAsset graph, ExportedGraph exportedGraph)
        {
            if (graph.Edges == null)
                return;

            foreach (var edge in graph.Edges)
            {
                var exported = new ExportedEdge
                {
                    sourceNodeId = edge.SourceNodeId,
                    targetNodeId = edge.TargetNodeId,
                    priority = edge.Priority,
                    isExitTransition = edge.IsExitTransition,
                    forceInstantly = edge.ForceInstantly,
                    useAndLogic = edge.UseAndLogic
                };

                if (_options.includeNodeIds)
                {
                    exported.id = edge.Id;
                }

                if (_options.includeConditions && edge.Conditions != null && edge.Conditions.Count > 0)
                {
                    foreach (var condition in edge.Conditions)
                    {
                        exported.conditions.Add(ExportedCondition.FromCondition(condition));
                    }
                }

                exportedGraph.edges.Add(exported);
            }
        }
    }
}
