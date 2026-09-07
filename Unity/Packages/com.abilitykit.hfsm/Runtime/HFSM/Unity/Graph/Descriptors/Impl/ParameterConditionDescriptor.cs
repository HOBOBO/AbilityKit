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
    /// 参数比较条件描述器实现
    /// </summary>
    [Serializable]
    public class ParameterConditionDescriptor : IParameterConditionDescriptor
    {
        private readonly Conditions.ParameterCondition _condition;

        public ParameterConditionDescriptor(Conditions.ParameterCondition condition)
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

        private static DescriptorParameterType ConvertParameterType(Params.ParameterValueType type)
        {
            return type switch
            {
                Params.ParameterValueType.Bool => DescriptorParameterType.Bool,
                Params.ParameterValueType.Float => DescriptorParameterType.Float,
                Params.ParameterValueType.Int => DescriptorParameterType.Int,
                Params.ParameterValueType.Trigger => DescriptorParameterType.Int, // Trigger 用 Int 表示
                _ => DescriptorParameterType.Bool
            };
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
