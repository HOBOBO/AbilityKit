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
    /// 参数描述器工厂
    /// </summary>
    public static class ParameterDescriptorFactory
    {
        public static IParameterDescriptor Create(Parameter parameter)
        {
            return new ParameterDescriptor(parameter);
        }

        public static List<IParameterDescriptor> CreateRange(IEnumerable<Parameter> parameters)
        {
            return parameters?.Select(Create).ToList() ?? new List<IParameterDescriptor>();
        }
    }
}
