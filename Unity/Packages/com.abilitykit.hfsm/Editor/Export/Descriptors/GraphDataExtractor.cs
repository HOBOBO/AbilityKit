// ============================================================================
// Graph Data Extractor - 使用描述器接口的数据提取器
// 从 IGraphDescriptor 提取数据到可序列化的 ExportDto
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHFSM.Graph.Descriptor;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// HFSM 数据提取器实现 - 使用描述器接口
    /// 完全解耦于具体的数据模型类型
    /// </summary>
    public class GraphDataExtractor : IGraphDataExtractor
    {
        public string Name => "Default HFSM Extractor";

        public ExportGraphData Extract(IGraphDescriptor graph, ExportOptions options)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var opts = options ?? ExportOptions.Default;

            var exportedGraph = new ExportGraphData
            {
                version = opts.version,
                graphName = graph.Name,
                exportedAt = DateTime.UtcNow.ToString("O"),
                rootStateMachineId = graph.RootStateMachineId
            };

            // 提取参数
            ExtractParameters(graph, exportedGraph, opts);

            // 提取节点
            ExtractNodes(graph, exportedGraph, opts);

            // 提取边
            ExtractEdges(graph, exportedGraph, opts);

            // 提取编辑器元数据（可选）
            if (opts.includeEditorMetadata)
            {
                exportedGraph.editorMetadata = ExtractEditorMetadata(graph);
            }

            return exportedGraph;
        }

        private ExportEditorMetadata ExtractEditorMetadata(IGraphDescriptor graph)
        {
            var metadata = new ExportEditorMetadata();

            // 提取图编辑器元数据
            var editorData = graph.EditorData;
            if (editorData != null)
            {
                metadata.zoom = editorData.Zoom;
                metadata.panX = editorData.Pan.x;
                metadata.panY = editorData.Pan.y;
                metadata.expandedStateMachineIds.AddRange(editorData.ExpandedStateMachineIds);
            }

            // 提取节点编辑器数据
            foreach (var node in graph.GetNodes())
            {
                var nodeEditorData = graph.GetNodeEditorData(node.Id);
                if (nodeEditorData != null)
                {
                    var nodeData = new ExportNodeEditorData
                    {
                        nodeId = node.Id,
                        positionX = nodeEditorData.Position.x,
                        positionY = nodeEditorData.Position.y,
                        sizeWidth = nodeEditorData.Size.x,
                        sizeHeight = nodeEditorData.Size.y,
                        isExpanded = nodeEditorData.IsExpanded
                    };

                    if (nodeEditorData.CustomColor.HasValue)
                    {
                        nodeData.hasCustomColor = true;
                        nodeData.customColorR = nodeEditorData.CustomColor.Value.r;
                        nodeData.customColorG = nodeEditorData.CustomColor.Value.g;
                        nodeData.customColorB = nodeEditorData.CustomColor.Value.b;
                        nodeData.customColorA = nodeEditorData.CustomColor.Value.a;
                    }

                    metadata.nodeEditorData.Add(nodeData);
                }
            }

            return metadata;
        }

        private void ExtractParameters(IGraphDescriptor graph, ExportGraphData exportedGraph, ExportOptions opts)
        {
            foreach (var param in graph.GetParameters())
            {
                var exported = new ExportParameterData
                {
                    name = param.Name,
                    type = param.ParameterType.ToString(),
                    defaultValue = opts.includeParameterDefaults ? param.GetSerializedDefaultValue() : null
                };
                exportedGraph.parameters.Add(exported);
            }
        }

        private void ExtractNodes(IGraphDescriptor graph, ExportGraphData exportedGraph, ExportOptions opts)
        {
            foreach (var node in graph.GetNodes())
            {
                ExportNodeData exportedNode = null;

                switch (node.NodeType)
                {
                    case DescriptorNodeType.State:
                        exportedNode = ExtractStateNode(node as IStateNodeDescriptor, opts);
                        break;

                    case DescriptorNodeType.StateMachine:
                        exportedNode = ExtractStateMachineNode(node as IStateMachineNodeDescriptor, opts);
                        break;

                    case DescriptorNodeType.AnyState:
                        exportedNode = ExtractAnyStateNode(node, opts);
                        break;

                    default:
                        exportedNode = new ExportNodeData();
                        break;
                }

                // 通用属性
                exportedNode.name = node.Name;
                exportedNode.nodeType = node.NodeType.ToString();
                exportedNode.parentStateMachineId = node.ParentStateMachineId;
                exportedNode.isDefault = node.IsDefault;

                if (opts.includeNodeIds)
                {
                    exportedNode.id = node.Id;
                }

                exportedGraph.nodes.Add(exportedNode);
            }
        }

        private ExportNodeData ExtractStateNode(IStateNodeDescriptor stateNode, ExportOptions opts)
        {
            var exported = new ExportNodeData
            {
                nodeType = "State",
                needsExitTime = stateNode.NeedsExitTime,
                isGhostState = stateNode.IsGhostState,
                hasBehaviors = stateNode.HasBehaviors
            };

            if (opts.includeBehaviors && stateNode.HasBehaviors)
            {
                exported.behaviors = ExtractBehaviors(stateNode.GetBehaviors());
            }

            return exported;
        }

        private ExportNodeData ExtractStateMachineNode(IStateMachineNodeDescriptor smNode, ExportOptions opts)
        {
            var exported = new ExportNodeData
            {
                nodeType = "StateMachine",
                defaultStateId = smNode.DefaultStateId,
                rememberLastState = smNode.RememberLastState
            };

            exported.childNodeIds.AddRange(smNode.GetChildNodeIds());
            exported.transitionIds.AddRange(smNode.GetTransitionIds());
            exported.anyStateTransitionIds.AddRange(smNode.GetAnyStateTransitionIds());

            return exported;
        }

        private ExportNodeData ExtractAnyStateNode(INodeDescriptor node, ExportOptions opts)
        {
            return new ExportNodeData
            {
                nodeType = "AnyState"
            };
        }

        private List<ExportBehaviorData> ExtractBehaviors(IReadOnlyList<IBehaviorDescriptor> behaviors)
        {
            var result = new List<ExportBehaviorData>();

            if (behaviors == null)
                return result;

            foreach (var item in behaviors)
            {
                var exported = new ExportBehaviorData
                {
                    id = item.Id,
                    name = item.Name,
                    type = item.BehaviorType.ToString(),
                    parentId = item.ParentId ?? "",
                    isExpanded = item.IsExpanded
                };

                exported.childIds.AddRange(item.ChildIds);

                foreach (var param in item.GetParameters())
                {
                    exported.parameters.Add(ExtractBehaviorParameter(param));
                }

                result.Add(exported);
            }

            return result;
        }

        private ExportBehaviorParameterData ExtractBehaviorParameter(IBehaviorParameterDescriptor param)
        {
            var exported = new ExportBehaviorParameterData
            {
                name = param.Name,
                valueType = param.ValueType.ToString()
            };

            switch (param.ValueType)
            {
                case DescriptorBehaviorParameterType.Float:
                    exported.floatValue = param.GetFloatValue();
                    break;
                case DescriptorBehaviorParameterType.Int:
                    exported.intValue = param.GetIntValue();
                    break;
                case DescriptorBehaviorParameterType.Bool:
                    exported.boolValue = param.GetBoolValue();
                    break;
                case DescriptorBehaviorParameterType.String:
                    exported.stringValue = param.GetStringValue();
                    break;
                case DescriptorBehaviorParameterType.Object:
                    exported.objectReference = param.GetObjectValue()?.ToString();
                    break;
                case DescriptorBehaviorParameterType.Vector2:
                    var v2 = param.GetVector2Value();
                    exported.vector2X = v2.x;
                    exported.vector2Y = v2.y;
                    break;
                case DescriptorBehaviorParameterType.Vector3:
                    var v3 = param.GetVector3Value();
                    exported.vector3X = v3.x;
                    exported.vector3Y = v3.y;
                    exported.vector3Z = v3.z;
                    break;
                case DescriptorBehaviorParameterType.Color:
                    var c = param.GetColorValue();
                    exported.colorR = c.r;
                    exported.colorG = c.g;
                    exported.colorB = c.b;
                    exported.colorA = c.a;
                    break;
            }

            return exported;
        }

        private void ExtractEdges(IGraphDescriptor graph, ExportGraphData exportedGraph, ExportOptions opts)
        {
            foreach (var edge in graph.GetEdges())
            {
                var exported = new ExportEdgeData
                {
                    sourceNodeId = edge.SourceNodeId,
                    targetNodeId = edge.TargetNodeId,
                    priority = edge.Priority,
                    isExitTransition = edge.IsExitTransition,
                    forceInstantly = edge.ForceInstantly,
                    useAndLogic = edge.UseAndLogic
                };

                if (opts.includeNodeIds)
                {
                    exported.id = edge.Id;
                }

                if (opts.includeConditions && edge.HasConditions)
                {
                    foreach (var condition in edge.GetConditions())
                    {
                        exported.conditions.Add(ExtractCondition(condition));
                    }
                }

                exportedGraph.edges.Add(exported);
            }
        }

        private ExportConditionData ExtractCondition(IConditionDescriptor condition)
        {
            var exported = new ExportConditionData
            {
                typeName = condition.TypeName,
                displayName = condition.DisplayName
            };

            if (condition is IParameterConditionDescriptor paramCondition)
            {
                exported.parameterName = paramCondition.ParameterName;
                exported.parameterType = paramCondition.ParameterType.ToString();
                exported.compareOperator = paramCondition.Operator.ToString();
                exported.boolValue = paramCondition.GetBoolValue();
                exported.floatValue = paramCondition.GetFloatValue();
                exported.intValue = paramCondition.GetIntValue();
            }
            else if (condition is ITimeElapsedConditionDescriptor timeCondition)
            {
                exported.sourceNodeId = timeCondition.SourceNodeId;
                exported.duration = timeCondition.Duration;
                exported.compareOperator = timeCondition.Operator.ToString();
            }
            else if (condition is IBehaviorCompleteConditionDescriptor behaviorCondition)
            {
                exported.sourceNodeId = behaviorCondition.SourceNodeId;
            }

            return exported;
        }
    }
}
