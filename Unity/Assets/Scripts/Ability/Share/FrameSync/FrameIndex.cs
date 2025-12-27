namespace AbilityKit.Ability.FrameSync
{
    public readonly struct FrameIndex
    {
        public readonly int Value;

        public FrameIndex(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
