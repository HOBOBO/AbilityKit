using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;

namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 生成节点调试全量与差量投影，只缓存静态描述，不参与树的执行或快照恢复。
    /// </summary>
    internal sealed class TreeDebugProjection
    {
        private NodeDebugStaticInfo[] _debugStaticInfos = Array.Empty<NodeDebugStaticInfo>();

        private sealed class NodeDebugStaticInfo
        {
            public string NodeId = "";
            public string Name = "";
            public string TypeId = "";
            public NodeKind Kind;
            public int Depth;
            public string? SourceTreeId;
        }

        internal void Rebuild(NodeDefinition[] definitions, TreeTopology topology,
            IReadOnlyDictionary<string, string>? nodeSourceTree, NodeRegistry registry)
        {
            _debugStaticInfos = new NodeDebugStaticInfo[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var sourceTree = nodeSourceTree != null
                    && nodeSourceTree.TryGetValue(definition.Id, out var src)
                    ? src
                    : null;
                registry.TryGetDescriptor(definition.Type, out var descriptor);
                _debugStaticInfos[i] = new NodeDebugStaticInfo
                {
                    NodeId = definition.Id,
                    Name = descriptor != null ? descriptor.DisplayName : definition.Type,
                    TypeId = definition.Type,
                    Kind = descriptor != null ? descriptor.Kind : NodeKind.Action,
                    Depth = topology.Depth[i],
                    SourceTreeId = sourceTree,
                };
            }
        }

        internal List<NodeDebugInfo> BuildNodeDebugInfos(
            NodeBase[] nodes, List<List<int>> runStacks,
            bool full,
            NodeState[]? previousStates,
            int[]? previousOnStack,
            int[]? previousRunningChild)
        {
            CaptureDebugDynamicState(nodes, runStacks, out var states, out var onStack, out var runningChild);
            var result = new List<NodeDebugInfo>(full ? nodes.Length : 0);
            for (var i = 0; i < nodes.Length; i++)
            {
                if (!full
                    && previousStates != null
                    && previousOnStack != null
                    && previousRunningChild != null
                    && previousStates.Length == nodes.Length
                    && previousOnStack.Length == nodes.Length
                    && previousRunningChild.Length == nodes.Length
                    && previousStates[i] == states[i]
                    && previousOnStack[i] == onStack[i]
                    && previousRunningChild[i] == runningChild[i])
                {
                    continue;
                }

                var info = _debugStaticInfos[i];
                result.Add(new NodeDebugInfo(
                    info.NodeId,
                    info.Name,
                    info.TypeId,
                    info.Kind,
                    states[i],
                    info.Depth,
                    onStack[i],
                    runningChild[i],
                    info.SourceTreeId));
            }
            return result;
        }

        internal static void CaptureDebugDynamicState(
            NodeBase[] nodes, List<List<int>> runStacks,
            out NodeState[] states,
            out int[] onStack,
            out int[] runningChild)
        {
            states = new NodeState[nodes.Length];
            onStack = new int[nodes.Length];
            runningChild = new int[nodes.Length];
            for (var i = 0; i < runningChild.Length; i++) runningChild[i] = -1;

            foreach (var stack in runStacks)
            {
                foreach (var index in stack)
                {
                    onStack[index]++;
                }
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                states[i] = nodes[i].State;
                runningChild[i] = nodes[i] is ParentNodeBase parent ? parent.CaptureRunningIndex() : -1;
            }
        }
    }
}
