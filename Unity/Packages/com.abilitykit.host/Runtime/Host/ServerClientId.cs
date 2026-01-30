namespace AbilityKit.Ability.Host
{
    public readonly struct ServerClientId
    {
        public readonly string Value;

        public ServerClientId(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }
}
