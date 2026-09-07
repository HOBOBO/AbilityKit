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
    /// 导出的转换条件基类
    /// </summary>
    [Serializable]
    public class ExportConditionData
    {
        public string typeName;
        public string displayName;

        // 参数比较条件字段
        public string parameterName;
        public string parameterType;
        public string compareOperator;
        public bool boolValue;
        public float floatValue;
        public int intValue;

        // 时间经过条件字段
        public string sourceNodeId;
        public float duration;
    }
}
