using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 数值来源类型枚举（用于序列化）
    // ============================================================================

    /// <summary>
    /// 数值来源类型枚举
    /// </summary>
    public enum ValueSourceType : byte
    {
        /// <summary>固定值</summary>
        Fixed = 0,

        /// <summary>等级曲线</summary>
        Scalable = 1,

        /// <summary>属性引用</summary>
        Attribute = 2,

        /// <summary>时间衰减</summary>
        TimeDecay = 3,

        /// <summary>修饰器管道</summary>
        Pipeline = 4,
    }

    // ============================================================================
    // 数值来源接口
    // ============================================================================

    /// <summary>
    /// 数值来源接口。
    /// 定义如何根据上下文计算当前数值。
    ///
    /// 设计原则：
    /// - 单一职责：只负责根据上下文计算数值
    /// - 可组合：通过 IMagnitudeModifier 组合多个变换
    /// - 可序列化：支持 TypeId 用于反序列化
    ///
    /// 使用示例：
    /// ```csharp
    /// // 定义
    /// public interface IValueSource
    /// {
    ///     byte TypeId { get; }
    ///     float Calculate(float level, IModifierContext context);
    /// }
    ///
    /// // 使用
    /// IValueSource source = new TimeDecaySource(50f, 5f);
    /// float value = source.Calculate(1f, context);
    /// ```
    /// </summary>
    public interface IValueSource
    {
        /// <summary>类型 ID（用于序列化/反序列化）</summary>
        byte TypeId { get; }

        /// <summary>
        /// 计算当前数值
        /// </summary>
        /// <param name="level">等级</param>
        /// <param name="context">上下文</param>
        /// <returns>计算后的数值</returns>
        float Calculate(float level, IModifierContext context);

        /// <summary>
        /// 获取基础值（用于缓存估计/比较）
        /// </summary>
        float BaseValue { get; }

        /// <summary>
        /// 是否是时变来源（值会随时间变化）
        /// </summary>
        bool IsTimeVarying { get; }
    }

    // ============================================================================
    // 固定值来源
    // ============================================================================

    /// <summary>
    /// 固定值来源。
    /// 返回恒定的数值。
    /// </summary>
    public struct FixedValueSource : IValueSource
    {
        public byte TypeId => 0;
        public float BaseValue { get; private set; }
        public bool IsTimeVarying => false;

        public FixedValueSource(float value)
        {
            BaseValue = value;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float Calculate(float level, IModifierContext context) => BaseValue;
    }

    // ============================================================================
    // 等级曲线来源
    // ============================================================================

    /// <summary>
    /// 等级曲线来源。
    /// 根据等级从曲线中插值获取数值。
    /// </summary>
    public struct LevelCurveSource : IValueSource
    {
        public byte TypeId => 2;
        public float BaseValue { get; private set; }
        public float Coefficient { get; private set; }
        public float[] Curve { get; private set; }
        public bool IsTimeVarying => false;

        public LevelCurveSource(float baseValue, float coefficient, float[] curve)
        {
            BaseValue = baseValue;
            Coefficient = coefficient;
            Curve = curve;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float Calculate(float level, IModifierContext context)
        {
            float multiplier = MagnitudeModifierUtils.InterpolateCurve(level, Curve);
            return BaseValue * Coefficient * multiplier;
        }
    }

    // ============================================================================
    // 属性引用来源
    // ============================================================================

    /// <summary>
    /// 属性引用来源。
    /// 获取指定属性的值并乘以系数。
    /// </summary>
    public struct AttributeRefSource : IValueSource
    {
        public byte TypeId => 3;
        public float BaseValue => Coefficient;
        public float Coefficient { get; private set; }
        public ModifierKey AttributeKey { get; private set; }
        public bool IsTimeVarying => false;

        public AttributeRefSource(ModifierKey attributeKey, float coefficient)
        {
            AttributeKey = attributeKey;
            Coefficient = coefficient;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float Calculate(float level, IModifierContext context)
        {
            if (context == null) return 0f;
            return context.GetAttribute(AttributeKey) * Coefficient;
        }
    }

    // ============================================================================
    // 时间衰减来源
    // ============================================================================

    /// <summary>
    /// 时间衰减来源。
    /// 根据已过时间从初始值逐渐衰减到 0。
    /// </summary>
    public struct TimeDecaySource : IValueSource
    {
        public byte TypeId => 1;
        public float BaseValue { get; private set; }
        public float Duration { get; private set; }
        public DecayType DecayType { get; private set; }
        public float Coefficient { get; private set; }
        public float[] Curve { get; private set; }
        public bool IsTimeVarying => true;

        public TimeDecaySource(float initialValue, float duration, DecayType decayType = DecayType.Linear, float coefficient = 1f)
        {
            BaseValue = initialValue;
            Duration = duration;
            DecayType = decayType;
            Coefficient = coefficient;
            Curve = null;
        }

        public TimeDecaySource(float initialValue, float duration, float[] curve)
        {
            BaseValue = initialValue;
            Duration = duration;
            DecayType = DecayType.CustomCurve;
            Coefficient = 1f;
            Curve = curve;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float Calculate(float level, IModifierContext context)
        {
            if (context == null || Duration <= 0f)
                return BaseValue * Coefficient;

            float elapsed = context.ElapsedTime;
            float t = Math.Min(elapsed / Duration, 1f);

            if (t >= 1f) return 0f;

            float decayMultiplier = DecayType == DecayType.CustomCurve
                ? InterpolateCurve(t)
                : MagnitudeModifierUtils.CalculateDecay(t, DecayType);

            return BaseValue * Coefficient * decayMultiplier;
        }

        private float InterpolateCurve(float t)
        {
            if (Curve == null || Curve.Length < 2) return 1f - t;
            if (Curve.Length == 1) return Curve[0];

            int count = Curve.Length;
            int index = (int)(t * (count - 1));
            index = Math.Clamp(index, 0, count - 2);

            float localT = (t * (count - 1)) - index;
            return Curve[index] * (1f - localT) + Curve[index + 1] * localT;
        }
    }

    // ============================================================================
    // 修饰器管道来源
    // ============================================================================

    /// <summary>
    /// 修饰器管道来源。
    /// 通过修饰器管道计算数值。
    /// </summary>
    public struct PipelineSource : IValueSource
    {
        public byte TypeId => 4;
        public float BaseValue { get; private set; }
        public MagnitudePipelineData PipelineData { get; private set; }
        public bool IsTimeVarying { get; private set; }

        public PipelineSource(float baseValue, MagnitudePipelineData pipelineData, bool isTimeVarying)
        {
            BaseValue = baseValue;
            PipelineData = pipelineData;
            IsTimeVarying = isTimeVarying;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float Calculate(float level, IModifierContext context)
        {
            if (PipelineData.IsEmpty)
                return BaseValue;

            return PipelineData.Calculate(context, 0f);
        }
    }

    // ============================================================================
    // 来源注册表
    // ============================================================================

    /// <summary>
    /// 数值来源注册表。
    /// 用于运行时注册自定义来源类型。
    /// </summary>
    public static class ValueSourceRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<byte, Func<IValueSource>> _factories = new();
        private static bool _initialized = false;

        /// <summary>
        /// 注册自定义来源工厂
        /// </summary>
        public static void Register(byte typeId, Func<IValueSource> factory)
        {
            _factories[typeId] = factory;
        }

        /// <summary>
        /// 创建指定类型的来源
        /// </summary>
        public static IValueSource Create(byte typeId)
        {
            if (_factories.TryGetValue(typeId, out var factory))
                return factory();

            return new FixedValueSource(0f);
        }
    }
}
