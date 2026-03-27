// ============================================================================
// Condition Descriptor Implementations - 条件描述器实现
// 将现有的 HfsmTransitionCondition 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using HfsmConditions = UnityHFSM.Graph.Conditions;
using HfsmParams = UnityHFSM.Graph;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 参数比较条件描述器实现
    /// </summary>
    [Serializable]
    public class ParameterConditionDescriptor : IParameterConditionDescriptor
    {
        private readonly HfsmConditions.HfsmParameterCondition _condition;

        public ParameterConditionDescriptor(HfsmConditions.HfsmParameterCondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public string TypeName => _condition.TypeName;
        public string DisplayName => _condition.DisplayName;
        public string ParameterName => _condition.ParameterName;
        public DescriptorParameterType ParameterType => ConvertParameterType(_condition.ParameterType);
        public DescriptorCompareOperator Operator => ConvertOperator(_condition.Operator);

        public string GetDescription() => _condition.GetDescription();

        public IDictionary<string, object> ToConfig()
        {
            return _condition.ToConfig();
        }

        public bool GetBoolValue() => _condition.BoolValue;
        public float GetFloatValue() => _condition.FloatValue;
        public int GetIntValue() => _condition.IntValue;

        private static DescriptorParameterType ConvertParameterType(HfsmParams.HfsmParameterType type)
        {
            return type switch
            {
                HfsmParams.HfsmParameterType.Bool => DescriptorParameterType.Bool,
                HfsmParams.HfsmParameterType.Float => DescriptorParameterType.Float,
                HfsmParams.HfsmParameterType.Int => DescriptorParameterType.Int,
                HfsmParams.HfsmParameterType.Trigger => DescriptorParameterType.Int, // Trigger 用 Int 表示
                _ => DescriptorParameterType.Bool
            };
        }

        private static DescriptorCompareOperator ConvertOperator(HfsmConditions.HfsmCompareOperator op)
        {
            return op switch
            {
                HfsmConditions.HfsmCompareOperator.Equal => DescriptorCompareOperator.Equal,
                HfsmConditions.HfsmCompareOperator.NotEqual => DescriptorCompareOperator.NotEqual,
                HfsmConditions.HfsmCompareOperator.GreaterThan => DescriptorCompareOperator.GreaterThan,
                HfsmConditions.HfsmCompareOperator.LessThan => DescriptorCompareOperator.LessThan,
                HfsmConditions.HfsmCompareOperator.GreaterOrEqual => DescriptorCompareOperator.GreaterOrEqual,
                HfsmConditions.HfsmCompareOperator.LessOrEqual => DescriptorCompareOperator.LessOrEqual,
                _ => DescriptorCompareOperator.Equal
            };
        }
    }

    /// <summary>
    /// 时间经过条件描述器实现
    /// </summary>
    [Serializable]
    public class TimeElapsedConditionDescriptor : ITimeElapsedConditionDescriptor
    {
        private readonly HfsmConditions.HfsmTimeElapsedCondition _condition;

        public TimeElapsedConditionDescriptor(HfsmConditions.HfsmTimeElapsedCondition condition)
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

        private static DescriptorCompareOperator ConvertOperator(HfsmConditions.HfsmCompareOperator op)
        {
            return op switch
            {
                HfsmConditions.HfsmCompareOperator.Equal => DescriptorCompareOperator.Equal,
                HfsmConditions.HfsmCompareOperator.NotEqual => DescriptorCompareOperator.NotEqual,
                HfsmConditions.HfsmCompareOperator.GreaterThan => DescriptorCompareOperator.GreaterThan,
                HfsmConditions.HfsmCompareOperator.LessThan => DescriptorCompareOperator.LessThan,
                HfsmConditions.HfsmCompareOperator.GreaterOrEqual => DescriptorCompareOperator.GreaterOrEqual,
                HfsmConditions.HfsmCompareOperator.LessOrEqual => DescriptorCompareOperator.LessOrEqual,
                _ => DescriptorCompareOperator.Equal
            };
        }
    }

    /// <summary>
    /// 行为完成条件描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorCompleteConditionDescriptor : IBehaviorCompleteConditionDescriptor
    {
        private readonly HfsmConditions.HfsmBehaviorCompleteCondition _condition;

        public BehaviorCompleteConditionDescriptor(HfsmConditions.HfsmBehaviorCompleteCondition condition)
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

    /// <summary>
    /// 条件描述器工厂 - 根据条件类型创建对应的描述器
    /// </summary>
    public static class ConditionDescriptorFactory
    {
        public static IConditionDescriptor Create(HfsmConditions.HfsmTransitionCondition condition)
        {
            return condition switch
            {
                HfsmConditions.HfsmParameterCondition paramCondition => new ParameterConditionDescriptor(paramCondition),
                HfsmConditions.HfsmTimeElapsedCondition timeCondition => new TimeElapsedConditionDescriptor(timeCondition),
                HfsmConditions.HfsmBehaviorCompleteCondition behaviorCondition => new BehaviorCompleteConditionDescriptor(behaviorCondition),
                _ => throw new NotSupportedException($"Unsupported condition type: {condition.GetType().Name}")
            };
        }
    }
}
