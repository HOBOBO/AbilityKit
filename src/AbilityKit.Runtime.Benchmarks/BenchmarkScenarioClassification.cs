namespace AbilityKit.Runtime.Benchmarks;

public static class BenchmarkWorkloadDimensions
{
    public const string Scope = "scope";
}

public static class BenchmarkScenarioScopes
{
    public const string Package = "package";

    public const string Capability = "capability";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Package,
        Capability
    };
}
