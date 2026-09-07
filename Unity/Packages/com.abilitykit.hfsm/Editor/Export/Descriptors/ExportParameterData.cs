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
    /// 导出的参数
    /// </summary>
    [Serializable]
    public class ExportParameterData
    {
        public string name;
        public string type;
        public object defaultValue;
    }
}
