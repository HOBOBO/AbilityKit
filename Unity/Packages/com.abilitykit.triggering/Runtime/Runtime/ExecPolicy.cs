namespace AbilityKit.Triggering.Runtime
{
    public readonly struct ExecPolicy
    {
        public readonly bool RequireDeterministic;

        public ExecPolicy(bool requireDeterministic)
        {
            RequireDeterministic = requireDeterministic;
        }

        public static ExecPolicy Default => default;
        public static ExecPolicy DeterministicOnly => new ExecPolicy(true);
    }
}
