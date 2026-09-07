// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 状态节点描述器接口
    /// </summary>
    public interface IStateNodeDescriptor : INodeDescriptor
    {
        /// <summary>是否需要退出时间</summary>
        bool NeedsExitTime { get; }

        /// <summary>是否为幽灵状态（不出现在活跃路径中）</summary>
        bool IsGhostState { get; }

        /// <summary>是否有行为定义</summary>
        bool HasBehaviors { get; }

        // 方法访问
        IReadOnlyList<string> GetEntryActionMethodNames();
        IReadOnlyList<string> GetLogicActionMethodNames();
        IReadOnlyList<string> GetExitActionMethodNames();
        IReadOnlyList<string> GetCanExitMethodNames();

        // 行为访问
        IReadOnlyList<IBehaviorDescriptor> GetBehaviors();
        IReadOnlyList<IBehaviorDescriptor> GetRootBehaviors();
        IBehaviorDescriptor GetBehavior(string id);
    }
}
