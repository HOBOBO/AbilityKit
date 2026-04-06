using System;
using System.Collections.Generic;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 默认的组件类型注册表实现。
    /// 线程安全，支持延迟初始化。
    /// 全局共享单例，确保所有 EntityWorld 实例使用相同的组件类型 ID。
    /// </summary>
    public sealed class ComponentRegistry : IComponentRegistry
    {
        private static ComponentRegistry _shared;

        /// <summary>
        /// 全局共享的组件注册表实例。
        /// 确保所有 EntityWorld 实例使用相同的组件类型 ID。
        /// </summary>
        public static ComponentRegistry Shared
        {
            get
            {
                if (_shared == null)
                {
                    _shared = new ComponentRegistry();
                }
                return _shared;
            }
        }

        private readonly object _lock = new object();
        private readonly Dictionary<Type, int> _ids = new Dictionary<Type, int>();
        private readonly Dictionary<int, Type> _types = new Dictionary<int, Type>();
        private int _nextId = 1;

        /// <inheritdoc/>
        public int GetId<T>()
        {
            return GetId(typeof(T));
        }

        /// <inheritdoc/>
        public int GetId(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            lock (_lock)
            {
                if (_ids.TryGetValue(type, out var id)) return id;
                id = _nextId++;
                _ids.Add(type, id);
                _types[id] = type;
                return id;
            }
        }

        /// <inheritdoc/>
        public bool TryGetType(int typeId, out Type type)
        {
            lock (_lock)
            {
                return _types.TryGetValue(typeId, out type);
            }
        }

        /// <inheritdoc/>
        public Type GetType(int typeId)
        {
            if (TryGetType(typeId, out var type)) return type;
            throw new KeyNotFoundException($"Unknown component typeId: {typeId}");
        }

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _nextId;
                }
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<Type> GetAllTypes()
        {
            lock (_lock)
            {
                var result = new List<Type>(_ids.Keys);
                return result;
            }
        }
    }
}
