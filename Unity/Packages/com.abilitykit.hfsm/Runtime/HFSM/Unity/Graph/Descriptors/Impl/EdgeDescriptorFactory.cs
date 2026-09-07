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
    /// 边描述器工厂
    /// </summary>
    public static class EdgeDescriptorFactory
    {
        public static IEdgeDescriptor Create(TransitionEdge edge)
        {
            return new EdgeDescriptor(edge);
        }

        public static List<IEdgeDescriptor> CreateRange(IEnumerable<TransitionEdge> edges)
        {
            return edges?.Select(Create).ToList() ?? new List<IEdgeDescriptor>();
        }
    }
}
