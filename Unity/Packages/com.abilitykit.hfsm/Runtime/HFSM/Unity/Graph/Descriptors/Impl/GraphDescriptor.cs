// ============================================================================
// Graph Descriptor Implementation - 图描述器实现
// 将 HfsmGraphAsset 适配到 IGraphDescriptor 接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 图描述器实现 - 适配 HfsmGraphAsset 到 IGraphDescriptor 接口
    /// 这是将现有 HfsmGraphAsset 与描述器系统集成的桥梁
    /// </summary>
    public class GraphDescriptor : IGraphDescriptor
    {
        private readonly HfsmGraphAsset _asset;

        // 缓存
        private List<INodeDescriptor> _nodeDescriptors;
        private List<IEdgeDescriptor> _edgeDescriptors;
        private List<IParameterDescriptor> _parameterDescriptors;

        // 编辑器元数据描述器
        private GraphEditorDataDescriptor _editorDataDescriptor;
        private Dictionary<string, NodeEditorDataDescriptor> _nodeEditorDataDescriptors;

        public GraphDescriptor(HfsmGraphAsset asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        /// <summary>
        /// 获取底层资产引用
        /// </summary>
        public HfsmGraphAsset Asset => _asset;

        public string Name => _asset.GraphName;
        public string RootStateMachineId => _asset.RootStateMachineId;

        public IReadOnlyList<INodeDescriptor> GetNodes()
        {
            if (_nodeDescriptors == null)
            {
                _nodeDescriptors = _asset.Nodes
                    .Select(NodeDescriptorFactory.Create)
                    .ToList();
            }
            return _nodeDescriptors;
        }

        public IReadOnlyList<IEdgeDescriptor> GetEdges()
        {
            if (_edgeDescriptors == null)
            {
                _edgeDescriptors = EdgeDescriptorFactory.CreateRange(_asset.Edges);
            }
            return _edgeDescriptors;
        }

        public IReadOnlyList<IParameterDescriptor> GetParameters()
        {
            if (_parameterDescriptors == null)
            {
                _parameterDescriptors = ParameterDescriptorFactory.CreateRange(_asset.Parameters);
            }
            return _parameterDescriptors;
        }

        public IStateMachineNodeDescriptor GetRootStateMachine()
        {
            var root = _asset.GetRootStateMachine();
            return root != null ? new StateMachineNodeDescriptor(root) : null;
        }

        public INodeDescriptor GetNodeById(string id)
        {
            var node = _asset.GetNodeById(id);
            return node != null ? NodeDescriptorFactory.Create(node) : null;
        }

        public IEdgeDescriptor GetEdgeById(string id)
        {
            var edge = _asset.GetEdgeById(id);
            return edge != null ? new EdgeDescriptor(edge) : null;
        }

        public T GetNodeById<T>(string id) where T : INodeDescriptor
        {
            var node = _asset.GetNodeById(id);
            if (node == null)
                return default;

            if (typeof(T) == typeof(INodeDescriptor))
                return (T)(INodeDescriptor)NodeDescriptorFactory.Create(node);

            if (typeof(T) == typeof(IStateNodeDescriptor) && node is HfsmStateNode stateNode)
                return (T)(INodeDescriptor)new StateNodeDescriptor(stateNode);

            if (typeof(T) == typeof(IStateMachineNodeDescriptor) && node is HfsmStateMachineNode smNode)
                return (T)(INodeDescriptor)new StateMachineNodeDescriptor(smNode);

            return default;
        }

        public IReadOnlyList<IEdgeDescriptor> GetOutgoingEdges(string nodeId)
        {
            return EdgeDescriptorFactory.CreateRange(_asset.GetOutgoingEdges(nodeId));
        }

        public IReadOnlyList<IEdgeDescriptor> GetIncomingEdges(string nodeId)
        {
            return EdgeDescriptorFactory.CreateRange(_asset.GetIncomingEdges(nodeId));
        }

        public IParameterDescriptor GetParameterByName(string name)
        {
            var param = _asset.GetParameterByName(name);
            return param != null ? new ParameterDescriptor(param) : null;
        }

        public bool Validate()
        {
            return _asset.Validate();
        }

        // ========== 编辑器元数据实现 ==========

        /// <summary>
        /// 获取图编辑器元数据
        /// </summary>
        public IGraphEditorDataDescriptor EditorData
        {
            get
            {
                if (_editorDataDescriptor == null)
                {
                    _editorDataDescriptor = new GraphEditorDataDescriptor(_asset.EditorData);
                }
                return _editorDataDescriptor;
            }
        }

        /// <summary>
        /// 获取节点的编辑器元数据
        /// </summary>
        public INodeEditorDataDescriptor GetNodeEditorData(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return EmptyNodeEditorDataDescriptor.Instance;

            EnsureNodeEditorDataCacheInitialized();
            if (_nodeEditorDataDescriptors.TryGetValue(nodeId, out var descriptor))
            {
                return descriptor;
            }

            // 如果没有编辑器数据，返回节点自身的 Position/Size
            var node = _asset.GetNodeById(nodeId);
            if (node != null)
            {
                // 创建包装器，使用节点的 Position/Size
                return new NodePositionSizeDescriptor(node);
            }

            return EmptyNodeEditorDataDescriptor.Instance;
        }

        private void EnsureNodeEditorDataCacheInitialized()
        {
            if (_nodeEditorDataDescriptors == null)
            {
                _nodeEditorDataDescriptors = new Dictionary<string, NodeEditorDataDescriptor>();

                // 尝试从编辑器数据中获取
                if (_asset.EditorData != null)
                {
                    foreach (var data in _asset.EditorData.GetAllNodeEditorData())
                    {
                        _nodeEditorDataDescriptors[data.NodeId] = new NodeEditorDataDescriptor(
                            new HfsmNodeEditorData(data.NodeId, data.Position, data.Size)
                            {
                                IsExpanded = data.IsExpanded,
                                CustomColor = data.CustomColor
                            }
                        );
                    }
                }
            }
        }

        /// <summary>
        /// 节点位置/大小描述器 - 使用节点自身的 Position/Size
        /// </summary>
        private class NodePositionSizeDescriptor : INodeEditorDataDescriptor
        {
            private readonly HfsmNodeBase _node;

            public NodePositionSizeDescriptor(HfsmNodeBase node)
            {
                _node = node;
            }

            public string NodeId => _node.Id;

            public UnityEngine.Vector2 Position
            {
                get => _node.Position;
                set => _node.Position = value;
            }

            public UnityEngine.Vector2 Size
            {
                get => _node.Size;
                set => _node.Size = value;
            }

            public bool IsExpanded
            {
                get => true;
                set { }
            }

            public UnityEngine.Color? CustomColor
            {
                get => null;
                set { }
            }
        }

        /// <summary>
        /// 刷新缓存（当底层数据变化时调用）
        /// </summary>
        public void InvalidateCache()
        {
            _nodeDescriptors = null;
            _edgeDescriptors = null;
            _parameterDescriptors = null;
            _editorDataDescriptor = null;
            _nodeEditorDataDescriptors = null;
        }
    }

    /// <summary>
    /// 图描述器工厂
    /// </summary>
    public static class GraphDescriptorFactory
    {
        /// <summary>
        /// 从 HfsmGraphAsset 创建图描述器
        /// </summary>
        public static IGraphDescriptor Create(HfsmGraphAsset asset)
        {
            return new GraphDescriptor(asset);
        }
    }
}
