namespace AbilityKit.Core.Common.Numbers
{
    public enum NumberModifierOp
    {
        Add = 0,
        Mul = 1,
        FinalAdd = 2,
        Override = 3,
        Custom = 4
    }

    public readonly struct NumberModifier
    {
        public readonly NumberModifierOp Op;
        public readonly float Value;
        public readonly int SourceId;

        public NumberModifier(NumberModifierOp op, float value, int sourceId = 0)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
        }
    }

    public readonly struct NumberModifierHandle
    {
        public readonly int Value;

        internal NumberModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;
    }

    public readonly struct NumberModifierSet
    {
        public readonly float Add;
        public readonly float Mul;
        public readonly float FinalAdd;
        public readonly float Override;
        public readonly bool HasOverride;

        public NumberModifierSet(float add, float mul, float finalAdd, float @override, bool hasOverride)
        {
            Add = add;
            Mul = mul;
            FinalAdd = finalAdd;
            Override = @override;
            HasOverride = hasOverride;
        }
    }
}
