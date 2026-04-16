namespace AbilityKit.Triggering.Runtime
{
    public readonly struct ExecPolicy
    {
        public readonly bool RequireDeterministic;
        public readonly float DeltaTimeMs;
        public readonly float TotalTimeMs;

        public ExecPolicy(bool requireDeterministic, float deltaTimeMs = 0, float totalTimeMs = 0)
        {
            RequireDeterministic = requireDeterministic;
            DeltaTimeMs = deltaTimeMs;
            TotalTimeMs = totalTimeMs;
        }

        public static ExecPolicy Default => default;
        public static ExecPolicy DeterministicOnly => new ExecPolicy(true);
    }
}
