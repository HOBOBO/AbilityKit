using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性公式接口。
    /// 定义如何将基础值和修改器聚合结果计算出最终值。
    ///
    /// 旧版 API（保留兼容）：直接传入 AttributeModifierSet
    /// 新版 API：使用 AbilityKit.Modifiers.ModifierResult
    /// </summary>
    public interface IAttributeFormula
    {
        /// <summary>
        /// 评估属性值（旧版 API，保留兼容）
        /// </summary>
        float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, in AttributeModifierSet modifiers);

        /// <summary>
        /// 评估属性值（新版 API，使用 ModifierResult）
        /// 默认实现将 ModifierResult 转换为 AttributeModifierSet 然后调用旧版 API
        /// </summary>
        float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult)
        {
            // 默认实现：转换为旧版格式
            var set = AttributeModifierSet.FromResult(modifierResult);
            return Evaluate(ctx, self, baseValue, in set);
        }
    }
}
