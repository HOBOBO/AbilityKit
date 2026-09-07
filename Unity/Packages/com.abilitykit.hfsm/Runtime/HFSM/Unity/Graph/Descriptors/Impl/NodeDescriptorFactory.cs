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
    /// 节点描述器工厂
    /// </summary>
    public static class NodeDescriptorFactory
    {
        public static INodeDescriptor Create(NodeBase node)
        {
            return node switch
            {
                StateNode stateNode => new StateNodeDescriptor(stateNode) as INodeDescriptor,
                StateMachineNode smNode => new StateMachineNodeDescriptor(smNode) as INodeDescriptor,
                _ => new NodeDescriptor(node)
            };
        }

        public static IStateNodeDescriptor CreateState(StateNode node)
        {
            return new StateNodeDescriptor(node);
        }

        public static IStateMachineNodeDescriptor CreateStateMachine(StateMachineNode node)
        {
            return new StateMachineNodeDescriptor(node);
        }
    }
}
