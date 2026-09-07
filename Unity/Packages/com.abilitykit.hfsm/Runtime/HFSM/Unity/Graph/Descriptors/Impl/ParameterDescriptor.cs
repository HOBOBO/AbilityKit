// ============================================================================
// Edge and Parameter Descriptor Implementations - 边和参数描述器实现
// 将现有的 TransitionEdge 和 Parameter 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 参数描述器实现
    /// </summary>
    [Serializable]
    public class ParameterDescriptor : IParameterDescriptor
    {
        private readonly Parameter _parameter;

        public ParameterDescriptor(Parameter parameter)
        {
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public string Name => _parameter.Name;
        public DescriptorParameterType ParameterType => ConvertParameterType(_parameter.ParameterType);

        public object GetSerializedDefaultValue() => _parameter.GetSerializedDefaultValue();

        private static DescriptorParameterType ConvertParameterType(ParameterValueType type)
        {
            return type switch
            {
                ParameterValueType.Bool => DescriptorParameterType.Bool,
                ParameterValueType.Float => DescriptorParameterType.Float,
                ParameterValueType.Int => DescriptorParameterType.Int,
                ParameterValueType.Trigger => DescriptorParameterType.Trigger,
                _ => DescriptorParameterType.Bool
            };
        }
    }
}
