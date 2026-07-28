using System.Runtime.InteropServices;

namespace AbilityKit.Benchmarking;

public sealed record BenchmarkRunOptions(
    int WarmupIterations,
    int MeasurementIterations,
    int InvocationsPerIteration)
{
    public void Validate()
    {
        if (WarmupIterations < 0)
            throw new ArgumentOutOfRangeException(nameof(WarmupIterations));
        if (MeasurementIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(MeasurementIterations));
        if (InvocationsPerIteration <= 0)
            throw new ArgumentOutOfRangeException(nameof(InvocationsPerIteration));
    }
}

public sealed record BenchmarkDescriptor(
    string Id,
    string Module,
    string OperationUnit,
    IReadOnlyDictionary<string, string> Workload);

public interface IBenchmarkScenario
{
    BenchmarkDescriptor Descriptor { get; }

    void Setup();

    void IterationSetup();

    long Execute(int invocationCount);

    void Validate();

    string GetDeterminismDigest();

    void Cleanup();
}

public abstract class BenchmarkScenarioBase : IBenchmarkScenario
{
    public abstract BenchmarkDescriptor Descriptor { get; }

    public virtual void Setup()
    {
    }

    public virtual void IterationSetup()
    {
    }

    public abstract long Execute(int invocationCount);

    public virtual void Validate()
    {
    }

    public virtual string GetDeterminismDigest() => string.Empty;

    public virtual void Cleanup()
    {
    }
}

public sealed record BenchmarkSample(
    int Index,
    long Operations,
    long ElapsedTimestampTicks,
    long ThreadAllocatedBytes,
    double NanosecondsPerOperation,
    double AllocatedBytesPerOperation);

public sealed record BenchmarkSummary(
    int SampleCount,
    long TotalOperations,
    double MeanNanosecondsPerOperation,
    double MedianNanosecondsPerOperation,
    double P95NanosecondsPerOperation,
    double P99NanosecondsPerOperation,
    double MaxNanosecondsPerOperation,
    double MeanAllocatedBytesPerOperation,
    long TotalThreadAllocatedBytes,
    double OperationsPerSecond);

public sealed record BenchmarkCaseResult(
    BenchmarkDescriptor Descriptor,
    IReadOnlyList<BenchmarkSample> Samples,
    BenchmarkSummary Summary,
    string DeterminismDigest,
    string Result);

public sealed record BenchmarkEnvironment(
    string MachineName,
    string OsDescription,
    string ProcessArchitecture,
    string FrameworkDescription,
    int ProcessorCount,
    bool ServerGc,
    string BuildConfiguration,
    string Commit);

public sealed record BenchmarkReport
{
    public const string Schema = "abilitykit.runtime-benchmark.v1";

    public string SchemaVersion { get; init; } = Schema;

    public required DateTimeOffset TimestampUtc { get; init; }

    public required string Profile { get; init; }

    public required BenchmarkEnvironment Environment { get; init; }

    public required BenchmarkRunOptions Options { get; init; }

    public required IReadOnlyDictionary<string, string> MetricDefinitions { get; init; }

    public required IReadOnlyList<BenchmarkCaseResult> Results { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}

internal static class BenchmarkEnvironmentCapture
{
    public static BenchmarkEnvironment Capture()
    {
        var configuration = IsDebugBuild() ? "Debug" : "Release";
        var commit = Environment.GetEnvironmentVariable("GITHUB_SHA")
            ?? Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION")
            ?? "unknown";

        return new BenchmarkEnvironment(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,
            System.Runtime.GCSettings.IsServerGC,
            configuration,
            commit);
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
