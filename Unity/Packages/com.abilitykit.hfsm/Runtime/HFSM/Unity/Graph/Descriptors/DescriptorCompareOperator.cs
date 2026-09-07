// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 比较操作符枚举
    /// </summary>
    public enum DescriptorCompareOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual
    }
}
