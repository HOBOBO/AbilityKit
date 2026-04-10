using System.Collections.Generic;
using AbilityKit.Core.Common.Pool;

namespace AbilityKit.Ability
{
    public class AbilityPipelineGraph
    {
        private Dictionary<string, AbilityPipelineNode> _nodes = new();
        private Dictionary<string, List<PipelineConnection>> _connections = new();
        private readonly List<AbilityPipelineNode> _activeNodes = new List<AbilityPipelineNode>(16);

        private static readonly ObjectPool<List<AbilityPipelineNode>> s_nodeListPool = Pools.GetPool(
            createFunc: () => new List<AbilityPipelineNode>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

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
            _activeNodes.Clear();
            foreach (var node in GetStartNodes())
            {
                _activeNodes.Add(node);
            }

            while (_activeNodes.Count > 0)
            {
                var currentNodes = s_nodeListPool.Get();
                currentNodes.AddRange(_activeNodes);
                _activeNodes.Clear();

                try
                {
                    foreach (var node in currentNodes)
                    {
                        var result = node.Execute(context) as AbilityPipelineNodeExecuteResult;
                        try
                        {
                            if (result != null && result.IsCompleted)
                            {
                                // 激活下一个节点
                                ActivateNextNodes(node.Id, result.ActiveOutputPorts, context);
                            }
                            else
                            {
                                _activeNodes.Add(node);
                            }
                        }
                        finally
                        {
                            if (result is System.IDisposable d)
                            {
                                try
                                {
                                    d.Dispose();
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
                finally
                {
                    s_nodeListPool.Release(currentNodes);
                }
            }
        }

        private void ActivateNextNodes(string nodeId, List<string> activePorts, IAbilityPipelineContext context)
        {
            if (activePorts == null || activePorts.Count == 0) return;
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