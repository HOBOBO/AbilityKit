// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 边（转换）描述器接口
    /// </summary>
    public interface IEdgeDescriptor
    {
        string Id { get; }
        string SourceNodeId { get; }
        string TargetNodeId { get; }
        int Priority { get; }
        bool IsExitTransition { get; }
        bool ForceInstantly { get; }
        bool UseAndLogic { get; }

        /// <summary>是否有条件</summary>
        bool HasConditions { get; }

        /// <summary>获取所有条件描述器</summary>
        IReadOnlyList<IConditionDescriptor> GetConditions();

        /// <summary>获取条件摘要文本</summary>
        string GetConditionSummary();
    }
}
