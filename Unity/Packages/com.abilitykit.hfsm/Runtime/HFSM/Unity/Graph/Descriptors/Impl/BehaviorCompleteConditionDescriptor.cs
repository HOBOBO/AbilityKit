// ============================================================================
// Condition Descriptor Implementations - 条件描述器实现
// 将现有的 TransitionCondition 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Conditions = AbilityKit.HFSM.Graph.Conditions;
using Params = AbilityKit.HFSM.Graph;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 行为完成条件描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorCompleteConditionDescriptor : IBehaviorCompleteConditionDescriptor
    {
        private readonly Conditions.BehaviorCompleteCondition _condition;

        public BehaviorCompleteConditionDescriptor(Conditions.BehaviorCompleteCondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public string TypeName => _condition.TypeName;
        public string DisplayName => _condition.DisplayName;
        public string SourceNodeId => _condition.SourceNodeId;

        public string GetDescription() => _condition.GetDescription();

        public IDictionary<string, object> ToConfig()
        {
            return _condition.ToConfig();
        }
    }
}
