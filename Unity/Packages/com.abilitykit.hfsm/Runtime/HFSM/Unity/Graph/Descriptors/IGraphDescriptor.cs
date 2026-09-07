// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 图描述器接口 - 顶层接口，描述整个 HFSM 图结构
    /// </summary>
    public interface IGraphDescriptor
    {
        /// <summary>图名称</summary>
        string Name { get; }

        /// <summary>根状态机 ID</summary>
        string RootStateMachineId { get; }

        /// <summary>获取所有节点</summary>
        IReadOnlyList<INodeDescriptor> GetNodes();

        /// <summary>获取所有边</summary>
        IReadOnlyList<IEdgeDescriptor> GetEdges();

        /// <summary>获取所有参数</summary>
        IReadOnlyList<IParameterDescriptor> GetParameters();

        /// <summary>获取根状态机节点</summary>
        IStateMachineNodeDescriptor GetRootStateMachine();

        /// <summary>根据 ID 获取节点</summary>
        INodeDescriptor GetNodeById(string id);

        /// <summary>根据 ID 获取边</summary>
        IEdgeDescriptor GetEdgeById(string id);

        /// <summary>根据 ID 获取节点，类型安全版本</summary>
        T GetNodeById<T>(string id) where T : INodeDescriptor;

        /// <summary>获取指定节点的所有出边</summary>
        IReadOnlyList<IEdgeDescriptor> GetOutgoingEdges(string nodeId);

        /// <summary>获取指定节点的所有入边</summary>
        IReadOnlyList<IEdgeDescriptor> GetIncomingEdges(string nodeId);

        /// <summary>根据名称获取参数</summary>
        IParameterDescriptor GetParameterByName(string name);

        /// <summary>验证图结构</summary>
        bool Validate();

        // ========== 编辑器元数据（可选） ==========

        /// <summary>
        /// 获取编辑器元数据（可能为 null）
        /// </summary>
        IGraphEditorDataDescriptor EditorData { get; }

        /// <summary>
        /// 获取节点的编辑器元数据
        /// </summary>
        INodeEditorDataDescriptor GetNodeEditorData(string nodeId);
    }
}
