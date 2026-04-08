using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    // ============================================================================
    // 属性修改器聚合结果（旧版 API）
    // ============================================================================

    /// <summary>
    /// 属性修改器聚合结果。
    /// 用于旧版 API 的参数传递。
    ///
    /// 新版 API 使用 AbilityKit.Modifiers.ModifierResult。
    /// 此类型保留用于向后兼容。
    /// </summary>
    public readonly struct AttributeModifierSet
    {
        /// <summary>加法修改器之和</summary>
        public float Add { get; }

        /// <summary>乘法修改器之和（百分比形式，如 0.2 表示 +20%）</summary>
        public float Mul { get; }

        /// <summary>最终加法（在公式最后执行的加法）</summary>
        public float FinalAdd { get; }

        /// <summary>覆盖值（如果有 Override 操作）</summary>
        public float Override { get; }

        /// <summary>是否有覆盖操作</summary>
        public bool HasOverride { get; }

        public AttributeModifierSet(float add, float mul, float finalAdd, float @override, bool hasOverride)
        {
            Add = add;
            Mul = mul;
            FinalAdd = finalAdd;
            Override = @override;
            HasOverride = hasOverride;
        }

        /// <summary>
        /// 从 ModifierResult 创建 AttributeModifierSet
        /// </summary>
        public static AttributeModifierSet FromResult(ModifierResult result)
        {
            float mul = result.MulProduct - 1f;  // MulProduct 是 (1 + mul) 的值
            return new AttributeModifierSet(
                result.AddSum,
                mul,
                finalAdd: 0f,  // ModifierResult 不支持 FinalAdd
                result.OverrideValue,
                result.HasOverride
            );
        }

        /// <summary>
        /// 计算最终值
        /// </summary>
        public float CalculateFinal(float baseValue)
        {
            if (HasOverride)
            {
                return Override;
            }

            return (baseValue + Add) * (1f + Mul) + FinalAdd;
        }

        /// <summary>空聚合结果</summary>
        public static AttributeModifierSet Empty => new AttributeModifierSet(0f, 0f, 0f, 0f, false);

        /// <summary>是否为空</summary>
        public bool IsEmpty => !HasOverride && Add == 0f && Mul == 0f && FinalAdd == 0f;
    }
}
