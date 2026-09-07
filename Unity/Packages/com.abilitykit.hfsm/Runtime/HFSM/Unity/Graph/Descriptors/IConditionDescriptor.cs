// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 条件描述器接口 - 描述一个可序列化的转换条件
    /// </summary>
    public interface IConditionDescriptor
    {
        string TypeName { get; }
        string DisplayName { get; }

        // 获取描述文本
        string GetDescription();

        // 转换为配置字典
        IDictionary<string, object> ToConfig();
    }
}
