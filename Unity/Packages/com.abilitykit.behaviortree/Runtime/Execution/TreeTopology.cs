using System;
using System.Collections.Generic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    public sealed class TreeTopology
    {
        private readonly Dictionary<string, int> _nodeIndexById;
        private readonly Dictionary<string, NodeDefinition> _definitionById;

        internal NodeDefinition[] FlatDefinitions { get; }
        internal int[] ParentIndex { get; }
        internal int[][] ChildrenIndex { get; }
        internal int[] RelativeChildIndex { get; }
        internal int[] ParentCompositeIndex { get; }
        internal int[] Depth { get; }

        public int NodeCount => FlatDefinitions.Length;

        private TreeTopology(
            NodeDefinition[] flatDefinitions,
            int[] parentIndex,
            int[][] childrenIndex,
            int[] relativeChildIndex,
            int[] parentCompositeIndex,
            int[] depth,
            Dictionary<string, int> nodeIndexById,
            Dictionary<string, NodeDefinition> definitionById)
        {
            FlatDefinitions = flatDefinitions;
            ParentIndex = parentIndex;
            ChildrenIndex = childrenIndex;
            RelativeChildIndex = relativeChildIndex;
            ParentCompositeIndex = parentCompositeIndex;
            Depth = depth;
            _nodeIndexById = nodeIndexById;
            _definitionById = definitionById;
        }

        public bool TryGetNodeIndex(string nodeId, out int flatIndex)
            => _nodeIndexById.TryGetValue(nodeId, out flatIndex);

        public bool TryGetNodeDefinition(string nodeId, out NodeDefinition definition)
            => _definitionById.TryGetValue(nodeId, out definition!);

        public NodeDefinition GetNodeDefinition(int flatIndex)
        {
            if ((uint)flatIndex >= (uint)FlatDefinitions.Length)
                throw new ArgumentOutOfRangeException(nameof(flatIndex));
            return FlatDefinitions[flatIndex];
        }

        public static TreeTopology Compile(TreeDefinition definition, NodeRegistry registry)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var byId = new Dictionary<string, NodeDefinition>(definition.Nodes.Count, StringComparer.Ordinal);
            foreach (var node in definition.Nodes)
            {
                byId[node.Id] = node;
            }

            var flatDefinitions = new List<NodeDefinition>(definition.Nodes.Count);
            var parentIndex = new List<int>(definition.Nodes.Count);
            var relativeChildIndex = new List<int>(definition.Nodes.Count);
            var parentCompositeIndex = new List<int>(definition.Nodes.Count);
            var childrenIndex = new List<int[]>(definition.Nodes.Count);
            var depth = new List<int>(definition.Nodes.Count);
            var nodeIndexById = new Dictionary<string, int>(definition.Nodes.Count, StringComparer.Ordinal);

            Visit(
                definition.RootNodeId,
                parent: -1,
                relativeIndex: -1,
                parentComposite: -1,
                nodeDepth: 0,
                byId,
                registry,
                flatDefinitions,
                parentIndex,
                relativeChildIndex,
                parentCompositeIndex,
                childrenIndex,
                depth,
                nodeIndexById);

            return new TreeTopology(
                flatDefinitions.ToArray(),
                parentIndex.ToArray(),
                childrenIndex.ToArray(),
                relativeChildIndex.ToArray(),
                parentCompositeIndex.ToArray(),
                depth.ToArray(),
                nodeIndexById,
                byId);
        }

        private static void Visit(
            string nodeId,
            int parent,
            int relativeIndex,
            int parentComposite,
            int nodeDepth,
            Dictionary<string, NodeDefinition> byId,
            NodeRegistry registry,
            List<NodeDefinition> flatDefinitions,
            List<int> parentIndex,
            List<int> relativeChildIndex,
            List<int> parentCompositeIndex,
            List<int[]> childrenIndex,
            List<int> depth,
            Dictionary<string, int> nodeIndexById)
        {
            if (!byId.TryGetValue(nodeId, out var definition))
                throw new InvalidOperationException($"BT node '{nodeId}' not found in definition.");

            var index = flatDefinitions.Count;
            flatDefinitions.Add(definition);
            parentIndex.Add(parent);
            relativeChildIndex.Add(relativeIndex);
            parentCompositeIndex.Add(parentComposite);
            depth.Add(nodeDepth);
            nodeIndexById.Add(nodeId, index);

            var childIndexes = new int[definition.ChildIds.Count];
            childrenIndex.Add(childIndexes);

            registry.TryGetDescriptor(definition.Type, out var descriptor);
            var nextParentComposite = descriptor != null && descriptor.Kind == NodeKind.Composite
                ? index
                : parentComposite;

            for (var k = 0; k < definition.ChildIds.Count; k++)
            {
                childIndexes[k] = flatDefinitions.Count;
                Visit(
                    definition.ChildIds[k],
                    index,
                    k,
                    nextParentComposite,
                    nodeDepth + 1,
                    byId,
                    registry,
                    flatDefinitions,
                    parentIndex,
                    relativeChildIndex,
                    parentCompositeIndex,
                    childrenIndex,
                    depth,
                    nodeIndexById);
            }
        }
    }
}
