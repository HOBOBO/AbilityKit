using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    [Serializable]
    public sealed class PipelineGraphDto
    {
        public string GraphId;
        public List<NodeDto> Nodes;
        public List<EdgeDto> Edges;

        [Serializable]
        public sealed class NodeDto
        {
            public string NodeId;
            public string RuntimeKey;
            public string DisplayName;
            public string NodeType;
            public float X;
            public float Y;
            public List<PortDto> InPorts;
            public List<PortDto> OutPorts;
        }

        [Serializable]
        public sealed class PortDto
        {
            public string PortId;
            public string DisplayName;
        }

        [Serializable]
        public sealed class EdgeDto
        {
            public string FromNodeId;
            public string FromPortId;
            public string ToNodeId;
            public string ToPortId;
        }
    }
}
