namespace AbilityKit.Effects.Core.Model
{
    public readonly struct EffectValue
    {
        public readonly int I;
        public readonly float F;

        public EffectValue(int i)
        {
            I = i;
            F = 0f;
        }

        public EffectValue(float f)
        {
            I = 0;
            F = f;
        }
    }
}
