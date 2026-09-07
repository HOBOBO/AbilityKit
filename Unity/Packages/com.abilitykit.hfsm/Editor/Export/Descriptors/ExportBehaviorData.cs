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
    /// 导出的行为项
    /// </summary>
    [Serializable]
    public class ExportBehaviorData
    {
        public string id;
        public string name;
        public string type;
        public string parentId;
        public List<string> childIds = new List<string>();
        public List<ExportBehaviorParameterData> parameters = new List<ExportBehaviorParameterData>();
        public bool isExpanded;
    }
}
