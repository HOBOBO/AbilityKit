// ============================================================================
// Node Descriptor Implementations - 节点描述器实现
// 将现有的 HfsmNodeBase 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 节点描述器实现 - 适配现有的 HfsmNodeBase
    /// </summary>
    public class NodeDescriptor : INodeDescriptor
    {
        protected readonly HfsmNodeBase _node;

        public NodeDescriptor(HfsmNodeBase node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string Id => _node.Id;
        public string Name => _node.DisplayName;
        public DescriptorNodeType NodeType => ConvertNodeType(_node.NodeType);
        public string ParentStateMachineId => _node.ParentStateMachineId;
        public bool IsDefault => _node.isDefault;

        public virtual string GetNodeTypeDescription() => _node.GetNodeTypeDescription();

        protected static DescriptorNodeType ConvertNodeType(HfsmNodeType type)
        {
            return type switch
            {
                HfsmNodeType.State => DescriptorNodeType.State,
                HfsmNodeType.StateMachine => DescriptorNodeType.StateMachine,
                HfsmNodeType.Entry => DescriptorNodeType.Entry,
                HfsmNodeType.AnyState => DescriptorNodeType.AnyState,
                _ => DescriptorNodeType.State
            };
        }
    }

    /// <summary>
    /// 状态节点描述器实现
    /// </summary>
    public class StateNodeDescriptor : NodeDescriptor, IStateNodeDescriptor
    {
        private readonly HfsmStateNode _stateNode;

        public StateNodeDescriptor(HfsmStateNode stateNode) : base(stateNode)
        {
            _stateNode = stateNode ?? throw new ArgumentNullException(nameof(stateNode));
        }

        public bool NeedsExitTime => _stateNode.NeedsExitTime;
        public bool IsGhostState => _stateNode.IsGhostState;
        public bool HasBehaviors => _stateNode.HasBehaviors;

        public override string GetNodeTypeDescription() => _stateNode.GetNodeTypeDescription();

        public IReadOnlyList<string> GetEntryActionMethodNames() => _stateNode.EntryActionMethodNames;
        public IReadOnlyList<string> GetLogicActionMethodNames() => _stateNode.LogicActionMethodNames;
        public IReadOnlyList<string> GetExitActionMethodNames() => _stateNode.ExitActionMethodNames;
        public IReadOnlyList<string> GetCanExitMethodNames() => _stateNode.CanExitMethodNames;

        public IReadOnlyList<IBehaviorDescriptor> GetBehaviors()
        {
            return BehaviorDescriptorFactory.CreateRange(_stateNode.BehaviorItems);
        }

        public IReadOnlyList<IBehaviorDescriptor> GetRootBehaviors()
        {
            return BehaviorDescriptorFactory.CreateRange(_stateNode.GetRootBehaviorItems());
        }

        public IBehaviorDescriptor GetBehavior(string id)
        {
            var item = _stateNode.GetBehaviorItem(id);
            return item != null ? new BehaviorDescriptor(item) : null;
        }
    }

    /// <summary>
    /// 状态机节点描述器实现
    /// </summary>
    public class StateMachineNodeDescriptor : NodeDescriptor, IStateMachineNodeDescriptor
    {
        private readonly HfsmStateMachineNode _smNode;

        public StateMachineNodeDescriptor(HfsmStateMachineNode smNode) : base(smNode)
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

    /// <summary>
    /// 节点描述器工厂
    /// </summary>
    public static class NodeDescriptorFactory
    {
        public static INodeDescriptor Create(HfsmNodeBase node)
        {
            return node switch
            {
                HfsmStateNode stateNode => new StateNodeDescriptor(stateNode) as INodeDescriptor,
                HfsmStateMachineNode smNode => new StateMachineNodeDescriptor(smNode) as INodeDescriptor,
                _ => new NodeDescriptor(node)
            };
        }

        public static IStateNodeDescriptor CreateState(HfsmStateNode node)
        {
            return new StateNodeDescriptor(node);
        }

        public static IStateMachineNodeDescriptor CreateStateMachine(HfsmStateMachineNode node)
        {
            return new StateMachineNodeDescriptor(node);
        }
    }
}
