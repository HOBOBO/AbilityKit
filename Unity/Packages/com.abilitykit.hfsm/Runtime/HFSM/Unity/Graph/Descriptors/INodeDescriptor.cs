// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 节点描述器接口 - 所有节点描述器的基接口
    /// </summary>
    public interface INodeDescriptor
    {
        /// <summary>唯一标识</summary>
        string Id { get; }

        /// <summary>显示名称</summary>
        string Name { get; }

        /// <summary>节点类型</summary>
        DescriptorNodeType NodeType { get; }

        /// <summary>所属父状态机 ID</summary>
        string ParentStateMachineId { get; }

        /// <summary>是否为默认起始状态</summary>
        bool IsDefault { get; }

        /// <summary>
        /// 获取节点类型描述
        /// </summary>
        string GetNodeTypeDescription();
    }
}
