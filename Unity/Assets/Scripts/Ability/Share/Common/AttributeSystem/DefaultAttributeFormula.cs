using System;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class DefaultAttributeFormula : IAttributeFormula
    {
        public static readonly DefaultAttributeFormula Instance = new DefaultAttributeFormula();

        private DefaultAttributeFormula() { }

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
