// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 参数类型枚举
    /// </summary>
    public enum DescriptorParameterType
    {
        Bool,
        Float,
        Int,
        Trigger
    }
}
