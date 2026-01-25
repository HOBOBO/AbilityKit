namespace AbilityKit.Ability.Server
{
    public readonly struct PlayerId
    {
        public readonly string Value;

        public PlayerId(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }
}
