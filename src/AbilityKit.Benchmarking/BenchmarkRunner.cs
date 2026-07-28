using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbilityKit.Benchmarking;

public static class BenchmarkRunner
{
    public static readonly IReadOnlyDictionary<string, string> MetricDefinitions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cpu"] = "Stopwatch time around scenario Execute only. Reported per logical operation with mean, median, P95, P99, max and throughput.",
            ["threadAllocation"] = "GC.GetAllocatedBytesForCurrentThread delta around scenario Execute only, reported per logical operation.",
            ["percentile"] = "Nearest-rank percentile over per-sample normalized values.",
            ["result"] = "Measurement is informational. It is not an approved budget or an enforced performance gate."
        };

    public static BenchmarkReport Run(
        string profile,
        IEnumerable<IBenchmarkScenario> scenarios,
        BenchmarkRunOptions options)
    {
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("Profile is required.", nameof(profile));
        if (scenarios == null)
            throw new ArgumentNullException(nameof(scenarios));

        options.Validate();
        var results = new List<BenchmarkCaseResult>();
        foreach (var scenario in scenarios)
            results.Add(RunScenario(scenario, options));

        if (results.Count == 0)
            throw new InvalidOperationException("No benchmark scenarios were selected.");

        return new BenchmarkReport
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Profile = profile,
            Environment = BenchmarkEnvironmentCapture.Capture(),
            Options = options,
            MetricDefinitions = MetricDefinitions,
            Results = results,
            Notes = new[]
            {
                "Initialization, iteration setup, validation, cleanup and JSON serialization are outside the measured interval.",
                "Allocation is current-thread managed allocation, not process, native or GPU memory.",
                "Compare results only when environment, build configuration and workload dimensions match."
            }
        };
    }

    public static BenchmarkCaseResult RunScenario(
        IBenchmarkScenario scenario,
        BenchmarkRunOptions options)
    {
        if (scenario == null)
            throw new ArgumentNullException(nameof(scenario));

        options.Validate();
        try
        {
            scenario.Setup();
            scenario.Validate();
            for (var i = 0; i < options.WarmupIterations; i++)
            {
                scenario.IterationSetup();
                RequireOperations(scenario, scenario.Execute(options.InvocationsPerIteration));
            }

            var samples = new BenchmarkSample[options.MeasurementIterations];
            for (var i = 0; i < samples.Length; i++)
            {
                scenario.IterationSetup();
                var allocationStart = GC.GetAllocatedBytesForCurrentThread();
                var timestampStart = Stopwatch.GetTimestamp();
                var operations = scenario.Execute(options.InvocationsPerIteration);
                var elapsedTicks = Stopwatch.GetTimestamp() - timestampStart;
                var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                RequireOperations(scenario, operations);

                samples[i] = new BenchmarkSample(
                    i,
                    operations,
                    elapsedTicks,
                    allocatedBytes,
                    TimestampTicksToNanoseconds(elapsedTicks) / operations,
                    allocatedBytes / (double)operations);
            }

            scenario.Validate();
            return new BenchmarkCaseResult(
                scenario.Descriptor,
                samples,
                Summarize(samples),
                scenario.GetDeterminismDigest(),
                "measurement");
        }
        finally
        {
            scenario.Cleanup();
        }
    }

    public static void WriteReport(string path, BenchmarkReport report)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Output path is required.", nameof(path));
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static BenchmarkSummary Summarize(IReadOnlyList<BenchmarkSample> samples)
    {
        var cpu = samples.Select(sample => sample.NanosecondsPerOperation).OrderBy(value => value).ToArray();
        var totalOperations = samples.Sum(sample => sample.Operations);
        var totalTicks = samples.Sum(sample => sample.ElapsedTimestampTicks);
        var totalAllocatedBytes = samples.Sum(sample => sample.ThreadAllocatedBytes);
        var elapsedSeconds = totalTicks / (double)Stopwatch.Frequency;

        return new BenchmarkSummary(
            samples.Count,
            totalOperations,
            TimestampTicksToNanoseconds(totalTicks) / totalOperations,
            Percentile(cpu, 0.50),
            Percentile(cpu, 0.95),
            Percentile(cpu, 0.99),
            cpu[^1],
            totalAllocatedBytes / (double)totalOperations,
            totalAllocatedBytes,
            elapsedSeconds > 0 ? totalOperations / elapsedSeconds : 0d);
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sortedValues.Count) - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static double TimestampTicksToNanoseconds(long ticks) =>
        ticks * 1_000_000_000d / Stopwatch.Frequency;

    private static void RequireOperations(IBenchmarkScenario scenario, long operations)
    {
        if (operations <= 0)
            throw new InvalidOperationException($"Benchmark '{scenario.Descriptor.Id}' completed no logical operations.");
    }
}
