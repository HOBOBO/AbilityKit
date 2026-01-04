namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public enum AttributeModifierOp
    {
        Add = 0,
        Mul = 1,
        FinalAdd = 2,
        Override = 3,
        Custom = 4
    }

    public readonly struct AttributeModifier
    {
        public readonly AttributeModifierOp Op;
        public readonly float Value;
        public readonly int SourceId;

        public AttributeModifier(AttributeModifierOp op, float value, int sourceId = 0)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
        }
    }

    public readonly struct AttributeModifierHandle
    {
        public readonly int Value;

        internal AttributeModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;
    }

    public readonly struct AttributeModifierSet
    {
        public readonly float Add;
        public readonly float Mul;
        public readonly float FinalAdd;
        public readonly float Override;
        public readonly bool HasOverride;

        public AttributeModifierSet(float add, float mul, float finalAdd, float @override, bool hasOverride)
        {
            Add = add;
            Mul = mul;
            FinalAdd = finalAdd;
            Override = @override;
            HasOverride = hasOverride;
        }
    }
}
