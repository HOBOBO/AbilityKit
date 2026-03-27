// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Descriptor
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

    /// <summary>
    /// 参数类型枚举
    /// </summary>
    public enum DescriptorParameterType
    {
        Bool,
        Float,
        Int,
        Trigger
    }

    /// <summary>
    /// 比较操作符枚举
    /// </summary>
    public enum DescriptorCompareOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual
    }

    /// <summary>
    /// 行为参数类型枚举
    /// </summary>
    public enum DescriptorBehaviorParameterType
    {
        Float,
        Int,
        Bool,
        String,
        Object,
        Vector2,
        Vector3,
        Color
    }

    /// <summary>
    /// 行为类型枚举
    /// </summary>
    public enum DescriptorBehaviorType
    {
        // ========== 原子行为 ==========
        Wait,
        WaitUntil,
        Log,
        SetFloat,
        SetBool,
        SetInt,
        PlayAnimation,
        SetActive,
        MoveTo,

        // ========== 复合行为 ==========
        Sequence,
        Selector,
        Parallel,
        RandomSelector,
        RandomSequence,

        // ========== 修饰器行为 ==========
        Repeat,
        Invert,
        TimeLimit,
        UntilSuccess,
        UntilFailure,
        Cooldown,
        If
    }

    // ========================================================================
    // 行为描述器
    // ========================================================================

    /// <summary>
    /// 行为参数描述器接口
    /// </summary>
    public interface IBehaviorParameterDescriptor
    {
        string Name { get; }
        DescriptorBehaviorParameterType ValueType { get; }

        // 值访问
        float GetFloatValue();
        int GetIntValue();
        bool GetBoolValue();
        string GetStringValue();
        object GetObjectValue();
        UnityEngine.Vector2 GetVector2Value();
        UnityEngine.Vector3 GetVector3Value();
        UnityEngine.Color GetColorValue();
    }

    /// <summary>
    /// 行为描述器接口 - 描述一个可序列化的行为配置
    /// </summary>
    public interface IBehaviorDescriptor
    {
        string Id { get; }
        string Name { get; }
        DescriptorBehaviorType BehaviorType { get; }
        string ParentId { get; }
        IReadOnlyList<string> ChildIds { get; }
        bool IsExpanded { get; }

        // 参数
        IReadOnlyList<IBehaviorParameterDescriptor> GetParameters();
        bool HasParameter(string name);
        IBehaviorParameterDescriptor GetParameter(string name);
    }

    // ========================================================================
    // 条件描述器
    // ========================================================================

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

    /// <summary>
    /// 参数比较条件描述器
    /// </summary>
    public interface IParameterConditionDescriptor : IConditionDescriptor
    {
        string ParameterName { get; }
        DescriptorParameterType ParameterType { get; }
        DescriptorCompareOperator Operator { get; }

        bool GetBoolValue();
        float GetFloatValue();
        int GetIntValue();
    }

    /// <summary>
    /// 时间经过条件描述器
    /// </summary>
    public interface ITimeElapsedConditionDescriptor : IConditionDescriptor
    {
        string SourceNodeId { get; }
        float Duration { get; }
        DescriptorCompareOperator Operator { get; }
    }

    /// <summary>
    /// 行为完成条件描述器
    /// </summary>
    public interface IBehaviorCompleteConditionDescriptor : IConditionDescriptor
    {
        string SourceNodeId { get; }
    }

    // ========================================================================
    // 节点描述器
    // ========================================================================

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

    // ========================================================================
    // 边描述器
    // ========================================================================

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

    // ========================================================================
    // 参数描述器
    // ========================================================================

    /// <summary>
    /// 参数描述器接口
    /// </summary>
    public interface IParameterDescriptor
    {
        string Name { get; }
        DescriptorParameterType ParameterType { get; }

        /// <summary>获取序列化的默认值</summary>
        object GetSerializedDefaultValue();
    }

    // ========================================================================
    // 图描述器
    // ========================================================================

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
