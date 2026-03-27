// ============================================================================
// Export DTO - 导出数据传输对象
// 基于描述器接口的可序列化数据结构
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Editor.Export
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
