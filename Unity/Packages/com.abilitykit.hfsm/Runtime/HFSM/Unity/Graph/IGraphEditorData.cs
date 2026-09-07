// ============================================================================
// Editor Data - 编辑器数据
// 定义编辑器专用数据的抽象，允许在运行时完全排除编辑器代码
// ============================================================================

// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;
using System.Collections.Generic;
#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif


namespace AbilityKit.HFSM.Graph
{

    /// <summary>
    /// 图编辑器数据接口 - 存储整个图在编辑器中的状态
    /// </summary>
    public interface IGraphEditorData
    {
        /// <summary>
        /// 图缩放级别
        /// </summary>
        float Zoom { get; set; }

        /// <summary>
        /// 图平移偏移
        /// </summary>
        Vector2 Pan { get; set; }

        /// <summary>
        /// 展开的状态机 ID 列表
        /// </summary>
        IReadOnlyList<string> ExpandedStateMachineIds { get; }

        /// <summary>
        /// 切换状态机的展开状态
        /// </summary>
        void ToggleExpanded(string stateMachineId);

        /// <summary>
        /// 检查状态机是否展开
        /// </summary>
        bool IsExpanded(string stateMachineId);

        /// <summary>
        /// 获取节点的编辑器数据
        /// </summary>
        INodeEditorData GetNodeEditorData(string nodeId);

        /// <summary>
        /// 创建或获取节点的编辑器数据
        /// </summary>
        INodeEditorData GetOrCreateNodeEditorData(string nodeId);

        /// <summary>
        /// 移除节点的编辑器数据
        /// </summary>
        void RemoveNodeEditorData(string nodeId);

        /// <summary>
        /// 获取所有节点编辑器数据
        /// </summary>
        IEnumerable<INodeEditorData> GetAllNodeEditorData();

        /// <summary>
        /// 清除所有编辑器数据
        /// </summary>
        void Clear();
    }
}
