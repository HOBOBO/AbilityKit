using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性修改器操作类型。
    /// 与 AbilityKit.Modifiers.ModifierOp 的对应关系：
    /// - Add → Add
    /// - Mul → Mul
    /// - Override → Override
    /// - FinalAdd → Custom(FinalAdd)
    /// - Custom → Custom
    /// </summary>
    public enum AttributeModifierOp
    {
        /// <summary>加法：Base + Value</summary>
        Add = 0,

        /// <summary>乘法：Base × (1 + Value)</summary>
        Mul = 1,

        /// <summary>最终加法：在公式最后执行的加法。用于 FinalAdd = Base + Add 后再 + FinalAdd</summary>
        FinalAdd = 2,

        /// <summary>覆盖：直接替换为 Value</summary>
        Override = 3,

        /// <summary>自定义操作（业务层扩展）</summary>
        Custom = 4
    }

    /// <summary>
    /// 属性修改器。
    /// 这是属性系统特有的修改器类型，兼容现有的 AttributeModifierOp。
    /// 内部可以转换为 AbilityKit.Modifiers.ModifierData。
    /// </summary>
    public readonly struct AttributeModifier
    {
        /// <summary>操作类型</summary>
        public readonly AttributeModifierOp Op;

        /// <summary>数值</summary>
        public readonly float Value;

        /// <summary>来源标识</summary>
        public readonly int SourceId;

        /// <summary>优先级（数字越小越先计算）</summary>
        public readonly int Priority;

        /// <summary>来源名称索引（用于调试）</summary>
        public readonly int SourceNameIndex;

        public AttributeModifier(AttributeModifierOp op, float value, int sourceId = 0, int priority = 10, int sourceNameIndex = 0)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
            Priority = priority;
            SourceNameIndex = sourceNameIndex;
        }

        #region 工厂方法

        public static AttributeModifier Add(float value, int sourceId = 0, int priority = 10)
            => new(AttributeModifierOp.Add, value, sourceId, priority);

        public static AttributeModifier Mul(float value, int sourceId = 0, int priority = 10)
            => new(AttributeModifierOp.Mul, value, sourceId, priority);

        public static AttributeModifier FinalAdd(float value, int sourceId = 0, int priority = 5)
            => new(AttributeModifierOp.FinalAdd, value, sourceId, priority);

        public static AttributeModifier Override(float value, int sourceId = 0)
            => new(AttributeModifierOp.Override, value, sourceId, priority: 0);

        public static AttributeModifier Custom(float value, int sourceId = 0, int priority = 10)
            => new(AttributeModifierOp.Custom, value, sourceId, priority);

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
                AttributeModifierOp.Mul => ModifierOp.Mul,
                AttributeModifierOp.FinalAdd => ModifierOp.Custom,  // 自定义操作
                AttributeModifierOp.Override => ModifierOp.Override,
                AttributeModifierOp.Custom => ModifierOp.Custom,
                _ => ModifierOp.Add
            };

            return new ModifierData
            {
                Key = key,
                Op = op,
                Value = Value,
                MagnitudeSource = MagnitudeType.None,
                Priority = Priority,
                SourceId = SourceId,
                SourceNameIndex = SourceNameIndex,
                CustomData = new CustomModifierData
                {
                    CustomTypeId = (int)Op,  // 存储原始的 AttributeModifierOp
                    IntValue = 0
                }
            };
        }

        /// <summary>
        /// 从 AbilityKit.Modifiers.ModifierData 创建（如果 CustomTypeId 是 AttributeModifierOp）
        /// </summary>
        public static AttributeModifier FromModifierData(ModifierData data)
        {
            var attrOp = data.CustomData.CustomTypeId switch
            {
                (int)AttributeModifierOp.Add => AttributeModifierOp.Add,
                (int)AttributeModifierOp.Mul => AttributeModifierOp.Mul,
                (int)AttributeModifierOp.FinalAdd => AttributeModifierOp.FinalAdd,
                (int)AttributeModifierOp.Override => AttributeModifierOp.Override,
                _ => AttributeModifierOp.Add
            };

            return new AttributeModifier(
                attrOp,
                data.Value,
                data.SourceId,
                data.Priority,
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
    }

    /// <summary>
    /// 属性修改器聚合结果。
    /// 这是属性系统在旧版本中使用的中间格式。
    /// 新代码建议使用 AbilityKit.Modifiers.ModifierResult。
    /// </summary>
    public readonly struct AttributeModifierSet
    {
        /// <summary>加法值之和</summary>
        public readonly float Add;

        /// <summary>乘法值之和（原版是乘积，新版改用加法以支持多来源叠加）</summary>
        public readonly float Mul;

        /// <summary>最终加法值之和</summary>
        public readonly float FinalAdd;

        /// <summary>覆盖值（多个 Override 时取最后一个）</summary>
        public readonly float Override;

        /// <summary>是否有覆盖操作</summary>
        public readonly bool HasOverride;

        public AttributeModifierSet(float add, float mul, float finalAdd, float @override, bool hasOverride)
        {
            Add = add;
            Mul = mul;
            FinalAdd = finalAdd;
            Override = @override;
            HasOverride = hasOverride;
        }

        /// <summary>
        /// 创建空结果
        /// </summary>
        public static AttributeModifierSet Empty => new(0f, 0f, 0f, 0f, false);

        /// <summary>
        /// 从 ModifierResult 创建
        /// </summary>
        public static AttributeModifierSet FromResult(ModifierResult result)
        {
            return new AttributeModifierSet(
                result.AddSum,
                result.MulProduct - 1f,  // MulProduct 是 (1 + M1) × (1 + M2) ...，需要减 1
                0f,  // FinalAdd 单独处理
                result.OverrideValue ?? 0f,
                result.HasOverride
            );
        }
    }
}
