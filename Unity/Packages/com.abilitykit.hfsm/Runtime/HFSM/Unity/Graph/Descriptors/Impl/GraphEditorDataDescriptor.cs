// ============================================================================
// Editor Data Descriptor Implementations - 编辑器元数据描述器实现
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 图编辑器元数据描述器实现
    /// </summary>
    public class GraphEditorDataDescriptor : IGraphEditorDataDescriptor
    {
        private readonly GraphEditorData _data;

        public GraphEditorDataDescriptor(GraphEditorData data)
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
}
