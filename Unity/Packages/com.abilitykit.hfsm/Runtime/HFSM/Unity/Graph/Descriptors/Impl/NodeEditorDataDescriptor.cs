// ============================================================================
// Editor Data Descriptor Implementations - 编辑器元数据描述器实现
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 节点编辑器元数据描述器实现
    /// </summary>
    public class NodeEditorDataDescriptor : INodeEditorDataDescriptor
    {
        private readonly INodeEditorData _data;

        public NodeEditorDataDescriptor(INodeEditorData data)
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
}
