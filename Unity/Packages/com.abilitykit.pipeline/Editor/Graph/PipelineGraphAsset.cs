using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Pipeline
{
    public sealed class PipelineGraphAsset : ScriptableObject
    {
        public string GraphId;
        public List<Node> Nodes = new List<Node>();
        public List<Edge> Edges = new List<Edge>();

        [Serializable]
        public sealed class Node
        {
            public string NodeId;
            public string RuntimeKey;
            public string DisplayName;
            public string NodeType;
            public Vector2 Position;
            public List<Port> InPorts = new List<Port>();
            public List<Port> OutPorts = new List<Port>();
        }

        [Serializable]
        public sealed class Port
        {
            public string PortId;
            public string DisplayName;
        }

        [Serializable]
        public sealed class Edge
        {
            public string FromNodeId;
            public string FromPortId;
            public string ToNodeId;
            public string ToPortId;
        }

        public PipelineGraphDto ToDto()
        {
            var dto = new PipelineGraphDto
            {
                GraphId = GraphId,
                Nodes = new List<PipelineGraphDto.NodeDto>(Nodes != null ? Nodes.Count : 0),
                Edges = new List<PipelineGraphDto.EdgeDto>(Edges != null ? Edges.Count : 0),
            };

            if (Nodes != null)
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    var n = Nodes[i];
                    if (n == null) continue;
                    dto.Nodes.Add(new PipelineGraphDto.NodeDto
                    {
                        NodeId = n.NodeId,
                        RuntimeKey = n.RuntimeKey,
                        DisplayName = n.DisplayName,
                        NodeType = n.NodeType,
                        X = n.Position.x,
                        Y = n.Position.y,
                        InPorts = ToPortDtos(n.InPorts),
                        OutPorts = ToPortDtos(n.OutPorts),
                    });
                }
            }

            if (Edges != null)
            {
                for (int i = 0; i < Edges.Count; i++)
                {
                    var e = Edges[i];
                    if (e == null) continue;
                    dto.Edges.Add(new PipelineGraphDto.EdgeDto
                    {
                        FromNodeId = e.FromNodeId,
                        FromPortId = e.FromPortId,
                        ToNodeId = e.ToNodeId,
                        ToPortId = e.ToPortId,
                    });
                }
            }

            return dto;
        }

        public void ApplyDto(PipelineGraphDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            GraphId = dto.GraphId;
            Nodes = new List<Node>(dto.Nodes != null ? dto.Nodes.Count : 0);
            Edges = new List<Edge>(dto.Edges != null ? dto.Edges.Count : 0);

            if (dto.Nodes != null)
            {
                for (int i = 0; i < dto.Nodes.Count; i++)
                {
                    var n = dto.Nodes[i];
                    if (n == null) continue;
                    Nodes.Add(new Node
                    {
                        NodeId = n.NodeId,
                        RuntimeKey = n.RuntimeKey,
                        DisplayName = n.DisplayName,
                        NodeType = n.NodeType,
                        Position = new Vector2(n.X, n.Y),
                        InPorts = FromPortDtos(n.InPorts),
                        OutPorts = FromPortDtos(n.OutPorts),
                    });
                }
            }

            if (dto.Edges != null)
            {
                for (int i = 0; i < dto.Edges.Count; i++)
                {
                    var e = dto.Edges[i];
                    if (e == null) continue;
                    Edges.Add(new Edge
                    {
                        FromNodeId = e.FromNodeId,
                        FromPortId = e.FromPortId,
                        ToNodeId = e.ToNodeId,
                        ToPortId = e.ToPortId,
                    });
                }
            }
        }

        private static List<PipelineGraphDto.PortDto> ToPortDtos(List<Port> ports)
        {
            if (ports == null || ports.Count == 0) return new List<PipelineGraphDto.PortDto>(0);
            var list = new List<PipelineGraphDto.PortDto>(ports.Count);
            for (int i = 0; i < ports.Count; i++)
            {
                var p = ports[i];
                if (p == null) continue;
                list.Add(new PipelineGraphDto.PortDto { PortId = p.PortId, DisplayName = p.DisplayName });
            }
            return list;
        }

        private static List<Port> FromPortDtos(List<PipelineGraphDto.PortDto> ports)
        {
            if (ports == null || ports.Count == 0) return new List<Port>(0);
            var list = new List<Port>(ports.Count);
            for (int i = 0; i < ports.Count; i++)
            {
                var p = ports[i];
                if (p == null) continue;
                list.Add(new Port { PortId = p.PortId, DisplayName = p.DisplayName });
            }
            return list;
        }
    }
}
