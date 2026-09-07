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
    /// 状态机节点描述器实现
    /// </summary>
    public class StateMachineNodeDescriptor : NodeDescriptor, IStateMachineNodeDescriptor
    {
        private readonly StateMachineNode _smNode;

        public StateMachineNodeDescriptor(StateMachineNode smNode) : base(smNode)
        {
            _smNode = smNode ?? throw new ArgumentNullException(nameof(smNode));
        }

        public string DefaultStateId => _smNode.DefaultStateId;
        public bool RememberLastState => _smNode.RememberLastState;

        public override string GetNodeTypeDescription() => _smNode.GetNodeTypeDescription();

        public IReadOnlyList<string> GetChildNodeIds() => _smNode.ChildNodeIds;
        public IReadOnlyList<string> GetTransitionIds() => _smNode.TransitionIds;
        public IReadOnlyList<string> GetAnyStateTransitionIds() => _smNode.AnyStateTransitionIds;
    }
}
