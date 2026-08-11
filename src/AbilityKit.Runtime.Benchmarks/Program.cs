using AbilityKit.Benchmarking;

namespace AbilityKit.Runtime.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var arguments = BenchmarkArguments.Parse(args);
            var options = arguments.CreateOptions();
            var scenarios = BenchmarkScenarioCatalog.Create(arguments.Profile)
                .Where(arguments.Matches)
                .ToArray();
            var report = BenchmarkRunner.Run(arguments.Profile, scenarios, options);
            var output = arguments.Output
                ?? Path.Combine("artifacts", "runtime-benchmarks", $"{arguments.Profile}.json");
            BenchmarkRunner.WriteReport(output, report);

            Console.WriteLine(
                $"AbilityKit runtime benchmarks profile={arguments.Profile}, " +
                $"cases={report.Results.Count}, output={Path.GetFullPath(output)}");
            foreach (var result in report.Results)
            {
                var summary = result.Summary;
                Console.WriteLine(
                    $"{result.Descriptor.Id}: " +
                    $"median={summary.MedianNanosecondsPerOperation:F2}ns/{result.Descriptor.OperationUnit} " +
                    $"p95={summary.P95NanosecondsPerOperation:F2}ns " +
                    $"p99={summary.P99NanosecondsPerOperation:F2}ns " +
                    $"alloc={summary.MeanAllocatedBytesPerOperation:F3}B/{result.Descriptor.OperationUnit} " +
                    $"throughput={summary.OperationsPerSecond:F0}/s");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

public sealed record BenchmarkArguments(
    string Profile,
    string Module,
    string Scope,
    string? CaseFilter,
    string? Output,
    int? Warmup,
    int? Measurement,
    int? Invocations)
{
    private static readonly HashSet<string> SupportedArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile",
        "module",
        "scope",
        "case",
        "output",
        "warmup",
        "measurement",
        "invocations"
    };

    public static BenchmarkArguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{token}'.");
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Argument '{token}' requires a value.");
            var name = token[2..];
            if (!SupportedArguments.Contains(name))
                throw new ArgumentException($"Unknown argument '{token}'.");
            values[name] = args[++i];
        }

        var profile = Get(values, "profile") ?? "smoke";
        if (profile is not ("smoke" or "full"))
            throw new ArgumentException("Profile must be 'smoke' or 'full'.");
        var module = Get(values, "module") ?? "all";
        if (!BenchmarkScenarioCatalog.IsSupportedModule(module))
        {
            var supported = string.Join("', '", BenchmarkScenarioCatalog.Modules.Order(StringComparer.OrdinalIgnoreCase));
            throw new ArgumentException($"Module must be 'all', '{supported}'.");
        }

        var scope = Get(values, "scope") ?? "all";
        if (!scope.Equals("all", StringComparison.OrdinalIgnoreCase)
            && !BenchmarkScenarioScopes.All.Contains(scope))
        {
            throw new ArgumentException("Scope must be 'all', 'package' or 'capability'.");
        }

        return new BenchmarkArguments(
            profile,
            module,
            scope,
            Get(values, "case"),
            Get(values, "output"),
            GetInt(values, "warmup"),
            GetInt(values, "measurement"),
            GetInt(values, "invocations"));
    }

    public BenchmarkRunOptions CreateOptions()
    {
        var defaults = Profile == "full"
            ? new BenchmarkRunOptions(5, 20, 50)
            : new BenchmarkRunOptions(2, 8, 10);
        return defaults with
        {
            WarmupIterations = Warmup ?? defaults.WarmupIterations,
            MeasurementIterations = Measurement ?? defaults.MeasurementIterations,
            InvocationsPerIteration = Invocations ?? defaults.InvocationsPerIteration
        };
    }

    public bool Matches(IBenchmarkScenario scenario)
    {
        var moduleMatches = Module.Equals("all", StringComparison.OrdinalIgnoreCase)
            || scenario.Descriptor.Module.Equals(Module, StringComparison.OrdinalIgnoreCase);
        var scopeMatches = Scope.Equals("all", StringComparison.OrdinalIgnoreCase)
            || (scenario.Descriptor.Workload.TryGetValue(BenchmarkWorkloadDimensions.Scope, out var scope)
                && scope.Equals(Scope, StringComparison.OrdinalIgnoreCase));
        var caseMatches = string.IsNullOrWhiteSpace(CaseFilter)
            || scenario.Descriptor.Id.Contains(CaseFilter, StringComparison.OrdinalIgnoreCase);
        return moduleMatches && scopeMatches && caseMatches;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int? GetInt(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value)
            ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
}
