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
    /// 导出的行为参数
    /// </summary>
    [Serializable]
    public class ExportBehaviorParameterData
    {
        public string name;
        public string valueType;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public string objectReference;
        public float vector2X;
        public float vector2Y;
        public float vector3X;
        public float vector3Y;
        public float vector3Z;
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;
    }
}
