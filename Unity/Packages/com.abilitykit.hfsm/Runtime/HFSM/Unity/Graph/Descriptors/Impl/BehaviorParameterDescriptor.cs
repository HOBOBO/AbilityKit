// ============================================================================
// Behavior Descriptor Implementations - 行为描述器实现
// 将现有的 BehaviorItem 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 行为参数描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorParameterDescriptor : IBehaviorParameterDescriptor
    {
        private readonly BehaviorParameter _param;

        public BehaviorParameterDescriptor(BehaviorParameter param)
        {
            _param = param ?? throw new ArgumentNullException(nameof(param));
        }

        public string Name => _param.name;
        public DescriptorBehaviorParameterType ValueType => ConvertType(_param.ValueType);

        public float GetFloatValue() => _param.floatValue;
        public int GetIntValue() => _param.intValue;
        public bool GetBoolValue() => _param.boolValue;
        public string GetStringValue() => _param.stringValue;
        public object GetObjectValue() => _param.objectValue;
        public Vector2 GetVector2Value() => _param.vector2Value;
        public Vector3 GetVector3Value() => _param.vector3Value;
        public Color GetColorValue() => _param.colorValue;

        private static DescriptorBehaviorParameterType ConvertType(BehaviorParameterType type)
        {
            return type switch
            {
                BehaviorParameterType.Float => DescriptorBehaviorParameterType.Float,
                BehaviorParameterType.Int => DescriptorBehaviorParameterType.Int,
                BehaviorParameterType.Bool => DescriptorBehaviorParameterType.Bool,
                BehaviorParameterType.String => DescriptorBehaviorParameterType.String,
                BehaviorParameterType.Object => DescriptorBehaviorParameterType.Object,
                BehaviorParameterType.Vector2 => DescriptorBehaviorParameterType.Vector2,
                BehaviorParameterType.Vector3 => DescriptorBehaviorParameterType.Vector3,
                BehaviorParameterType.Color => DescriptorBehaviorParameterType.Color,
                _ => DescriptorBehaviorParameterType.Float
            };
        }
    }
}
