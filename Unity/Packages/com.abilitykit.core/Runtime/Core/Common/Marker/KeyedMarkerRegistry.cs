using System;
using System.Collections.Generic;

namespace AbilityKit.Common.Marker
{
    /// <summary>
    /// 基于 Key-Type 的标记注册表。
    /// 适合 "string actionType → Type" 这类需要通过 Key 查找实现的场景。
    /// MarkerAttribute 子类负责从自己的数据中提取 Key。
    /// </summary>
    /// <typeparam name="TKey">注册键的类型，通常是 string 或 int</typeparam>
    /// <typeparam name="TAttr">自定义的 MarkerAttribute 子类</typeparam>
    public class KeyedMarkerRegistry<TKey, TAttr> : IMarkerRegistry where TAttr : MarkerAttribute
    {
        private readonly Dictionary<TKey, Type> _map = new Dictionary<TKey, Type>();
        private readonly List<Type> _types = new List<Type>();

        public int Count => _map.Count;

        /// <summary>
        /// 通过 Key 查找已注册的类型。
        /// </summary>
        public bool TryGet(TKey key, out Type type) => _map.TryGetValue(key, out type);

        /// <summary>
        /// 通过 Key 获取类型，未找到时抛出异常。
        /// </summary>
        public Type Get(TKey key)
        {
            if (_map.TryGetValue(key, out var type))
                return type;
            throw new KeyNotFoundException($"Type not registered with key: {key}");
        }

        /// <summary>
        /// 获取所有已注册的 Key。
        /// </summary>
        public IEnumerable<TKey> Keys => _map.Keys;

        /// <summary>
        /// 获取所有已注册的类型。
        /// </summary>
        public IReadOnlyList<Type> Types => _types;

        public void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;

            _types.Add(implType);
        }

        /// <summary>
        /// 带 Key 注册，框架层在 Attribute.OnScanned 中使用。
        /// </summary>
        public void Register(TKey key, Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;

            _map[key] = implType;
            if (!_types.Contains(implType))
                _types.Add(implType);
        }

        /// <summary>
        /// 检查 Key 是否已注册。
        /// </summary>
        public bool Contains(TKey key) => _map.ContainsKey(key);

        /// <summary>
        /// 遍历所有 Key-Type 对。
        /// </summary>
        public void ForEach(Action<TKey, Type> action)
        {
            foreach (var kv in _map)
            {
                action(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// 获取某个 Key 的实现实例（需要默认构造函数）。
        /// </summary>
        public object? GetOrCreateInstance(TKey key)
        {
            if (!TryGet(key, out var type))
                return null;
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 获取某个 Key 的实现实例（泛型版本）。
        /// </summary>
        public T? GetOrCreateInstance<T>(TKey key) where T : class
        {
            if (!TryGet(key, out var type))
                return null;
            if (!typeof(T).IsAssignableFrom(type))
                return null;
            return Activator.CreateInstance(type) as T;
        }
    }
}
