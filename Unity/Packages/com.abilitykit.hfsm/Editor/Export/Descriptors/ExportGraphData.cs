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
    /// 导出数据根对象
    /// </summary>
    [Serializable]
    public class ExportGraphData
    {
        public string version = "1.0";
        public string graphName;
        public string exportedAt;
        public string rootStateMachineId;
        public List<ExportParameterData> parameters = new List<ExportParameterData>();
        public List<ExportNodeData> nodes = new List<ExportNodeData>();
        public List<ExportEdgeData> edges = new List<ExportEdgeData>();

        // 编辑器元数据
        public ExportEditorMetadata editorMetadata;
    }
}
