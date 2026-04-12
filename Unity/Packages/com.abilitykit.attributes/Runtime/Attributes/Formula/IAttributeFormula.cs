using AbilityKit.Attributes.Core;
using AbilityKit.Modifiers;

namespace AbilityKit.Attributes.Formula
{
    // ============================================================================
    // 属性公式接口
    // ============================================================================

    /// <summary>
    /// 属性公式接口。
    /// 定义如何将基础值和修改器计算结果计算出最终值。
    ///
    /// 此接口是业务层友好的抽象，内部使用 AbilityKit.Modifiers.ModifierResult。
    /// </summary>
    public interface IAttributeFormula
    {
        /// <summary>
        /// 评估属性值。
        /// </summary>
        /// <param name="ctx">属性上下文</param>
        /// <param name="self">自身属性 ID</param>
        /// <param name="baseValue">基础值</param>
        /// <param name="modifierResult">修改器计算结果</param>
        /// <returns>最终计算值</returns>
        float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult);
    }
}
