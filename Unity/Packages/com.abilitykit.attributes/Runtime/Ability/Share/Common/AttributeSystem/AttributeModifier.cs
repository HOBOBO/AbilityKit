using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性修改器操作类型。
    /// 与 AbilityKit.Modifiers.ModifierOp 的对应关系：
    /// - Add → Add
    /// - Mul → Mul
    /// - Override → Override
    /// - FinalAdd → PercentAdd（语义相似）
    /// - Custom → 自定义操作
    /// </summary>
    public enum AttributeModifierOp
    {
        /// <summary>加法：Base + Value</summary>
        Add = 0,

        /// <summary>百分比加成：Base × (1 + Value)。Value=0.2 表示 +20%</summary>
        Mul = 1,

        /// <summary>最终加法：在公式最后执行的加法。用于 FinalAdd = Base + Add 后再 + FinalAdd</summary>
        FinalAdd = 2,

        /// <summary>覆盖：直接替换为 Value</summary>
        Override = 3,

        /// <summary>自定义操作（业务层扩展）</summary>
        Custom = 100
    }

    /// <summary>
    /// 属性修改器。
    /// 业务层友好的修改器 API，内部会转换为 ModifierData。
    /// </summary>
    public readonly struct AttributeModifier
    {
        /// <summary>操作类型</summary>
        public readonly AttributeModifierOp Op;

        /// <summary>数值</summary>
        public readonly float Value;

        /// <summary>来源标识（用于批量移除）</summary>
        public readonly int SourceId;

        /// <summary>优先级（数字越小越先计算）</summary>
        public readonly byte Priority;

        /// <summary>来源名称索引（用于调试，-1 表示无名称）</summary>
        public readonly short SourceNameIndex;

        public AttributeModifier(AttributeModifierOp op, float value, int sourceId = 0, byte priority = 10, short sourceNameIndex = -1)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
            Priority = priority;
            SourceNameIndex = sourceNameIndex;
        }

        #region 工厂方法

        public static AttributeModifier Add(float value, int sourceId = 0, byte priority = 10)
            => new(AttributeModifierOp.Add, value, sourceId, priority);

        public static AttributeModifier Mul(float value, int sourceId = 0, byte priority = 10)
            => new(AttributeModifierOp.Mul, value, sourceId, priority);

        public static AttributeModifier FinalAdd(float value, int sourceId = 0, byte priority = 5)
            => new(AttributeModifierOp.FinalAdd, value, sourceId, priority);

        public static AttributeModifier Override(float value, int sourceId = 0)
            => new(AttributeModifierOp.Override, value, sourceId, priority: 0);

        #endregion

        #region 转换为 ModifierData

        /// <summary>
        /// 转换为 AbilityKit.Modifiers.ModifierData
        /// </summary>
        public ModifierData ToModifierData(ModifierKey key)
        {
            var op = Op switch
            {
                AttributeModifierOp.Add => ModifierOp.Add,
                AttributeModifierOp.Mul => ModifierOp.PercentAdd,
                AttributeModifierOp.FinalAdd => ModifierOp.Add,  // FinalAdd 也转为加法
                AttributeModifierOp.Override => ModifierOp.Override,
                _ => ModifierOp.Add
            };

            return new ModifierData
            {
                Key = key,
                Op = op,
                Magnitude = MagnitudeSource.Fixed(Value),
                Priority = Priority,
                SourceId = SourceId,
                SourceNameIndex = SourceNameIndex,
                CustomData = CustomModifierData.None
            };
        }

        #endregion

        #region 从 ModifierData 创建

        /// <summary>
        /// 从 AbilityKit.Modifiers.ModifierData 创建 AttributeModifier
        /// </summary>
        public static AttributeModifier FromModifierData(ModifierData data, AttributeModifierOp defaultOp = AttributeModifierOp.Add)
        {
            var attrOp = data.Op switch
            {
                ModifierOp.Add => AttributeModifierOp.Add,
                ModifierOp.Mul => AttributeModifierOp.Mul,
                ModifierOp.PercentAdd => AttributeModifierOp.Mul,
                ModifierOp.Override => AttributeModifierOp.Override,
                _ => defaultOp
            };

            return new AttributeModifier(
                attrOp,
                data.GetMagnitude(),
                data.SourceId,
                (byte)data.Priority,
                data.SourceNameIndex
            );
        }

        #endregion
    }

    /// <summary>
    /// 属性修改器句柄。
    /// 用于移除修改器。
    /// </summary>
    public readonly struct AttributeModifierHandle
    {
        public readonly int Value;

        internal AttributeModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public static readonly AttributeModifierHandle Invalid = new(0);
    }
}
