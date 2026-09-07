// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{
    /// <summary>
    /// 节点类型枚举
    /// </summary>
    public enum DescriptorNodeType
    {
        /// <summary>叶子状态</summary>
        State,

        /// <summary>嵌套状态机</summary>
        StateMachine,

        /// <summary>入口点</summary>
        Entry,

        /// <summary>任意状态（用于全局转换）</summary>
        AnyState
    }
}
