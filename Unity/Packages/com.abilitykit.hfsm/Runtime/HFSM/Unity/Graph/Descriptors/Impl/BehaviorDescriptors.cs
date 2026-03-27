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
        public DescriptorBehaviorType BehaviorType => ConvertBehaviorType(_item.Type);
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

        private static DescriptorBehaviorType ConvertBehaviorType(HfsmBehaviorType type)
        {
            // 优先从注册表获取 DescriptorBehaviorType
            if (HfsmBehaviorTypeRegistry.IsInitialized)
            {
                var def = HfsmBehaviorTypeRegistry.GetDefinition(type.ToString());
                if (def != null)
                {
                    // 如果注册表中的类型可以转换为 DescriptorBehaviorType，进行转换
                    // 这里使用字符串匹配来兼容旧版本
                    if (Enum.TryParse<DescriptorBehaviorType>(def.typeName, out var result))
                    {
                        return result;
                    }
                }
            }

            // 回退到旧的 switch 逻辑
            return type switch
            {
                HfsmBehaviorType.Wait => DescriptorBehaviorType.Wait,
                HfsmBehaviorType.WaitUntil => DescriptorBehaviorType.WaitUntil,
                HfsmBehaviorType.Log => DescriptorBehaviorType.Log,
                HfsmBehaviorType.SetFloat => DescriptorBehaviorType.SetFloat,
                HfsmBehaviorType.SetBool => DescriptorBehaviorType.SetBool,
                HfsmBehaviorType.SetInt => DescriptorBehaviorType.SetInt,
                HfsmBehaviorType.PlayAnimation => DescriptorBehaviorType.PlayAnimation,
                HfsmBehaviorType.SetActive => DescriptorBehaviorType.SetActive,
                HfsmBehaviorType.MoveTo => DescriptorBehaviorType.MoveTo,
                HfsmBehaviorType.Sequence => DescriptorBehaviorType.Sequence,
                HfsmBehaviorType.Selector => DescriptorBehaviorType.Selector,
                HfsmBehaviorType.Parallel => DescriptorBehaviorType.Parallel,
                HfsmBehaviorType.RandomSelector => DescriptorBehaviorType.RandomSelector,
                HfsmBehaviorType.RandomSequence => DescriptorBehaviorType.RandomSequence,
                HfsmBehaviorType.Repeat => DescriptorBehaviorType.Repeat,
                HfsmBehaviorType.Invert => DescriptorBehaviorType.Invert,
                HfsmBehaviorType.TimeLimit => DescriptorBehaviorType.TimeLimit,
                HfsmBehaviorType.UntilSuccess => DescriptorBehaviorType.UntilSuccess,
                HfsmBehaviorType.UntilFailure => DescriptorBehaviorType.UntilFailure,
                HfsmBehaviorType.Cooldown => DescriptorBehaviorType.Cooldown,
                HfsmBehaviorType.If => DescriptorBehaviorType.If,
                _ => DescriptorBehaviorType.Wait
            };
        }

        /// <summary>
        /// 根据类型名称转换行为类型
        /// 支持包外扩展的类型名称
        /// </summary>
        private static DescriptorBehaviorType ConvertBehaviorTypeFromTypeName(string typeName)
        {
            // 优先从注册表获取定义
            if (HfsmBehaviorTypeRegistry.IsInitialized)
            {
                var def = HfsmBehaviorTypeRegistry.GetDefinition(typeName);
                if (def != null)
                {
                    // 尝试将类型名转换为 DescriptorBehaviorType
                    if (Enum.TryParse<DescriptorBehaviorType>(def.typeName, out var result))
                    {
                        return result;
                    }
                }
            }

            // 回退到尝试直接解析
            if (Enum.TryParse<DescriptorBehaviorType>(typeName, out var fallback))
            {
                return fallback;
            }

            return DescriptorBehaviorType.Wait;
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
