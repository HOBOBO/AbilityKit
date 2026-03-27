// ============================================================================
// Editor Data Descriptor Implementations - 编辑器元数据描述器实现
// ============================================================================

using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 节点编辑器元数据描述器实现
    /// </summary>
    public class NodeEditorDataDescriptor : INodeEditorDataDescriptor
    {
        private readonly IHfsmNodeEditorData _data;

        public NodeEditorDataDescriptor(IHfsmNodeEditorData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string NodeId => _data.NodeId;

        public UnityEngine.Vector2 Position
        {
            get => _data.Position;
            set => _data.Position = value;
        }

        public UnityEngine.Vector2 Size
        {
            get => _data.Size;
            set => _data.Size = value;
        }

        public bool IsExpanded
        {
            get => _data.IsExpanded;
            set => _data.IsExpanded = value;
        }

        public UnityEngine.Color? CustomColor
        {
            get => _data.CustomColor;
            set => _data.CustomColor = value;
        }
    }

    /// <summary>
    /// 图编辑器元数据描述器实现
    /// </summary>
    public class GraphEditorDataDescriptor : IGraphEditorDataDescriptor
    {
        private readonly HfsmGraphEditorData _data;

        public GraphEditorDataDescriptor(HfsmGraphEditorData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public float Zoom
        {
            get => _data.Zoom;
            set => _data.Zoom = value;
        }

        public UnityEngine.Vector2 Pan
        {
            get => _data.Pan;
            set => _data.Pan = value;
        }

        public IReadOnlyList<string> ExpandedStateMachineIds => _data.ExpandedStateMachineIds;

        public bool IsExpanded(string stateMachineId)
        {
            return _data.IsExpanded(stateMachineId);
        }

        public INodeEditorDataDescriptor GetNodeEditorData(string nodeId)
        {
            var data = _data.GetNodeEditorData(nodeId);
            return data != null ? new NodeEditorDataDescriptor(data) : null;
        }

        public INodeEditorDataDescriptor GetOrCreateNodeEditorData(string nodeId)
        {
            var data = _data.GetOrCreateNodeEditorData(nodeId);
            return new NodeEditorDataDescriptor(data);
        }
    }

    /// <summary>
    /// 空编辑器元数据描述器（当没有编辑器数据时返回）
    /// </summary>
    public class EmptyNodeEditorDataDescriptor : INodeEditorDataDescriptor
    {
        private static readonly EmptyNodeEditorDataDescriptor _instance = new EmptyNodeEditorDataDescriptor();

        public static EmptyNodeEditorDataDescriptor Instance => _instance;

        private EmptyNodeEditorDataDescriptor() { }

        public string NodeId => string.Empty;

        public UnityEngine.Vector2 Position
        {
            get => UnityEngine.Vector2.zero;
            set { }
        }

        public UnityEngine.Vector2 Size
        {
            get => new UnityEngine.Vector2(150, 60);
            set { }
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
}
