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
    /// 时间经过条件描述器实现
    /// </summary>
    [Serializable]
    public class TimeElapsedConditionDescriptor : ITimeElapsedConditionDescriptor
    {
        private readonly Conditions.TimeElapsedCondition _condition;

        public TimeElapsedConditionDescriptor(Conditions.TimeElapsedCondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public string TypeName => _condition.TypeName;
        public string DisplayName => _condition.DisplayName;
        public string SourceNodeId => _condition.SourceNodeId;
        public float Duration => _condition.Duration;
        public DescriptorCompareOperator Operator => ConvertOperator(_condition.Operator);

        public string GetDescription() => _condition.GetDescription();

        public IDictionary<string, object> ToConfig()
        {
            return _condition.ToConfig();
        }

        private static DescriptorCompareOperator ConvertOperator(Conditions.CompareOperator op)
        {
            return op switch
            {
                Conditions.CompareOperator.Equal => DescriptorCompareOperator.Equal,
                Conditions.CompareOperator.NotEqual => DescriptorCompareOperator.NotEqual,
                Conditions.CompareOperator.GreaterThan => DescriptorCompareOperator.GreaterThan,
                Conditions.CompareOperator.LessThan => DescriptorCompareOperator.LessThan,
                Conditions.CompareOperator.GreaterOrEqual => DescriptorCompareOperator.GreaterOrEqual,
                Conditions.CompareOperator.LessOrEqual => DescriptorCompareOperator.LessOrEqual,
                _ => DescriptorCompareOperator.Equal
            };
        }
    }
}
