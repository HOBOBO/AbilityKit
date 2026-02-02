namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public readonly struct TagSource
    {
        public readonly long Value;

        public TagSource(long value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public override string ToString() => Value.ToString();
    }
}
