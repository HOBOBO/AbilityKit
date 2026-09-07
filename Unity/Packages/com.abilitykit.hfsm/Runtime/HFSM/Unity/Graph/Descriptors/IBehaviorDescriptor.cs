// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 行为描述器接口 - 描述一个可序列化的行为配置
    /// </summary>
    public interface IBehaviorDescriptor
    {
        string Id { get; }
        string Name { get; }
        string TypeName { get; }
        string ParentId { get; }
        IReadOnlyList<string> ChildIds { get; }
        bool IsExpanded { get; }

        // 参数
        IReadOnlyList<IBehaviorParameterDescriptor> GetParameters();
        bool HasParameter(string name);
        IBehaviorParameterDescriptor GetParameter(string name);
    }
}
