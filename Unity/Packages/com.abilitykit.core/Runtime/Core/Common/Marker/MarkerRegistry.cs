using System;
using System.Collections.Generic;

namespace AbilityKit.Common.Marker
{
    /// <summary>
    /// 基于类型的标记注册表。
    /// 直接存储所有扫描到的实现类型，适合"扫描所有实现"的场景。
    /// </summary>
    /// <typeparam name="TAttr">自定义的 MarkerAttribute 子类</typeparam>
    public class MarkerRegistry<TAttr> : IMarkerRegistry where TAttr : MarkerAttribute
    {
        private readonly List<Type> _types = new List<Type>();

        public int Count => _types.Count;

        public IReadOnlyList<Type> Types => _types;

        public void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;

            _types.Add(implType);
        }

        /// <summary>
        /// 遍历所有已注册的类型。
        /// </summary>
        public void ForEach(Action<Type> action)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                action(_types[i]);
            }
        }

        /// <summary>
        /// 根据条件筛选类型。
        /// </summary>
        public IEnumerable<Type> Where(Func<Type, bool> predicate)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                if (predicate(_types[i]))
                    yield return _types[i];
            }
        }

        /// <summary>
        /// 查找第一个匹配的类型。
        /// </summary>
        public Type? Find(Func<Type, bool> predicate)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                if (predicate(_types[i]))
                    return _types[i];
            }
            return null;
        }
    }
}
