namespace AbilityKit.Effects.Core.Model
{
    public sealed class EffectStatItem
    {
        public readonly int KeyId;
        public readonly EffectOp Op;
        public readonly EffectValue Value;

        public EffectStatItem(int keyId, EffectOp op, in EffectValue value)
        {
            KeyId = keyId;
            Op = op;
            Value = value;
        }
    }
}
