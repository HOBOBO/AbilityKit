using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 衰减类型枚举
    // ============================================================================

    /// <summary>
    /// 衰减类型
    /// </summary>
    public enum DecayType : byte
    {
        /// <summary>线性衰减</summary>
        Linear = 0,

        /// <summary>指数衰减（先快后慢）</summary>
        Exponential = 1,

        /// <summary>对数衰减（先慢后快）</summary>
        Logarithmic = 2,

        /// <summary>缓出曲线（先快后慢）</summary>
        EaseOut = 3,

        /// <summary>缓入曲线（先慢后快）</summary>
        EaseIn = 4,

        /// <summary>缓入缓出</summary>
        EaseInOut = 5,

        /// <summary>自定义曲线</summary>
        CustomCurve = 6,
    }

    // ============================================================================
    // 修饰器标记属性
    // ============================================================================

    /// <summary>
    /// 修饰器类型标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class MagnitudeModifierAttribute : Attribute
    {
        /// <summary>修饰器类型 ID（用于序列化）</summary>
        public byte ModifierTypeId { get; }

        public MagnitudeModifierAttribute(byte typeId)
        {
            ModifierTypeId = typeId;
        }
    }

    // ============================================================================
    // 核心接口：数值修饰器
    // ============================================================================

    /// <summary>
    /// 数值修饰器接口。
    /// 支持修饰器链式组合，每个修饰器负责特定的数值变换。
    ///
    /// 组合示例：
    /// ```csharp
    /// // 创建一个"时间衰减 + 等级曲线 + 属性引用"的修改器
    /// var pipeline = ModifierPipeline.Create()
    ///     .Then(new TimeDecayModifier(duration: 5f, decayType: DecayType.Exponential))
    ///     .Then(new LevelCurveModifier(curve: levelUpCurve))
    ///     .Then(new AttributeRefModifier(AttributeKey.Strength, coefficient: 0.5f));
    ///
    /// // 计算最终数值
    /// float result = pipeline.Calculate(context, baseValue: 100f);
    /// ```
    /// </summary>
    public interface IMagnitudeModifier
    {
        /// <summary>修饰器类型 ID（用于序列化）</summary>
        byte ModifierTypeId { get; }

        /// <summary>修饰器名称（用于调试）</summary>
        string Name { get; }

        /// <summary>
        /// 修改数值
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="input">输入值（来自前一个修饰器的输出）</param>
        /// <returns>修改后的值</returns>
        float Modify(IModifierContext context, float input);

        /// <summary>
        /// 获取基础值（用于缓存/比较）
        /// </summary>
        float GetBaseValue();
    }

    // ============================================================================
    // 内置修饰器实现
    // ============================================================================

    /// <summary>
    /// 固定值修饰器（无操作，直接传递输入值）
    /// </summary>
    [MagnitudeModifier(0)]
    public struct FixedModifier : IMagnitudeModifier
    {
        public byte ModifierTypeId => 0;
        public string Name => "Fixed";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input) => input;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetBaseValue() => 1f;
    }

    /// <summary>
    /// 时间衰减修饰器。
    /// 根据已过时间从初始值逐渐衰减到 0。
    ///
    /// 使用示例：
    /// ```csharp
    /// var decay = new TimeDecayModifier(50f, 5f, DecayType.Exponential);
    /// float currentValue = decay.Modify(context, 0f); // 随时间变化
    /// ```
    /// </summary>
    [MagnitudeModifier(1)]
    public struct TimeDecayModifier : IMagnitudeModifier
    {
        /// <summary>初始值</summary>
        public float InitialValue;

        /// <summary>持续时间（秒）</summary>
        public float Duration;

        /// <summary>衰减类型</summary>
        public DecayType DecayType;

        /// <summary>衰减系数（用于指数衰减）</summary>
        public float Coefficient;

        /// <summary>曲线数据（用于 CustomCurve）</summary>
        public float[] Curve;

        public byte ModifierTypeId => 1;
        public string Name => "TimeDecay";

        public TimeDecayModifier(float initialValue = 1f, float duration = 5f, DecayType decayType = DecayType.Linear, float coefficient = 1f)
        {
            InitialValue = initialValue;
            Duration = duration;
            DecayType = decayType;
            Coefficient = coefficient;
            Curve = null;
        }

        public TimeDecayModifier(float initialValue, float duration, float[] curve)
        {
            InitialValue = initialValue;
            Duration = duration;
            DecayType = DecayType.CustomCurve;
            Coefficient = 1f;
            Curve = curve;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input)
        {
            if (context == null || Duration <= 0f)
                return InitialValue * Coefficient;

            float elapsed = context.ElapsedTime;
            float t = Math.Min(elapsed / Duration, 1f);

            if (t >= 1f) return 0f;

            float decayMultiplier = CalculateDecay(t);
            return InitialValue * Coefficient * decayMultiplier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetBaseValue() => InitialValue * Coefficient;

        private float CalculateDecay(float t)
        {
            return DecayType switch
            {
                DecayType.Linear => 1f - t,
                DecayType.Exponential => (float)Math.Exp(-2f * t),
                DecayType.Logarithmic => 1f - MathF.Log(1f + t) / MathF.Log(2f),
                DecayType.EaseOut => 1f - MathF.Pow(1f - t, 2f),
                DecayType.EaseIn => MathF.Pow(t, 2f),
                DecayType.EaseInOut => t < 0.5f
                    ? 2f * t * t
                    : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f,
                DecayType.CustomCurve => InterpolateCurve(t),
                _ => 1f - t
            };
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

    /// <summary>
    /// 等级曲线修饰器。
    /// 根据等级从曲线中插值获取缩放因子。
    ///
    /// 使用示例：
    /// ```csharp
    /// float[] curve = { 1f, 1f, 5f, 1.5f, 10f, 2f }; // [level, value, ...]
    /// var levelCurve = new LevelCurveModifier(10f, curve);
    /// float bonus = levelCurve.Modify(context, 0f); // 基于等级返回加成值
    /// ```
    /// </summary>
    [MagnitudeModifier(2)]
    public struct LevelCurveModifier : IMagnitudeModifier
    {
        /// <summary>基础值</summary>
        public float BaseValue;

        /// <summary>曲线数据 [level0, value0, level1, value1, ...]</summary>
        public float[] Curve;

        public byte ModifierTypeId => 2;
        public string Name => "LevelCurve";

        public LevelCurveModifier(float baseValue = 1f, float[] curve = null)
        {
            BaseValue = baseValue;
            Curve = curve;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input)
        {
            float level = context?.Level ?? 1f;
            float multiplier = InterpolateCurve(level);
            return BaseValue * multiplier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetBaseValue() => BaseValue;

        private float InterpolateCurve(float level)
        {
            if (Curve == null || Curve.Length < 2) return 1f;
            if (Curve.Length == 2) return Curve[1];

            int count = Curve.Length / 2;
            if (count == 0) return 1f;
            if (count == 1) return Curve[1];

            for (int i = 0; i < count - 1; i++)
            {
                float level0 = Curve[i * 2];
                float value0 = Curve[i * 2 + 1];
                float level1 = Curve[(i + 1) * 2];
                float value1 = Curve[(i + 1) * 2 + 1];

                if (level <= level1)
                {
                    float t = (level - level0) / (level1 - level0);
                    return value0 + (value1 - value0) * t;
                }
            }

            return Curve[Curve.Length - 1];
        }
    }

    /// <summary>
    /// 属性引用修饰器。
    /// 获取指定属性的值并乘以系数。
    ///
    /// 使用示例：
    /// ```csharp
    /// var attrRef = new AttributeRefModifier(ModifierKey.Strength, 0.5f);
    /// float bonus = attrRef.Modify(context, 0f); // 返回 Strength * 0.5
    /// ```
    /// </summary>
    [MagnitudeModifier(3)]
    public struct AttributeRefModifier : IMagnitudeModifier
    {
        /// <summary>引用的属性键</summary>
        public ModifierKey AttributeKey;

        /// <summary>系数</summary>
        public float Coefficient;

        public byte ModifierTypeId => 3;
        public string Name => "AttributeRef";

        public AttributeRefModifier(ModifierKey attributeKey, float coefficient = 1f)
        {
            AttributeKey = attributeKey;
            Coefficient = coefficient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input)
        {
            if (context == null) return 0f;
            float attrValue = context.GetAttribute(AttributeKey);
            return attrValue * Coefficient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetBaseValue() => Coefficient;
    }

    /// <summary>
    /// 叠加修饰器。
    /// 将多个修饰器的结果相加。
    ///
    /// 使用示例：
    /// ```csharp
    /// var stack = new StackingModifier(
    ///     new TimeDecayModifier(10f, 5f),
    ///     new LevelCurveModifier(5f, curve)
    /// );
    /// float result = stack.Modify(context, 0f); // 返回两个修饰器的和
    /// ```
    /// </summary>
    [MagnitudeModifier(4)]
    public struct StackingModifier : IMagnitudeModifier
    {
        /// <summary>子修饰器数组</summary>
        public IMagnitudeModifier[] Modifiers;

        public byte ModifierTypeId => 4;
        public string Name => "Stacking";

        public StackingModifier(params IMagnitudeModifier[] modifiers)
        {
            Modifiers = modifiers;
        }

        public float Modify(IModifierContext context, float input)
        {
            if (Modifiers == null || Modifiers.Length == 0) return input;

            float result = input;
            for (int i = 0; i < Modifiers.Length; i++)
            {
                result += Modifiers[i].Modify(context, 0f);
            }
            return result;
        }

        public float GetBaseValue()
        {
            if (Modifiers == null || Modifiers.Length == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < Modifiers.Length; i++)
            {
                sum += Modifiers[i].GetBaseValue();
            }
            return sum;
        }
    }

    /// <summary>
    /// 缩放修饰器。
    /// 将输入值乘以缩放因子。
    ///
    /// 使用示例：
    /// ```csharp
    /// var scale = new ScaleModifier(1.5f);
    /// float result = scale.Modify(context, 100f); // 返回 150f
    /// ```
    /// </summary>
    [MagnitudeModifier(5)]
    public struct ScaleModifier : IMagnitudeModifier
    {
        /// <summary>缩放系数</summary>
        public float Scale;

        public byte ModifierTypeId => 5;
        public string Name => "Scale";

        public ScaleModifier(float scale = 1f)
        {
            Scale = scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input) => input * Scale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetBaseValue() => Scale;
    }

    // ============================================================================
    // 修饰器管道
    // ============================================================================

    /// <summary>
    /// 修饰器管道。
    /// 管理多个修饰器的组合计算。
    ///
    /// 设计原则：
    /// - 修饰器按添加顺序依次执行
    /// - 支持任意数量的修饰器组合
    /// - 零 GC 友好的值类型实现
    ///
    /// 使用示例：
    /// ```csharp
    /// // 创建管道：基础值 → 时间衰减 → 等级缩放 → 属性加成
    /// var pipeline = ModifierPipeline.Create(
    ///     new TimeDecayModifier(100f, 5f, DecayType.Exponential),
    ///     new LevelCurveModifier(10f, levelCurve),
    ///     new AttributeRefModifier(ModifierKey.Bonus, 0.2f)
    /// );
    ///
    /// float result = pipeline.Calculate(context, baseValue: 0f);
    /// ```
    /// </summary>
    public struct ModifierPipeline : IMagnitudeModifier
    {
        /// <summary>修饰器数组</summary>
        private readonly IMagnitudeModifier[] _modifiers;

        /// <summary>修饰器数量</summary>
        private readonly byte _count;

        /// <summary>管道名称</summary>
        public string Name { get; }

        /// <summary>修饰器类型 ID</summary>
        public byte ModifierTypeId => 255; // 特殊标记

        public byte Count => _count;

        #region 构造函数

        private ModifierPipeline(string name, IMagnitudeModifier[] modifiers, byte count)
        {
            Name = name;
            _modifiers = modifiers;
            _count = count;
        }

        /// <summary>
        /// 创建修饰器管道
        /// </summary>
        public ModifierPipeline(params IMagnitudeModifier[] modifiers)
        {
            Name = "Pipeline";
            _count = (byte)modifiers.Length;
            _modifiers = modifiers.Length > 0 ? modifiers : null;
        }

        #endregion

        #region IMagnitudeModifier

        /// <summary>
        /// 计算管道输出
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Modify(IModifierContext context, float input)
        {
            if (_modifiers == null || _count == 0) return input;

            float result = input;
            for (int i = 0; i < _count; i++)
            {
                result = _modifiers[i].Modify(context, result);
            }
            return result;
        }

        /// <summary>
        /// 获取最大基础值（用于缓存估计）
        /// </summary>
        public float GetBaseValue()
        {
            if (_modifiers == null || _count == 0) return 0f;

            float max = 0f;
            for (int i = 0; i < _count; i++)
            {
                max += _modifiers[i].GetBaseValue();
            }
            return max;
        }

        #endregion

        #region 链式 API

        /// <summary>
        /// 添加修饰器
        /// </summary>
        public ModifierPipeline Then(IMagnitudeModifier modifier)
        {
            if (modifier == null) return this;

            var newModifiers = new IMagnitudeModifier[_count + 1];
            if (_modifiers != null)
            {
                Array.Copy(_modifiers, newModifiers, _count);
            }
            newModifiers[_count] = modifier;

            return new ModifierPipeline($"Pipeline+{modifier.Name}", newModifiers, (byte)(_count + 1));
        }

        /// <summary>
        /// 添加时间衰减
        /// </summary>
        public ModifierPipeline ThenTimeDecay(float initialValue, float duration, DecayType decayType = DecayType.Linear)
            => Then(new TimeDecayModifier(initialValue, duration, decayType));

        /// <summary>
        /// 添加等级曲线
        /// </summary>
        public ModifierPipeline ThenLevelCurve(float baseValue, float[] curve)
            => Then(new LevelCurveModifier(baseValue, curve));

        /// <summary>
        /// 添加属性引用
        /// </summary>
        public ModifierPipeline ThenAttributeRef(ModifierKey key, float coefficient = 1f)
            => Then(new AttributeRefModifier(key, coefficient));

        /// <summary>
        /// 添加缩放
        /// </summary>
        public ModifierPipeline ThenScale(float scale)
            => Then(new ScaleModifier(scale));

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建时间衰减管道
        /// </summary>
        public static ModifierPipeline TimeDecay(float initialValue, float duration, DecayType decayType = DecayType.Linear)
            => new(new TimeDecayModifier(initialValue, duration, decayType));

        /// <summary>
        /// 创建等级曲线管道
        /// </summary>
        public static ModifierPipeline LevelCurve(float baseValue, float[] curve)
            => new(new LevelCurveModifier(baseValue, curve));

        /// <summary>
        /// 创建属性引用管道
        /// </summary>
        public static ModifierPipeline AttributeRef(ModifierKey key, float coefficient = 1f)
            => new(new AttributeRefModifier(key, coefficient));

        /// <summary>
        /// 创建缩放管道
        /// </summary>
        public static ModifierPipeline Scale(float scale)
            => new(new ScaleModifier(scale));

        #endregion

        #region 索引访问

        /// <summary>
        /// 获取指定索引的修饰器
        /// </summary>
        public IMagnitudeModifier this[int index]
        {
            get
            {
                if (index < 0 || index >= _count || _modifiers == null)
                    throw new IndexOutOfRangeException();
                return _modifiers[index];
            }
        }

        #endregion

        public override string ToString()
            => $"ModifierPipeline({Name}, Count={_count})";
    }

    // ============================================================================
    // 修饰器工具类
    // ============================================================================

    /// <summary>
    /// 修饰器工具类
    /// </summary>
    public static class MagnitudeModifierUtils
    {
        /// <summary>
        /// 计算衰减曲线上的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateDecay(float t, DecayType decayType)
        {
            return decayType switch
            {
                DecayType.Linear => 1f - t,
                DecayType.Exponential => (float)Math.Exp(-2f * t),
                DecayType.Logarithmic => 1f - MathF.Log(1f + t) / MathF.Log(2f),
                DecayType.EaseOut => 1f - MathF.Pow(1f - t, 2f),
                DecayType.EaseIn => MathF.Pow(t, 2f),
                DecayType.EaseInOut => t < 0.5f
                    ? 2f * t * t
                    : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f,
                DecayType.CustomCurve => 1f - t,
                _ => 1f - t
            };
        }

        /// <summary>
        /// 从曲线插值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InterpolateCurve(float level, float[] curve)
        {
            if (curve == null || curve.Length < 2) return 1f;
            if (curve.Length == 2) return curve[1];

            int count = curve.Length / 2;
            if (count == 0) return 1f;
            if (count == 1) return curve[1];

            for (int i = 0; i < count - 1; i++)
            {
                float level0 = curve[i * 2];
                float value0 = curve[i * 2 + 1];
                float level1 = curve[(i + 1) * 2];
                float value1 = curve[(i + 1) * 2 + 1];

                if (level <= level1)
                {
                    float t = (level - level0) / (level1 - level0);
                    return value0 + (value1 - value0) * t;
                }
            }

            return curve[curve.Length - 1];
        }
    }
}
