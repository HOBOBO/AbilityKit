using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// 导出数据根对象
    /// </summary>
    [Serializable]
    public class ExportedGraph
    {
        public string version = "1.0";
        public string graphName;
        public string exportedAt;
        public string rootStateMachineId;
        public List<ExportedParameter> parameters = new List<ExportedParameter>();
        public List<ExportedNode> nodes = new List<ExportedNode>();
        public List<ExportedEdge> edges = new List<ExportedEdge>();
    }

    /// <summary>
    /// 导出的参数
    /// </summary>
    [Serializable]
    public class ExportedParameter
    {
        public string name;
        public string type;
        public object defaultValue;
    }

    /// <summary>
    /// 导出的节点基类
    /// </summary>
    [Serializable]
    public class ExportedNode
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
        public List<ExportedBehaviorItem> behaviors = new List<ExportedBehaviorItem>();

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
    public class ExportedBehaviorItem
    {
        public string id;
        public string name;
        public string type;
        public string parentId;
        public List<string> childIds = new List<string>();
        public List<ExportedBehaviorParameter> parameters = new List<ExportedBehaviorParameter>();
        public bool isExpanded;
    }

    /// <summary>
    /// 导出的行为参数
    /// </summary>
    [Serializable]
    public class ExportedBehaviorParameter
    {
        public string name;
        public string valueType;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public string objectReference; // Unity Object reference stored as string (GUID or name)
        public float vector2X;
        public float vector2Y;
        public float vector3X;
        public float vector3Y;
        public float vector3Z;
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;

        public static ExportedBehaviorParameter FromBehaviorParameter(HfsmBehaviorParameter param)
        {
            var exported = new ExportedBehaviorParameter
            {
                name = param.name,
                valueType = param.ValueType.ToString()
            };

            switch (param.ValueType)
            {
                case HfsmBehaviorParameterType.Float:
                    exported.floatValue = param.floatValue;
                    break;
                case HfsmBehaviorParameterType.Int:
                    exported.intValue = param.intValue;
                    break;
                case HfsmBehaviorParameterType.Bool:
                    exported.boolValue = param.boolValue;
                    break;
                case HfsmBehaviorParameterType.String:
                    exported.stringValue = param.stringValue;
                    break;
                case HfsmBehaviorParameterType.Object:
                    exported.objectReference = param.objectValue != null ? param.objectValue.name : null;
                    break;
                    break;
                case HfsmBehaviorParameterType.Vector2:
                    exported.vector2X = param.vector2Value.x;
                    exported.vector2Y = param.vector2Value.y;
                    break;
                case HfsmBehaviorParameterType.Vector3:
                    exported.vector3X = param.vector3Value.x;
                    exported.vector3Y = param.vector3Value.y;
                    exported.vector3Z = param.vector3Value.z;
                    break;
                case HfsmBehaviorParameterType.Color:
                    exported.colorR = param.colorValue.r;
                    exported.colorG = param.colorValue.g;
                    exported.colorB = param.colorValue.b;
                    exported.colorA = param.colorValue.a;
                    break;
            }

            return exported;
        }
    }

    /// <summary>
    /// 导出的边（转换）
    /// </summary>
    [Serializable]
    public class ExportedEdge
    {
        public string id;
        public string sourceNodeId;
        public string targetNodeId;
        public int priority;
        public bool isExitTransition;
        public bool forceInstantly;
        public bool useAndLogic;
        public List<ExportedCondition> conditions = new List<ExportedCondition>();
    }

    /// <summary>
    /// 导出的转换条件基类
    /// </summary>
    [Serializable]
    public class ExportedCondition
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

        public static ExportedCondition FromCondition(HfsmTransitionCondition condition)
        {
            var exported = new ExportedCondition
            {
                typeName = condition.TypeName,
                displayName = condition.DisplayName
            };

            if (condition is HfsmParameterCondition paramCondition)
            {
                exported.parameterName = paramCondition.ParameterName;
                exported.parameterType = paramCondition.ParameterType.ToString();
                exported.compareOperator = paramCondition.Operator.ToString();
                exported.boolValue = paramCondition.BoolValue;
                exported.floatValue = paramCondition.FloatValue;
                exported.intValue = paramCondition.IntValue;
            }
            else if (condition is HfsmTimeElapsedCondition timeCondition)
            {
                exported.sourceNodeId = timeCondition.SourceNodeId;
                exported.duration = timeCondition.Duration;
                exported.compareOperator = timeCondition.Operator.ToString();
            }
            else if (condition is HfsmBehaviorCompleteCondition behaviorCondition)
            {
                exported.sourceNodeId = behaviorCondition.SourceNodeId;
            }

            return exported;
        }
    }
}
