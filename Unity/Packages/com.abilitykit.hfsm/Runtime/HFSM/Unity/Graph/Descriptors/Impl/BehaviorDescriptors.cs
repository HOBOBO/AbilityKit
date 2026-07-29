// ============================================================================
// Behavior Descriptor Implementations - 行为描述器实现
// 将现有的 HfsmBehaviorItem 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityHFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 行为参数描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorParameterDescriptor : IBehaviorParameterDescriptor
    {
        private readonly HfsmBehaviorParameter _param;

        public BehaviorParameterDescriptor(HfsmBehaviorParameter param)
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

        private static DescriptorBehaviorParameterType ConvertType(HfsmBehaviorParameterType type)
        {
            return type switch
            {
                HfsmBehaviorParameterType.Float => DescriptorBehaviorParameterType.Float,
                HfsmBehaviorParameterType.Int => DescriptorBehaviorParameterType.Int,
                HfsmBehaviorParameterType.Bool => DescriptorBehaviorParameterType.Bool,
                HfsmBehaviorParameterType.String => DescriptorBehaviorParameterType.String,
                HfsmBehaviorParameterType.Object => DescriptorBehaviorParameterType.Object,
                HfsmBehaviorParameterType.Vector2 => DescriptorBehaviorParameterType.Vector2,
                HfsmBehaviorParameterType.Vector3 => DescriptorBehaviorParameterType.Vector3,
                HfsmBehaviorParameterType.Color => DescriptorBehaviorParameterType.Color,
                _ => DescriptorBehaviorParameterType.Float
            };
        }
    }

    /// <summary>
    /// 行为描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorDescriptor : IBehaviorDescriptor
    {
        private readonly HfsmBehaviorItem _item;
        private List<IBehaviorParameterDescriptor> _paramDescriptors;

        public BehaviorDescriptor(HfsmBehaviorItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            InitializeParameterDescriptors();
        }

        public string Id => _item.id;
        public string Name => _item.displayName;
        public string TypeName => _item.TypeName;
        public string ParentId => _item.parentId;
        public IReadOnlyList<string> ChildIds => _item.childIds;
        public bool IsExpanded => _item.isExpanded;

        public IReadOnlyList<IBehaviorParameterDescriptor> GetParameters() => _paramDescriptors;

        public bool HasParameter(string name)
        {
            return _item.parameters?.Any(p => p.name == name) ?? false;
        }

        public IBehaviorParameterDescriptor GetParameter(string name)
        {
            var param = _item.GetParameter(name);
            return param != null ? new BehaviorParameterDescriptor(param) : null;
        }

        private void InitializeParameterDescriptors()
        {
            _paramDescriptors = new List<IBehaviorParameterDescriptor>();
            if (_item.parameters != null)
            {
                foreach (var p in _item.parameters)
                {
                    _paramDescriptors.Add(new BehaviorParameterDescriptor(p));
                }
            }
        }

    }

    /// <summary>
    /// 行为描述器工厂
    /// </summary>
    public static class BehaviorDescriptorFactory
    {
        public static IBehaviorDescriptor Create(HfsmBehaviorItem item)
        {
            return new BehaviorDescriptor(item);
        }

        public static List<IBehaviorDescriptor> CreateRange(IEnumerable<HfsmBehaviorItem> items)
        {
            return items?.Select(Create).ToList() ?? new List<IBehaviorDescriptor>();
        }
    }
}
