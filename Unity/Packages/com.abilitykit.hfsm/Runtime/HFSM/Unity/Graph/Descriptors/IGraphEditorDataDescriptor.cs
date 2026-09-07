// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 图编辑器元数据描述器接口
    /// 提供图在编辑器中的状态
    /// </summary>
    public interface IGraphEditorDataDescriptor
    {
        float Zoom { get; set; }
        UnityEngine.Vector2 Pan { get; set; }
        IReadOnlyList<string> ExpandedStateMachineIds { get; }
        bool IsExpanded(string stateMachineId);
        INodeEditorDataDescriptor GetNodeEditorData(string nodeId);
        INodeEditorDataDescriptor GetOrCreateNodeEditorData(string nodeId);
    }
}
