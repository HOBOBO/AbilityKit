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
    /// 状态节点描述器实现
    /// </summary>
    public class StateNodeDescriptor : NodeDescriptor, IStateNodeDescriptor
    {
        private readonly StateNode _stateNode;

        public StateNodeDescriptor(StateNode stateNode) : base(stateNode)
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
}
