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
    /// 条件描述器工厂 - 根据条件类型创建对应的描述器
    /// </summary>
    public static class ConditionDescriptorFactory
    {
        public static IConditionDescriptor Create(Conditions.TransitionCondition condition)
        {
            return condition switch
            {
                Conditions.ParameterCondition paramCondition => new ParameterConditionDescriptor(paramCondition),
                Conditions.TimeElapsedCondition timeCondition => new TimeElapsedConditionDescriptor(timeCondition),
                Conditions.BehaviorCompleteCondition behaviorCondition => new BehaviorCompleteConditionDescriptor(behaviorCondition),
                _ => throw new NotSupportedException($"Unsupported condition type: {condition.GetType().Name}")
            };
        }
    }
}
