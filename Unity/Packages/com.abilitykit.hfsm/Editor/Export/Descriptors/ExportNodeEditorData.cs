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
    /// 导出的节点编辑器数据
    /// </summary>
    [Serializable]
    public class ExportNodeEditorData
    {
        public string nodeId;
        public float positionX;
        public float positionY;
        public float sizeWidth;
        public float sizeHeight;
        public bool isExpanded;
        public bool hasCustomColor;
        public float customColorR;
        public float customColorG;
        public float customColorB;
        public float customColorA;
    }
}
