using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    // ============================================================================
    // 默认属性公式
    // ============================================================================

    /// <summary>
    /// 默认属性公式。
    /// 直接使用 ModifierResult.FinalValue 作为最终结果。
    /// </summary>
    public sealed class DefaultAttributeFormula : IAttributeFormula
    {
        /// <summary>单例实例</summary>
        public static readonly DefaultAttributeFormula Instance = new();

        private DefaultAttributeFormula() { }

        /// <summary>
        /// 评估属性值。
        /// 直接使用 ModifierResult.FinalValue。
        /// </summary>
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult)
        {
            var v = modifierResult.FinalValue;

            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                return 0f;
            }

            return v;
        }
    }
}
