using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 行为类型描述符
    /// </summary>
    public sealed class ExecutableDescriptor
    {
        public int TypeId;
        public string TypeName;
        public ExecutableMetadata Metadata;
        public bool IsScheduled => Metadata.IsScheduled;
        public bool IsPeriodic => Metadata.IsScheduled && Metadata.DefaultPeriodMs.HasValue;
        public Func<IExecutable> Factory;
    }

    /// <summary>
    /// 条件类型描述符
    /// </summary>
    public sealed class ConditionDescriptor
    {
        public int TypeId;
        public string TypeName;
        public Func<ICondition> Factory;
    }

    /// <summary>
    /// 行为类型注册表
    /// 基于 Attribute 自动发现和注册类型，包外扩展只需在类型上标记 Attribute
    /// 继承 MarkerRegistry 模式，支持 MarkerScanner 自动扫描
    /// </summary>
    public sealed class ExecutableRegistry : IMarkerRegistry
    {
        private static readonly Lazy<ExecutableRegistry> _instance = new(() => new ExecutableRegistry());
        public static ExecutableRegistry Instance => _instance.Value;

        private readonly Dictionary<int, ExecutableDescriptor> _executables = new();
        private readonly Dictionary<string, int> _nameToId = new();
        private readonly Dictionary<int, ConditionDescriptor> _conditions = new();
        private readonly Dictionary<string, int> _conditionNameToId = new();

        private ExecutableRegistry()
        {
            RegisterBuiltin();
            var assembly = typeof(SequenceExecutable).Assembly;
            MarkerScanner<ExecutableTypeIdAttribute>.Scan(new[] { assembly }, this);
            MarkerScanner<ConditionTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        /// <summary>
        /// 注册行为类型（通过显式 TypeId）
        /// </summary>
        public void Register<TExecutable>(int typeId, string typeName, ExecutableMetadata metadata = default)
            where TExecutable : IExecutable, new()
        {
            _executables[typeId] = new ExecutableDescriptor
            {
                TypeId = typeId,
                TypeName = typeName,
                Metadata = metadata,
                Factory = () => new TExecutable()
            };
            _nameToId[typeName] = typeId;
        }

        public IExecutable CreateExecutable(int typeId)
        {
            if (_executables.TryGetValue(typeId, out var descriptor))
                return descriptor.Factory();
            throw new KeyNotFoundException($"Executable type {typeId} not found");
        }

        public TExecutable CreateExecutable<TExecutable>(int typeId) where TExecutable : IExecutable
            => (TExecutable)CreateExecutable(typeId);

        public bool TryGetDescriptor(int typeId, out ExecutableDescriptor descriptor)
            => _executables.TryGetValue(typeId, out descriptor);

        public ExecutableDescriptor GetDescriptor(int typeId)
        {
            if (_executables.TryGetValue(typeId, out var descriptor))
                return descriptor;
            throw new KeyNotFoundException($"Executable type {typeId} not found");
        }

        public bool TryGetTypeIdByName(string typeName, out int typeId)
            => _nameToId.TryGetValue(typeName, out typeId);

        public void RegisterCondition<TCondition>(int typeId, string typeName)
            where TCondition : ICondition, new()
        {
            _conditions[typeId] = new ConditionDescriptor
            {
                TypeId = typeId,
                TypeName = typeName,
                Factory = () => new TCondition()
            };
            _conditionNameToId[typeName] = typeId;
        }

        public ICondition CreateCondition(int typeId)
        {
            if (_conditions.TryGetValue(typeId, out var descriptor))
                return descriptor.Factory();
            throw new KeyNotFoundException($"Condition type {typeId} not found");
        }

        public IEnumerable<ExecutableDescriptor> GetAllExecutables()
            => _executables.Values;

        /// <summary>
        /// 通过 Attribute 注册 Executable 类型（供 MarkerAttribute.OnScanned 调用）
        /// </summary>
        internal void RegisterByAttribute(ExecutableTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            var factory = CreateExecutableFactory(implType);
            if (factory != null)
            {
                _executables[attr.TypeId] = new ExecutableDescriptor
                {
                    TypeId = attr.TypeId,
                    TypeName = attr.TypeName,
                    Metadata = new ExecutableMetadata(attr.TypeId, attr.TypeName, isComposite: attr.IsComposite),
                    Factory = factory
                };
                _nameToId[attr.TypeName] = attr.TypeId;
            }
        }

        /// <summary>
        /// 通过 Attribute 注册 Condition 类型（供 MarkerAttribute.OnScanned 调用）
        /// </summary>
        internal void RegisterConditionByAttribute(ConditionTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            var factory = CreateConditionFactory(implType);
            if (factory != null)
            {
                _conditions[attr.TypeId] = new ConditionDescriptor
                {
                    TypeId = attr.TypeId,
                    TypeName = attr.TypeName,
                    Factory = factory
                };
                _conditionNameToId[attr.TypeName] = attr.TypeId;
            }
        }

        private Func<IExecutable> CreateExecutableFactory(Type type)
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                return () => (IExecutable)ctor.Invoke(null);
            }
            return null;
        }

        private Func<ICondition> CreateConditionFactory(Type type)
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                return () => (ICondition)ctor.Invoke(null);
            }
            return null;
        }

        private void RegisterBuiltin()
        {
            // 仅用于兼容旧代码，不推荐新代码使用
            // 新代码应使用 Attribute 标记类型
        }

        #region IMarkerRegistry 实现

        private readonly List<Type> _types = new();
        public int Count => _types.Count;
        public IReadOnlyList<Type> Types => _types;

        public void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;
            _types.Add(implType);
        }

        public void ForEach(Action<Type> action)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                action(_types[i]);
            }
        }

        public IEnumerable<Type> Where(Func<Type, bool> predicate)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                if (predicate(_types[i]))
                    yield return _types[i];
            }
        }

        public Type? Find(Func<Type, bool> predicate)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                if (predicate(_types[i]))
                    return _types[i];
            }
            return null;
        }

        #endregion
    }
}
