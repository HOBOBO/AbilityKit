// ============================================================================
// BehaviorTypeRegistry - 行为类型注册表
// 支持包外扩展行为类型，无需修改枚举
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using IAction = AbilityKit.HFSM.Actions.IAction;
using ICompositeAction = AbilityKit.HFSM.Actions.ICompositeAction;
using IDecoratorAction = AbilityKit.HFSM.Actions.IDecoratorAction;


namespace AbilityKit.HFSM
{

    /// <summary>
    /// 行为参数定义
    /// </summary>
    [Serializable]
    public class BehaviorParameterDefinition
    {
        public string name;
        public BehaviorParameterType valueType;
        public string displayName;
        public string description;

        [NonSerialized]
        public object defaultValue;

        public BehaviorParameterDefinition() { }

        public BehaviorParameterDefinition(string name, BehaviorParameterType valueType, string displayName = null, string description = null, object defaultValue = null)
        {
            this.name = name;
            this.valueType = valueType;
            this.displayName = displayName ?? name;
            this.description = description ?? string.Empty;
            this.defaultValue = defaultValue;
        }

        public void ApplyDefaultValue(BehaviorParameter parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));
            if (defaultValue == null)
                return;

            switch (valueType)
            {
                case BehaviorParameterType.Float:
                    parameter.floatValue = Convert.ToSingle(defaultValue);
                    break;
                case BehaviorParameterType.Int:
                    parameter.intValue = Convert.ToInt32(defaultValue);
                    break;
                case BehaviorParameterType.Bool:
                    parameter.boolValue = Convert.ToBoolean(defaultValue);
                    break;
                case BehaviorParameterType.String:
                    parameter.stringValue = Convert.ToString(defaultValue);
                    break;
                case BehaviorParameterType.Object:
                    parameter.objectValue = (UnityEngine.Object)defaultValue;
                    break;
                case BehaviorParameterType.Vector2:
                    parameter.vector2Value = (UnityEngine.Vector2)defaultValue;
                    break;
                case BehaviorParameterType.Vector3:
                    parameter.vector3Value = (UnityEngine.Vector3)defaultValue;
                    break;
                case BehaviorParameterType.Color:
                    parameter.colorValue = (UnityEngine.Color)defaultValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
