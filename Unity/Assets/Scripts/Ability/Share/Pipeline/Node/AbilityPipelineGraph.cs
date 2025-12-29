using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Ability
{
    public class AbilityPipelineGraph
    {
        private Dictionary<string, AbilityPipelineNode> _nodes = new();
        private Dictionary<string, List<PipelineConnection>> _connections = new();
        private List<AbilityPipelineNode> _activeNodes = new();
        
        // 连接定义
        public class PipelineConnection
        {
            public string FromNode;
            public string FromPort;
            public string ToNode;
            public string ToPort;
        }
        
        // 添加节点
        public void AddNode(AbilityPipelineNode node)
        {
            _nodes[node.Id] = node;
        }
        
        // 连接节点
        public void Connect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            if (!_connections.ContainsKey(fromNodeId))
            {
                _connections[fromNodeId] = new List<PipelineConnection>();
            }
            
            _connections[fromNodeId].Add(new PipelineConnection
            {
                FromNode = fromNodeId,
                FromPort = fromPort,
                ToNode = toNodeId,
                ToPort = toPort
            });
        }
        
        public IEnumerable<AbilityPipelineNode> GetStartNodes()
        {
            foreach (var node in _nodes.Values)
            {
                if (!_connections.ContainsKey(node.Id))
                {
                    yield return node;
                }
            }
        }
        
        // 执行管线
        public void Execute(IAbilityPipelineContext context)
        {
            // 初始化活动节点
            _activeNodes = GetStartNodes().ToList();
            
            while (_activeNodes.Count > 0)
            {
                var currentNodes = new List<AbilityPipelineNode>(_activeNodes);
                _activeNodes.Clear();
                
                foreach (var node in currentNodes)
                {
                    var result = node.Execute(context) as AbilityPipelineNodeExecuteResult;
                    if (result.IsCompleted)
                    {
                        // 激活下一个节点
                        ActivateNextNodes(node.Id, result.ActiveOutputPorts, context);
                    }
                    else
                    {
                        _activeNodes.Add(node);
                    }
                }
            }
        }
        
        private void ActivateNextNodes(string nodeId, List<string> activePorts, IAbilityPipelineContext context)
        {
            if (_connections.TryGetValue(nodeId, out var connections))
            {
                foreach (var conn in connections)
                {
                    if (activePorts.Contains(conn.FromPort))
                    {
                        var nextNode = _nodes[conn.ToNode];
                        _activeNodes.Add(nextNode);
                    }
                }
            }
        }
    }
}