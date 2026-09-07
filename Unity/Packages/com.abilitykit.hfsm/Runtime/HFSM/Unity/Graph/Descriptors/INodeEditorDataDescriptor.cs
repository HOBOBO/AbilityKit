// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 节点编辑器元数据描述器接口
    /// 提供节点在编辑器中的可视化信息
    /// </summary>
    public interface INodeEditorDataDescriptor
    {
        string NodeId { get; }
        UnityEngine.Vector2 Position { get; set; }
        UnityEngine.Vector2 Size { get; set; }
        bool IsExpanded { get; set; }
        UnityEngine.Color? CustomColor { get; set; }
    }
}
