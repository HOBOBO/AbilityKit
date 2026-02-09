namespace AbilityKit.Effects.Core.Model
{
    public sealed class EffectStatItem
    {
        public readonly EffectStatKey Key;
        public readonly EffectOp Op;
        public readonly EffectValue Value;

        public EffectStatItem(EffectStatKey key, EffectOp op, in EffectValue value)
        {
            Key = key;
            Op = op;
            Value = value;
        }
    }
}
