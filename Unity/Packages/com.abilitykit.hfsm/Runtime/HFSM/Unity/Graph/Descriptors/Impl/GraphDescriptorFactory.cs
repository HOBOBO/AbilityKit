// ============================================================================
// Graph Descriptor Implementation - 图描述器实现
// 将 GraphAsset 适配到 IGraphDescriptor 接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 图描述器工厂
    /// </summary>
    public static class GraphDescriptorFactory
    {
        /// <summary>
        /// 从 GraphAsset 创建图描述器
        /// </summary>
        public static IGraphDescriptor Create(GraphAsset asset)
        {
            return new GraphDescriptor(asset);
        }
    }
}
