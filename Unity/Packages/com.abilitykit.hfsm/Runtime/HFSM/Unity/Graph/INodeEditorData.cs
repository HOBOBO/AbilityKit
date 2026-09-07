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
    /// 节点编辑器数据接口 - 存储节点在编辑器中的可视化信息
    /// 运行时不需要此数据，可以完全排除
    /// </summary>
    public interface INodeEditorData
    {
        /// <summary>
        /// 关联的节点 ID
        /// </summary>
        string NodeId { get; }

        /// <summary>
        /// 节点在图视图中的位置
        /// </summary>
        Vector2 Position { get; set; }

        /// <summary>
        /// 节点在图视图中的大小
        /// </summary>
        Vector2 Size { get; set; }

        /// <summary>
        /// 是否在编辑器中展开（用于复合节点）
        /// </summary>
        bool IsExpanded { get; set; }

        /// <summary>
        /// 节点颜色（自定义染色）
        /// </summary>
        Color? CustomColor { get; set; }
    }
}
