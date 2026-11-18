using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 按首次登记顺序保留不同类型的兼容注册表。
    /// 不验证类型是否携带特性或能否实例化；实例不支持并发访问。
    /// </summary>
    /// <typeparam name="TAttr">关联的标记类型，仅用于区分注册表类型。</typeparam>
    public class MarkerRegistry<TAttr> : IMarkerRegistry where TAttr : MarkerAttribute
    {
        private readonly List<Type> _types = new List<Type>();

        /// <summary>
        /// 获取去重后的类型数量。
        /// </summary>
        public int Count => _types.Count;

        /// <summary>
        /// 获取底层类型列表的实时接口视图，不复制也不包装列表。
        /// 调用方应只读使用；该接口不提供快照或深度不可变保证。
        /// </summary>
        public IReadOnlyList<Type> Types => _types;

        /// <summary>
        /// 忽略空类型、抽象类型和接口，按 Type 默认相等比较去重。
        /// 其他类型按首次登记顺序追加，不检查特性、开放泛型或构造条件。
        /// </summary>
        /// <param name="implType">待登记类型。</param>
        public virtual void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;

            if (!_types.Contains(implType))
                _types.Add(implType);
        }

        /// <summary>
        /// 按实时列表顺序调用回调；回调追加的类型可进入本轮遍历。
        /// 不预先验证回调，回调异常立即传播且不回滚已登记类型。
        /// </summary>
        /// <param name="action">处理单个类型的同步回调。</param>
        public void ForEach(Action<Type> action)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                action(_types[i]);
            }
        }

        /// <summary>
        /// 返回延迟筛选序列，枚举时按实时列表及长度执行谓词。
        /// 追加类型可进入同轮枚举，谓词异常在枚举时传播；不预先验证谓词。
        /// </summary>
        /// <param name="predicate">类型筛选条件。</param>
        /// <returns>不具有快照隔离的延迟序列。</returns>
        public IEnumerable<Type> Where(Func<Type, bool> predicate)
        {
            for (int i = 0; i < _types.Count; i++)
            {
                if (predicate(_types[i]))
                    yield return _types[i];
            }
        }

        /// <summary>
        /// 按实时列表顺序返回首个满足谓词的类型，未匹配时返回空值。
        /// 谓词可追加后续类型；异常立即传播，不预先验证谓词。
        /// </summary>
        /// <param name="predicate">类型匹配条件。</param>
        /// <returns>首个匹配类型或空值。</returns>
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
