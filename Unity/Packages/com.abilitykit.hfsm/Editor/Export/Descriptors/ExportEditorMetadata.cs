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
    /// 导出的编辑器元数据
    /// </summary>
    [Serializable]
    public class ExportEditorMetadata
    {
        public float zoom = 1.0f;
        public float panX;
        public float panY;
        public List<string> expandedStateMachineIds = new List<string>();
        public List<ExportNodeEditorData> nodeEditorData = new List<ExportNodeEditorData>();
    }
}
