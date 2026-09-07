// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 状态机节点描述器接口
    /// </summary>
    public interface IStateMachineNodeDescriptor : INodeDescriptor
    {
        /// <summary>默认状态 ID</summary>
        string DefaultStateId { get; }

        /// <summary>是否记住最后状态</summary>
        bool RememberLastState { get; }

        /// <summary>子节点 ID 列表</summary>
        IReadOnlyList<string> GetChildNodeIds();

        /// <summary>转换 ID 列表</summary>
        IReadOnlyList<string> GetTransitionIds();

        /// <summary>任意状态转换 ID 列表</summary>
        IReadOnlyList<string> GetAnyStateTransitionIds();
    }
}
