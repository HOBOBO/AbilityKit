// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 参数描述器接口
    /// </summary>
    public interface IParameterDescriptor
    {
        string Name { get; }
        DescriptorParameterType ParameterType { get; }

        /// <summary>获取序列化的默认值</summary>
        object GetSerializedDefaultValue();
    }
}
