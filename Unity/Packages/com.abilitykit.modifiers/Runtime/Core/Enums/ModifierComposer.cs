using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修饰器组合类型
    // ============================================================================

    /// <summary>
    /// 修饰器组合类型
    /// </summary>
    public enum ModifierCompositionType : byte
    {
        /// <summary>
        /// 链式组合：修饰器依次执行，前一个的输出作为后一个的输入
        /// </summary>
        Chain = 0,

        /// <summary>
        /// 并行组合：多个修饰器的结果相加
        /// </summary>
        Parallel = 1,

        /// <summary>
        /// 条件组合：根据条件选择不同的修饰器
        /// </summary>
        Conditional = 2,
    }

    // ============================================================================
    // 修饰器组合器接口
    // ============================================================================

    /// <summary>
    /// 修饰器组合器接口。
    /// 定义如何组合多个修饰器。
    ///
    /// 设计原则：
    /// - 单一职责：只负责修饰器之间的组合逻辑
    /// - 可嵌套：支持任意深度的组合
    /// </summary>
    public interface IModifierComposer
    {
        /// <summary>组合类型</summary>
        ModifierCompositionType CompositionType { get; }

        /// <summary>
        /// 计算组合后的值
        /// </summary>
        float Compose(float baseValue, IModifierContext context);

        /// <summary>
        /// 获取最大基础值（用于缓存估计）
        /// </summary>
        float GetMaxBaseValue();
    }

    // ============================================================================
    // 链式组合器
    // ============================================================================

    /// <summary>
    /// 链式组合器。
    /// 修饰器依次执行，前一个的输出作为后一个的输入。
    ///
    /// 流程：baseValue -> modifier0 -> modifier1 -> ... -> result
    /// </summary>
    public struct ChainComposer : IModifierComposer
    {
        /// <summary>修饰器数组</summary>
        private readonly IMagnitudeModifier[] _modifiers;

        /// <summary>修饰器数量</summary>
        public byte Count { get; private set; }

        public ModifierCompositionType CompositionType => ModifierCompositionType.Chain;

        public ChainComposer(params IMagnitudeModifier[] modifiers)
        {
            _modifiers = modifiers;
            Count = (byte)modifiers.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compose(float baseValue, IModifierContext context)
        {
            if (_modifiers == null || Count == 0)
                return baseValue;

            float result = baseValue;
            for (int i = 0; i < Count; i++)
            {
                result = _modifiers[i].Modify(context, result);
            }
            return result;
        }

        public float GetMaxBaseValue()
        {
            if (_modifiers == null || Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < Count; i++)
            {
                sum += _modifiers[i].GetBaseValue();
            }
            return sum;
        }

        public IMagnitudeModifier this[int index] => _modifiers[index];
    }

    // ============================================================================
    // 并行组合器
    // ============================================================================

    /// <summary>
    /// 并行组合器。
    /// 多个修饰器的结果相加。
    ///
    /// 流程：baseValue + modifier0 + modifier1 + ... -> result
    ///
    /// 使用场景：
    /// - 同一属性有多个来源的加成
    /// - 多个 Buff 同时生效
    /// </summary>
    public struct ParallelComposer : IModifierComposer
    {
        /// <summary>修饰器数组</summary>
        private readonly IMagnitudeModifier[] _modifiers;

        /// <summary>修饰器数量</summary>
        public byte Count { get; private set; }

        public ModifierCompositionType CompositionType => ModifierCompositionType.Parallel;

        public ParallelComposer(params IMagnitudeModifier[] modifiers)
        {
            _modifiers = modifiers;
            Count = (byte)modifiers.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compose(float baseValue, IModifierContext context)
        {
            if (_modifiers == null || Count == 0)
                return baseValue;

            float result = baseValue;
            for (int i = 0; i < Count; i++)
            {
                result += _modifiers[i].Modify(context, 0f);
            }
            return result;
        }

        public float GetMaxBaseValue()
        {
            if (_modifiers == null || Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < Count; i++)
            {
                sum += _modifiers[i].GetBaseValue();
            }
            return sum;
        }

        public IMagnitudeModifier this[int index] => _modifiers[index];
    }

    // ============================================================================
    // 条件组合器
    // ============================================================================

    /// <summary>
    /// 条件选择器
    /// </summary>
    public delegate bool ModifierCondition(IModifierContext context);

    /// <summary>
    /// 条件修饰器对
    /// </summary>
    public struct ConditionalModifier
    {
        public ModifierCondition Condition { get; private set; }
        public IMagnitudeModifier Modifier { get; private set; }

        public ConditionalModifier(ModifierCondition condition, IMagnitudeModifier modifier)
        {
            Condition = condition;
            Modifier = modifier;
        }
    }

    /// <summary>
    /// 条件组合器。
    /// 根据条件选择不同的修饰器执行。
    ///
    /// 流程：
    /// if condition0: result = modifier0.Modify(baseValue)
    /// else if condition1: result = modifier1.Modify(baseValue)
    /// else: result = baseValue
    /// </summary>
    public struct ConditionalComposer : IModifierComposer
    {
        /// <summary>默认修饰器（无条件）</summary>
        public IMagnitudeModifier DefaultModifier { get; private set; }

        /// <summary>条件修饰器对</summary>
        public ConditionalModifier[] Conditions { get; private set; }

        /// <summary>条件数量</summary>
        public byte Count => (byte)(Conditions?.Length ?? 0);

        public ModifierCompositionType CompositionType => ModifierCompositionType.Conditional;

        public ConditionalComposer(IMagnitudeModifier defaultModifier, params ConditionalModifier[] conditions)
        {
            DefaultModifier = defaultModifier;
            Conditions = conditions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compose(float baseValue, IModifierContext context)
        {
            if (Conditions != null)
            {
                for (int i = 0; i < Conditions.Length; i++)
                {
                    if (Conditions[i].Condition(context))
                        return Conditions[i].Modifier.Modify(context, baseValue);
                }
            }

            return DefaultModifier?.Modify(context, baseValue) ?? baseValue;
        }

        public float GetMaxBaseValue()
        {
            float max = DefaultModifier?.GetBaseValue() ?? 0f;

            if (Conditions != null)
            {
                for (int i = 0; i < Conditions.Length; i++)
                {
                    float value = Conditions[i].Modifier?.GetBaseValue() ?? 0f;
                    if (value > max) max = value;
                }
            }

            return max;
        }
    }

    // ============================================================================
    // 修饰器组合器工厂
    // ============================================================================

    /// <summary>
    /// 修饰器组合器工厂
    /// </summary>
    public static class ModifierComposerFactory
    {
        /// <summary>
        /// 创建链式组合器
        /// </summary>
        public static ChainComposer Chain(params IMagnitudeModifier[] modifiers)
            => new(modifiers);

        /// <summary>
        /// 创建并行组合器
        /// </summary>
        public static ParallelComposer Parallel(params IMagnitudeModifier[] modifiers)
            => new(modifiers);

        /// <summary>
        /// 创建条件组合器
        /// </summary>
        public static ConditionalComposer When(
            ModifierCondition condition,
            IMagnitudeModifier modifier,
            IMagnitudeModifier defaultModifier = null)
            => new(defaultModifier, new ConditionalModifier(condition, modifier));

        /// <summary>
        /// 创建条件组合器（多条件）
        /// </summary>
        public static ConditionalComposer WhenAny(
            IMagnitudeModifier defaultModifier,
            params ConditionalModifier[] conditions)
            => new(defaultModifier, conditions);
    }
}