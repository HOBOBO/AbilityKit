using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 使用默认键比较器维护键到类型的映射，并保留曾登记的类型目录。
    /// 不验证特性和构造条件；实例不支持并发访问。
    /// </summary>
    /// <typeparam name="TKey">非空注册键的类型。</typeparam>
    /// <typeparam name="TAttr">关联的标记类型，仅用于区分注册表类型。</typeparam>
    public class KeyedMarkerRegistry<TKey, TAttr> : IMarkerRegistry
        where TKey : notnull
        where TAttr : MarkerAttribute
    {
        private readonly Dictionary<TKey, Type> _map = new Dictionary<TKey, Type>();
        private readonly List<Type> _types = new List<Type>();

        /// <summary>
        /// 获取当前键数量，与历史类型目录的长度可能不同。
        /// </summary>
        public int Count => _map.Count;

        /// <summary>
        /// 按默认键比较器查询映射，未找到时输出空类型。
        /// 非法键的异常沿用底层字典行为。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <param name="type">找到的类型，失败时为空。</param>
        /// <returns>是否存在对应映射。</returns>
        public bool TryGet(TKey key, [MaybeNullWhen(false)] out Type type) => _map.TryGetValue(key, out type);

        /// <summary>
        /// 获取键对应的类型，缺失时抛出 KeyNotFoundException。
        /// </summary>
        /// <param name="key">查询键；非法键的异常沿用底层字典行为。</param>
        /// <returns>当前映射类型。</returns>
        public Type Get(TKey key)
        {
            if (_map.TryGetValue(key, out var type))
                return type;
            throw new KeyNotFoundException($"Type not registered with key: {key}");
        }

        /// <summary>
        /// 获取当前字典键的实时视图，不承诺跨运行时的枚举顺序。
        /// 枚举期间修改映射的限制沿用底层字典行为。
        /// </summary>
        public IEnumerable<TKey> Keys => _map.Keys;

        /// <summary>
        /// 按首次登记顺序返回历史类型目录的实时列表接口，不复制也不包装。
        /// 键被覆盖后旧类型仍保留；调用方应只读使用，不具备快照或不可变保证。
        /// </summary>
        public IReadOnlyList<Type> Types => _types;

        /// <summary>
        /// 仅向类型目录追加，忽略空类型、抽象类型和接口，并按 Type 去重。
        /// 不新增键或改变 Count，不检查特性和实例化条件。
        /// </summary>
        /// <param name="implType">待登记类型。</param>
        public void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;

            if (!_types.Contains(implType))
                _types.Add(implType);
        }

        /// <summary>
        /// 先过滤空类型、抽象类型和接口，再覆盖同键映射并向历史目录去重追加。
        /// 旧类型不从目录移除；无效类型直接忽略，不访问或验证键。
        /// </summary>
        /// <param name="key">注册键，使用字典默认比较器。</param>
        /// <param name="implType">待登记类型，不检查特性和实例化条件。</param>
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
        /// 检查键是否已有映射，非法键的异常沿用底层字典行为。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <returns>是否存在对应映射。</returns>
        public bool Contains(TKey key) => _map.ContainsKey(key);

        /// <summary>
        /// 直接枚举实时字典并调用回调，不承诺跨运行时顺序或修改期间的稳定性。
        /// 不预先验证回调；回调异常立即传播，已执行的副作用不回滚。
        /// </summary>
        /// <param name="action">处理键和类型的同步回调。</param>
        public void ForEach(Action<TKey, Type> action)
        {
            foreach (var kv in _map)
            {
                action(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// 键存在时每次通过 Activator.CreateInstance 创建新对象，不缓存实例。
        /// 构造条件不满足或构造失败时保留反射异常。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <returns>新建对象；键不存在时为空。</returns>
        public object? GetOrCreateInstance(TKey key)
        {
            if (!TryGet(key, out var type))
                return null;
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 键存在且类型可赋给 T 时，每次反射创建新对象，不缓存实例。
        /// 不匹配时不执行构造；构造失败时保留反射异常。
        /// </summary>
        /// <typeparam name="T">期望的引用类型。</typeparam>
        /// <param name="key">查询键。</param>
        /// <returns>新建实例；键缺失或类型不兼容时为空。</returns>
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
