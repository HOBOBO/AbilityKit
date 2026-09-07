// ============================================================================
// Node Descriptor Implementations - 节点描述器实现
// 将现有的 NodeBase 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 节点描述器实现 - 适配现有的 NodeBase
    /// </summary>
    public class NodeDescriptor : INodeDescriptor
    {
        protected readonly NodeBase _node;

        public NodeDescriptor(NodeBase node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string Id => _node.Id;
        public string Name => _node.DisplayName;
        public DescriptorNodeType NodeType => ConvertNodeType(_node.NodeType);
        public string ParentStateMachineId => _node.ParentStateMachineId;
        public bool IsDefault => _node.isDefault;

        public virtual string GetNodeTypeDescription() => _node.GetNodeTypeDescription();

        protected static DescriptorNodeType ConvertNodeType(GraphNodeType type)
        {
            return type switch
            {
                GraphNodeType.State => DescriptorNodeType.State,
                GraphNodeType.StateMachine => DescriptorNodeType.StateMachine,
                GraphNodeType.Entry => DescriptorNodeType.Entry,
                GraphNodeType.AnyState => DescriptorNodeType.AnyState,
                _ => DescriptorNodeType.State
            };
        }
    }
}
