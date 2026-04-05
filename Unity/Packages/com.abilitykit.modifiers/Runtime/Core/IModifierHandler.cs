using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 修改器处理器接口。
    /// 定义如何将修改器应用到基础值上。
    ///
    /// 泛型设计：支持任意类型的值（float、int、struct 等）
    /// 策略支持：通过 IStrategy 扩展自定义修改逻辑
    /// </summary>
    public interface IModifierHandler<TValue>
    {
        /// <summary>
        /// 应用单个修改器到基础值
        /// </summary>
        /// <param name="baseValue">基础值</param>
        /// <param name="modifier">修改器数据</param>
        /// <param name="context">修改器上下文（用于获取属性等）</param>
        /// <returns>应用后的值</returns>
        TValue Apply(TValue baseValue, in ModifierData modifier, IModifierContext context);

        /// <summary>
        /// 比较两个值，用于 Override 优先级判断
        /// 返回值：&lt;0 表示 a 更优先，&gt;0 表示 b 更优先，=0 表示相同
        /// </summary>
        int Compare(TValue a, TValue b);

        /// <summary>
        /// 合并多个同类型修改器的值（如叠加层数）
        /// </summary>
        TValue Combine(in Span<TValue> values);
    }

    /// <summary>
    /// 修改器上下文接口。
    /// 提供修改器计算过程中需要的外部数据（属性值、等级等）
    /// </summary>
    public interface IModifierContext
    {
        /// <summary>
        /// 获取属性值
        /// </summary>
        float GetAttribute(ModifierKey key);

        /// <summary>
        /// 获取等级
        /// </summary>
        float Level { get; }
    }

    /// <summary>
    /// 空上下文（无外部依赖时使用）
    /// </summary>
    public struct EmptyModifierContext : IModifierContext
    {
        public static EmptyModifierContext Default => default;

        public float GetAttribute(ModifierKey key) => 0f;
        public float Level => 1f;
    }

    /// <summary>
    /// 策略感知的修改器上下文。
    /// 在计算时携带策略注册表。
    /// </summary>
    public readonly struct StrategyAwareModifierContext : IModifierContext
    {
        public float GetAttribute(ModifierKey key) => _getAttribute?.Invoke(key) ?? 0f;
        public float Level => _level;
        public IStrategyRegistry StrategyRegistry => _registry;

        private readonly IStrategyRegistry _registry;
        private readonly float _level;
        private readonly Func<ModifierKey, float> _getAttribute;

        public StrategyAwareModifierContext(
            IStrategyRegistry registry,
            float level = 1f,
            Func<ModifierKey, float> getAttribute = null)
        {
            _registry = registry;
            _level = level;
            _getAttribute = getAttribute;
        }
    }
}
