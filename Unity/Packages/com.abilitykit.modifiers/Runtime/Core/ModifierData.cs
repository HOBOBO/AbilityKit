using System;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 修改器数据。
    /// 表示一个具体的、可生效的修改器实例。
    ///
    /// 纯值类型，无堆分配。
    /// </summary>
    [Serializable]
    public struct ModifierData : IEquatable<ModifierData>
    {
        /// <summary>修改目标键</summary>
        public ModifierKey Key;

        /// <summary>操作类型</summary>
        public ModifierOp Op;

        /// <summary>
        /// 数值来源类型
        /// </summary>
        public MagnitudeType MagnitudeSource;

        /// <summary>数值（当 MagnitudeSource = None 时使用）</summary>
        public float Value;

        /// <summary>
        /// 可缩放数值（当 MagnitudeSource = ScalableFloat 时使用）
        /// </summary>
        public ScalableFloat ScalableValue;

        /// <summary>
        /// 基于属性的数值（当 MagnitudeSource = AttributeBased 时使用）
        /// </summary>
        public AttributeBasedMagnitude AttributeValue;

        /// <summary>优先级。数字越小越先计算。同一标签下按优先级排序。</summary>
        public int Priority;

        /// <summary>来源标识（业务层定义，用于溯源和批量移除）</summary>
        public int SourceId;

        /// <summary>来源名称索引（用于调试显示，业务层自行映射）</summary>
        public int SourceNameIndex;

        /// <summary>
        /// 叠加配置（可选）
        /// </summary>
        public StackingConfig? Stacking;

        /// <summary>
        /// 自定义数据槽（用于扩展类型的修改器）
        /// 例如：技能 ID、Prefab 引用、配置结构等
        /// </summary>
        public CustomModifierData CustomData;

        #region 工厂方法

        /// <summary>创建加法修改器（固定值）</summary>
        public static ModifierData Add(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Value = value,
                MagnitudeSource = MagnitudeType.None,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建乘法修改器（固定值）</summary>
        public static ModifierData Mul(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Value = value,
                MagnitudeSource = MagnitudeType.None,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建覆盖修改器（固定值）</summary>
        public static ModifierData Override(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0)
            => new()
            {
                Key = key,
                Op = ModifierOp.Override,
                Value = value,
                MagnitudeSource = MagnitudeType.None,
                Priority = 0,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建百分比加成修改器（固定值，Value=0.2 表示 +20%）</summary>
        public static ModifierData PercentAdd(
            ModifierKey key,
            float percentValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.PercentAdd,
                Value = percentValue,
                MagnitudeSource = MagnitudeType.None,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建加法修改器（可缩放值）</summary>
        public static ModifierData AddScalable(
            ModifierKey key,
            ScalableFloat scalableValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Value = 0f,
                MagnitudeSource = MagnitudeType.ScalableFloat,
                ScalableValue = scalableValue,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建乘法修改器（可缩放值）</summary>
        public static ModifierData MulScalable(
            ModifierKey key,
            ScalableFloat scalableValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Value = 0f,
                MagnitudeSource = MagnitudeType.ScalableFloat,
                ScalableValue = scalableValue,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建基于属性的加法修改器</summary>
        public static ModifierData AddAttributeBased(
            ModifierKey key,
            AttributeBasedMagnitude attributeValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Value = 0f,
                MagnitudeSource = MagnitudeType.AttributeBased,
                AttributeValue = attributeValue,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建基于属性的乘法修改器</summary>
        public static ModifierData MulAttributeBased(
            ModifierKey key,
            AttributeBasedMagnitude attributeValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Value = 0f,
                MagnitudeSource = MagnitudeType.AttributeBased,
                AttributeValue = attributeValue,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建自定义修改器（使用 CustomData）</summary>
        public static ModifierData Custom(
            ModifierKey key,
            ModifierOp op,
            CustomModifierData customData,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                Value = 0f,
                MagnitudeSource = MagnitudeType.None,
                CustomData = customData,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        #endregion

        #region 计算

        /// <summary>
        /// 获取当前生效的数值（根据 MagnitudeSource 计算）
        /// </summary>
        /// <param name="level">当前等级（用于 ScalableFloat）</param>
        /// <param name="context">上下文（用于 AttributeBased）</param>
        public float GetMagnitude(float level = 1f, IModifierContext context = null)
        {
            return MagnitudeSource switch
            {
                MagnitudeType.None => Value,
                MagnitudeType.ScalableFloat => ScalableValue.Calculate(level),
                MagnitudeType.AttributeBased => AttributeValue.Calculate(key => context?.GetAttribute(key) ?? 0f),
                _ => Value
            };
        }

        #endregion

        #region IEquatable

        public bool Equals(ModifierData other)
            => Key == other.Key
            && Op == other.Op
            && Value == other.Value
            && Priority == other.Priority
            && SourceId == other.SourceId;

        public override bool Equals(object obj) => obj is ModifierData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Key, Op, Value.GetHashCode(), Priority, SourceId);

        #endregion

        public override string ToString()
            => $"Mod({Key}, {Op} {Value}, Prio={Priority}, Src={SourceId})";
    }

    /// <summary>
    /// 数值来源类型
    /// </summary>
    public enum MagnitudeType : byte
    {
        /// <summary>使用固定值</summary>
        None = 0,

        /// <summary>使用 ScalableFloat</summary>
        ScalableFloat = 1,

        /// <summary>使用 AttributeBased</summary>
        AttributeBased = 2,
    }

    /// <summary>
    /// 可缩放的浮点值。
    /// 基础值 × 系数 × 曲线缩放。
    ///
    /// 对标 GAS 的 FScalableFloat。
    /// </summary>
    [Serializable]
    public struct ScalableFloat
    {
        /// <summary>基础值</summary>
        public float BaseValue;

        /// <summary>系数（用于和其他修改器配合）</summary>
        public float Coefficient;

        /// <summary>
        /// 缩放曲线数组。
        /// X 轴为等级，Y 轴为缩放系数。
        /// 格式：level1,curve1,level2,curve2,...
        /// 例如：1,0.5,5,1.0,10,1.5 表示 1 级 0.5x，5 级 1.0x，10 级 1.5x
        /// </summary>
        public float[] Curve;

        /// <summary>计算最终值</summary>
        /// <param name="level">当前等级</param>
        public float Calculate(float level)
        {
            float multiplier = 1f;

            if (Curve != null && Curve.Length >= 2)
            {
                multiplier = InterpolateCurve(level);
            }

            return BaseValue * Coefficient * multiplier;
        }

        private float InterpolateCurve(float level)
        {
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

        public override string ToString()
            => $"{BaseValue:F2} × {Coefficient:F2}";
    }

    /// <summary>
    /// 基于另一个属性计算的数值来源。
    ///
    /// 对标 GAS 的 FAttributeBasedMagnitude。
    /// </summary>
    [Serializable]
    public struct AttributeBasedMagnitude
    {
        /// <summary>参考属性键</summary>
        public ModifierKey AttributeKey;

        /// <summary>使用属性的哪个值</summary>
        public AttributeCaptureType CaptureType;

        /// <summary>系数</summary>
        public float Coefficient;

        /// <summary>计算最终值</summary>
        public float Calculate(Func<ModifierKey, float> captureDelegate)
        {
            if (captureDelegate == null) return 0f;

            float attributeValue = captureDelegate(AttributeKey);

            float captured = CaptureType switch
            {
                AttributeCaptureType.Current => attributeValue,
                AttributeCaptureType.Base => attributeValue,
                AttributeCaptureType.Bonus => 0f,
                _ => 0f
            };

            return captured * Coefficient;
        }

        public override string ToString()
            => $"{AttributeKey} × {Coefficient:F2}";
    }

    /// <summary>
    /// 属性抓取类型
    /// </summary>
    public enum AttributeCaptureType : byte
    {
        /// <summary>使用属性当前值</summary>
        Current = 0,

        /// <summary>使用属性基础值（不含修改器）</summary>
        Base = 1,

        /// <summary>使用属性 Bonus 值（当前 - 基础）</summary>
        Bonus = 2,
    }

    /// <summary>
    /// 自定义修改器数据。
    /// 用于存储非数值类型的修改器数据。
    /// </summary>
    [Serializable]
    public struct CustomModifierData : IEquatable<CustomModifierData>
    {
        /// <summary>自定义类型 ID（业务层定义）</summary>
        public int CustomTypeId;

        /// <summary>整数数据</summary>
        public int IntValue;

        /// <summary>字符串数据（调试用）</summary>
        public string StringValue;

        /// <summary>原始字节数据（用于序列化复杂结构）</summary>
        public byte[] RawData;

        #region 工厂方法

        /// <summary>创建技能 ID 修改器</summary>
        public static CustomModifierData SkillId(int skillId)
            => new() { CustomTypeId = 1, IntValue = skillId };

        /// <summary>创建字符串数据</summary>
        public static CustomModifierData String(string value)
            => new() { CustomTypeId = 2, StringValue = value };

        #endregion

        public bool Equals(CustomModifierData other)
            => CustomTypeId == other.CustomTypeId && IntValue == other.IntValue;

        public override bool Equals(object obj) => obj is CustomModifierData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CustomTypeId, IntValue);
    }
}
