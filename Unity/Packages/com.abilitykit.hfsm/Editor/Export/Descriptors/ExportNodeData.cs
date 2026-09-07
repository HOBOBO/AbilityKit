// ============================================================================
// Export DTO - 导出数据传输对象
// 基于描述器接口的可序列化数据结构
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;


namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>
    /// 导出的节点基类
    /// </summary>
    [Serializable]
    public class ExportNodeData
    {
        public string name;
        public string id;
        public string nodeType;
        public string parentStateMachineId;
        public bool isDefault;

        // 编辑器元数据
        public float positionX;
        public float positionY;
        public float sizeWidth;
        public float sizeHeight;

        // 状态节点属性
        public bool needsExitTime;
        public bool isGhostState;
        public bool hasBehaviors;
        public List<ExportBehaviorData> behaviors = new List<ExportBehaviorData>();

        // 状态机节点属性
        public string defaultStateId;
        public bool rememberLastState;
        public List<string> childNodeIds = new List<string>();
        public List<string> transitionIds = new List<string>();
        public List<string> anyStateTransitionIds = new List<string>();
    }
}
