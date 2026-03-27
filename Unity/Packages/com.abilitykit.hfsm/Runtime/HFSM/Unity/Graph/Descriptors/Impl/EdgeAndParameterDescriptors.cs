// ============================================================================
// Edge and Parameter Descriptor Implementations - 边和参数描述器实现
// 将现有的 HfsmTransitionEdge 和 HfsmParameter 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 边（转换）描述器实现
    /// </summary>
    public class EdgeDescriptor : IEdgeDescriptor
    {
        private readonly HfsmTransitionEdge _edge;
        private List<IConditionDescriptor> _conditionDescriptors;

        public EdgeDescriptor(HfsmTransitionEdge edge)
        {
            _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        }

        public string Id => _edge.Id;
        public string SourceNodeId => _edge.SourceNodeId;
        public string TargetNodeId => _edge.TargetNodeId;
        public int Priority => _edge.Priority;
        public bool IsExitTransition => _edge.IsExitTransition;
        public bool ForceInstantly => _edge.ForceInstantly;
        public bool UseAndLogic => _edge.UseAndLogic;

        public bool HasConditions
        {
            get
            {
                EnsureConditionsLoaded();
                return _conditionDescriptors.Count > 0;
            }
        }

        public IReadOnlyList<IConditionDescriptor> GetConditions()
        {
            EnsureConditionsLoaded();
            return _conditionDescriptors;
        }

        public string GetConditionSummary() => _edge.GetConditionSummary();

        private void EnsureConditionsLoaded()
        {
            if (_conditionDescriptors == null)
            {
                _conditionDescriptors = new List<IConditionDescriptor>();
                if (_edge.Conditions != null)
                {
                    foreach (var condition in _edge.Conditions)
                    {
                        _conditionDescriptors.Add(ConditionDescriptorFactory.Create(condition));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 边描述器工厂
    /// </summary>
    public static class EdgeDescriptorFactory
    {
        public static IEdgeDescriptor Create(HfsmTransitionEdge edge)
        {
            return new EdgeDescriptor(edge);
        }

        public static List<IEdgeDescriptor> CreateRange(IEnumerable<HfsmTransitionEdge> edges)
        {
            return edges?.Select(Create).ToList() ?? new List<IEdgeDescriptor>();
        }
    }

    /// <summary>
    /// 参数描述器实现
    /// </summary>
    [Serializable]
    public class ParameterDescriptor : IParameterDescriptor
    {
        private readonly HfsmParameter _parameter;

        public ParameterDescriptor(HfsmParameter parameter)
        {
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public string Name => _parameter.Name;
        public DescriptorParameterType ParameterType => ConvertParameterType(_parameter.ParameterType);

        public object GetSerializedDefaultValue() => _parameter.GetSerializedDefaultValue();

        private static DescriptorParameterType ConvertParameterType(HfsmParameterType type)
        {
            return type switch
            {
                HfsmParameterType.Bool => DescriptorParameterType.Bool,
                HfsmParameterType.Float => DescriptorParameterType.Float,
                HfsmParameterType.Int => DescriptorParameterType.Int,
                HfsmParameterType.Trigger => DescriptorParameterType.Trigger,
                _ => DescriptorParameterType.Bool
            };
        }
    }

    /// <summary>
    /// 参数描述器工厂
    /// </summary>
    public static class ParameterDescriptorFactory
    {
        public static IParameterDescriptor Create(HfsmParameter parameter)
        {
            return new ParameterDescriptor(parameter);
        }

        public static List<IParameterDescriptor> CreateRange(IEnumerable<HfsmParameter> parameters)
        {
            return parameters?.Select(Create).ToList() ?? new List<IParameterDescriptor>();
        }
    }
}
