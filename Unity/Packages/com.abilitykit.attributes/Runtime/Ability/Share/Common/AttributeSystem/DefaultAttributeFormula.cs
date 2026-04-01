using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 默认属性公式。
    ///
    /// 计算公式：
    /// 1. 如果有 Override：Final = Override
    /// 2. 否则：Final = (BaseValue + Add) * (1 + Mul) + FinalAdd
    /// </summary>
    public sealed class DefaultAttributeFormula : IAttributeFormula
    {
        public static readonly DefaultAttributeFormula Instance = new DefaultAttributeFormula();

        private DefaultAttributeFormula() { }

        /// <summary>
        /// 评估属性值（新版 API，直接使用 ModifierResult）
        /// </summary>
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult)
        {
            // 直接使用 ModifierResult 的 FinalValue
            var v = modifierResult.FinalValue;

            // 处理 NaN/Infinity
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                return 0f;
            }

            return v;
        }

        /// <summary>
        /// 评估属性值（旧版 API，保留兼容）
        /// </summary>
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, in AttributeModifierSet modifiers)
        {
            var add = modifiers.Add;
            var mul = modifiers.Mul;
            var finalAdd = modifiers.FinalAdd;

            var v = (baseValue + add) * (1f + mul) + finalAdd;

            if (modifiers.HasOverride)
            {
                v = modifiers.Override;
            }

            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                return 0f;
            }

            return v;
        }
    }
}
