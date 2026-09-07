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
    /// 导出的边（转换）
    /// </summary>
    [Serializable]
    public class ExportEdgeData
    {
        public string id;
        public string sourceNodeId;
        public string targetNodeId;
        public int priority;
        public bool isExitTransition;
        public bool forceInstantly;
        public bool useAndLogic;
        public List<ExportConditionData> conditions = new List<ExportConditionData>();
    }
}
