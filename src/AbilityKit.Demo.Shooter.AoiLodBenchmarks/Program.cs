namespace AbilityKit.Demo.Shooter.AoiLodBenchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var arguments = Arguments.Parse(args);
            if (arguments.GetBool("pipeline", false))
            {
                return RunPipelineBenchmark(arguments);
            }

            var options = new BenchmarkOptions
            {
                Seed = arguments.GetInt("seed", 0x5A17),
                WarmupIterations = arguments.GetInt("warmup", arguments.Profile == "full" ? 5 : 2),
                MeasurementIterations = arguments.GetInt("measurement", arguments.Profile == "full" ? 12 : 4),
                TicksPerIteration = arguments.GetInt("ticks", 8),
                EntityBudget = arguments.GetInt("budget", 128)
            };
            var cases = arguments.CreateCases();
            var thresholds = arguments.Profile == "full" ? Thresholds.Full : Thresholds.Smoke;
            var baselines = ShooterAoiLodBenchmarkRunner.ReadBaselines(arguments.Get("baseline"));
            var report = ShooterAoiLodBenchmarkRunner.Run(arguments.Profile, cases, options, thresholds, baselines);
            var output = arguments.Get("output") ?? Path.Combine("artifacts", "shooter-aoi-lod", $"{arguments.Profile}.json");
            ShooterAoiLodBenchmarkRunner.WriteReport(output, report);

            Console.WriteLine($"Shooter AOI/LOD benchmark profile={arguments.Profile}, output={Path.GetFullPath(output)}");
            foreach (var result in report.Results)
            {
                var metrics = result.Metrics;
                Console.WriteLine(
                    $"{result.Case.Id}: {(result.Passed ? "PASS" : "FAIL")} " +
                    $"mean={metrics.MeanTickMilliseconds:F3}ms median={metrics.MedianTickMilliseconds:F3}ms " +
                    $"[export={metrics.ExportMillisecondsPerTick:F3}ms serialize={metrics.SerializationMillisecondsPerTick:F3}ms] " +
                    $"alloc={metrics.ThreadAllocatedBytesPerTick}B/tick " +
                    $"[export={metrics.ExportAllocatedBytesPerTick}B serialize={metrics.SerializationAllocatedBytesPerTick}B] " +
                    $"payload={metrics.PayloadBytesPerTick}B/tick " +
                    $"enter={metrics.EnterCount} leave={metrics.LeaveCount} " +
                    $"starved={metrics.StarvedEntitiesAtEnd} maxUnsent={metrics.MaxUnsentTicks}");
                foreach (var failure in result.Failures)
                    Console.Error.WriteLine($"  {failure.Message}");
            }
            return report.Passed ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunPipelineBenchmark(Arguments arguments)
    {
        var options = new SyncPipelineBenchmarkOptions
        {
            Entities = arguments.GetInt("entities", 1000),
            WarmupIterations = arguments.GetInt("warmup", 5),
            MeasurementIterations = arguments.GetInt("measurement", 64),
            FullBaseline = !string.Equals(arguments.Get("snapshot"), "delta", StringComparison.OrdinalIgnoreCase),
            ChangedEntityFraction = arguments.GetDouble("changed-fraction", 0.05),
            RefreshIntervalFrames = arguments.GetInt("refresh-frames", 60),
            MaxP99Milliseconds = arguments.GetDouble("max-p99-ms", 16.7),
            MaxAllocatedBytesPerIteration = arguments.GetLong("max-alloc-bytes", 4 * 1024 * 1024)
        };
        var report = ShooterSyncPipelineBenchmarkRunner.Run(options);
        var output = arguments.Get("output") ?? Path.Combine("artifacts", "shooter-sync-pipeline", options.FullBaseline ? "full.json" : "delta.json");
        ShooterSyncPipelineBenchmarkRunner.WriteReport(output, report);

        Console.WriteLine(
            $"Shooter sync pipeline: {(report.Passed ? "PASS" : "FAIL")} entities={options.Entities} " +
            $"snapshot={(options.FullBaseline ? "full" : "delta")} mean={report.Total.MeanMilliseconds:F3}ms " +
            $"p95={report.Total.P95Milliseconds:F3}ms p99={report.Total.P99Milliseconds:F3}ms " +
            $"alloc={report.Total.AllocatedBytesPerIteration}B payload={report.PayloadBytes}B " +
            $"entityDeltas={report.MeanEntityDeltas:F1} changed={report.MeanChangedEntities:F1} " +
            $"suppression={report.UnchangedSuppressionRatio:P1} maxAge={report.ObservedMaxEntityAgeFrames}f " +
            $"output={Path.GetFullPath(output)}");
        foreach (var phase in report.Phases)
        {
            Console.WriteLine(
                $"  {phase.Key}: mean={phase.Value.MeanMilliseconds:F3}ms " +
                $"p95={phase.Value.P95Milliseconds:F3}ms p99={phase.Value.P99Milliseconds:F3}ms " +
                $"alloc={phase.Value.AllocatedBytesPerIteration}B");
        }
        foreach (var failure in report.Failures)
        {
            Console.Error.WriteLine($"  {failure}");
        }

        return report.Passed ? 0 : 2;
    }

    private sealed class Arguments
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public string Profile => Get("profile")?.ToLowerInvariant() switch
        {
            "full" => "full",
            "smoke" or null => "smoke",
            var value => throw new ArgumentException($"Unsupported profile '{value}'.")
        };

        public static Arguments Parse(string[] args)
        {
            var result = new Arguments();
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unexpected argument '{args[i]}'.");
                var key = args[i][2..];
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Missing value for '--{key}'.");
                result._values[key] = args[++i];
            }
            return result;
        }

        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public int GetInt(string key, int fallback) => Get(key) is { } value
            ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

        public long GetLong(string key, long fallback) => Get(key) is { } value
            ? long.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

        public double GetDouble(string key, double fallback) => Get(key) is { } value
            ? double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

        public bool GetBool(string key, bool fallback) => Get(key) is { } value
            ? bool.Parse(value)
            : fallback;

        public IReadOnlyList<BenchmarkCase> CreateCases()
        {
            var entityValue = Get("entities");
            var observerValue = Get("observers");
            var scenarioValue = Get("scenario");
            if (entityValue is null && observerValue is null && scenarioValue is null)
                return Profile == "full" ? BenchmarkOptions.ExpandFullMatrix() : BenchmarkOptions.ExpandSmokeMatrix();
            if (entityValue is null || observerValue is null)
                throw new ArgumentException("--entities and --observers must be specified together.");

            var entities = int.Parse(entityValue, System.Globalization.CultureInfo.InvariantCulture);
            var observers = int.Parse(observerValue, System.Globalization.CultureInfo.InvariantCulture);
            if (scenarioValue is null || scenarioValue.Equals("both", StringComparison.OrdinalIgnoreCase))
                return new[]
                {
                    new BenchmarkCase(entities, observers, BenchmarkScenario.Steady),
                    new BenchmarkCase(entities, observers, BenchmarkScenario.Churn)
                };
            if (!Enum.TryParse<BenchmarkScenario>(scenarioValue, true, out var scenario))
                throw new ArgumentException($"Unsupported scenario '{scenarioValue}'.");
            return new[] { new BenchmarkCase(entities, observers, scenario) };
        }
    }
}
