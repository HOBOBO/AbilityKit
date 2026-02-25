using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    [Serializable]
    internal sealed class ExplainRelationGraph
    {
        public readonly List<ExplainRelationNode> Nodes = new List<ExplainRelationNode>();
        public readonly Dictionary<string, ExplainRelationNode> NodeIndex = new Dictionary<string, ExplainRelationNode>();
        public readonly Dictionary<string, ExplainRelationEntity> EntityIndex = new Dictionary<string, ExplainRelationEntity>();

        public int DiscoveredStartIndex = -1;
    }

    [Serializable]
    internal sealed class ExplainRelationNode
    {
        public string NodeId;
        public string ParentNodeId;
        public string Title;
        public string Kind;
        public int Depth;
        public readonly List<PipelineItemKey> ReferencedEntities = new List<PipelineItemKey>();

        public readonly List<string> ChildrenNodeIds = new List<string>();
    }

    [Serializable]
    internal sealed class ExplainRelationEntity
    {
        public PipelineItemKey Key;
        public readonly List<string> ReferencedByNodeIds = new List<string>();
    }
}
