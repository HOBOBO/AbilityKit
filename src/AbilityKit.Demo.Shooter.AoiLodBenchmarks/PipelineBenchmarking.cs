using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Serialization;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.AoiLodBenchmarks;

public sealed record SyncPipelineBenchmarkOptions
{
    public int Entities { get; init; } = 1000;
    public int WarmupIterations { get; init; } = 5;
    public int MeasurementIterations { get; init; } = 64;
    public bool FullBaseline { get; init; } = true;
    public double MaxP99Milliseconds { get; init; } = 16.7;
    public long MaxAllocatedBytesPerIteration { get; init; } = 4 * 1024 * 1024;
}

public sealed record SyncPipelinePhaseMetrics
{
    public required double MeanMilliseconds { get; init; }
    public required double P50Milliseconds { get; init; }
    public required double P95Milliseconds { get; init; }
    public required double P99Milliseconds { get; init; }
    public required long AllocatedBytesPerIteration { get; init; }
}

public sealed record SyncPipelineBenchmarkReport
{
    public const string Schema = "abilitykit.shooter-sync-pipeline-benchmark.v1";

    public string SchemaVersion { get; init; } = Schema;
    public required DateTimeOffset TimestampUtc { get; init; }
    public required BenchmarkEnvironment Environment { get; init; }
    public required SyncPipelineBenchmarkOptions Options { get; init; }
    public required IReadOnlyDictionary<string, string> MetricDefinitions { get; init; }
    public required IReadOnlyDictionary<string, SyncPipelinePhaseMetrics> Phases { get; init; }
    public required SyncPipelinePhaseMetrics Total { get; init; }
    public required int PayloadBytes { get; init; }
    public required int ProjectedEntities { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
    public bool Passed => Failures.Count == 0;
}

public static class ShooterSyncPipelineBenchmarkRunner
{
    private static readonly string[] PhaseNames = { "export", "encode", "decode", "map", "projection", "release" };

    public static SyncPipelineBenchmarkReport Run(SyncPipelineBenchmarkOptions options)
    {
        if (options.Entities <= 0) throw new ArgumentOutOfRangeException(nameof(options.Entities));
        if (options.WarmupIterations < 0) throw new ArgumentOutOfRangeException(nameof(options.WarmupIterations));
        if (options.MeasurementIterations <= 0) throw new ArgumentOutOfRangeException(nameof(options.MeasurementIterations));

        var fixture = new Fixture(options);
        for (var i = 0; i < options.WarmupIterations; i++)
        {
            fixture.Execute(measure: false);
        }

        var samples = PhaseNames.ToDictionary(name => name, _ => new PhaseSamples(options.MeasurementIterations), StringComparer.Ordinal);
        var totals = new PhaseSamples(options.MeasurementIterations);
        for (var i = 0; i < options.MeasurementIterations; i++)
        {
            var measurement = fixture.Execute(measure: true);
            long totalTicks = 0;
            long totalAllocatedBytes = 0;
            foreach (var phaseName in PhaseNames)
            {
                var phase = measurement.Phases[phaseName];
                samples[phaseName].Add(phase.TimestampTicks, phase.AllocatedBytes);
                totalTicks += phase.TimestampTicks;
                totalAllocatedBytes += phase.AllocatedBytes;
            }

            totals.Add(totalTicks, totalAllocatedBytes);
        }

        var phaseMetrics = samples.ToDictionary(pair => pair.Key, pair => pair.Value.ToMetrics(), StringComparer.Ordinal);
        var totalMetrics = totals.ToMetrics();
        var failures = new List<string>();
        if (totalMetrics.P99Milliseconds > options.MaxP99Milliseconds)
        {
            failures.Add($"total.p99={totalMetrics.P99Milliseconds:F3}ms exceeds {options.MaxP99Milliseconds:F3}ms.");
        }
        if (totalMetrics.AllocatedBytesPerIteration > options.MaxAllocatedBytesPerIteration)
        {
            failures.Add($"total.alloc={totalMetrics.AllocatedBytesPerIteration}B exceeds {options.MaxAllocatedBytesPerIteration}B.");
        }
        if (fixture.ProjectedEntities != options.Entities)
        {
            failures.Add($"projection.entities={fixture.ProjectedEntities} expected {options.Entities}.");
        }

        return new SyncPipelineBenchmarkReport
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Environment = new BenchmarkEnvironment(
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription,
                Environment.ProcessorCount,
                System.Runtime.GCSettings.IsServerGC),
            Options = options,
            MetricDefinitions = new Dictionary<string, string>
            {
                ["scope"] = "Headless Pure State path: ECS export, codec encode/decode, ViewModel mapping, dictionary projection and pooled-batch release. Unity rendering is excluded.",
                ["latency"] = "Per-iteration wall-clock stage latency with mean, P50, P95 and P99.",
                ["allocation"] = "GC.GetAllocatedBytesForCurrentThread delta averaged per iteration for each stage."
            },
            Phases = phaseMetrics,
            Total = totalMetrics,
            PayloadBytes = fixture.PayloadBytes,
            ProjectedEntities = fixture.ProjectedEntities,
            Failures = failures
        };
    }

    public static void WriteReport(string path, SyncPipelineBenchmarkReport report)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, ShooterAoiLodBenchmarkRunner.JsonOptions));
    }

    private sealed class Fixture
    {
        private readonly SyncPipelineBenchmarkOptions _options;
        private readonly ShooterBattleState _state;
        private readonly ShooterPureStateSnapshotExporter _exporter;
        private readonly ReusableMemoryPackSerializationBuffer _serializationBuffer = new();
        private readonly ShooterSnapshotViewModelMapper _mapper = new();
        private readonly ShooterSnapshotViewProjection _projection = new();
        private readonly ShooterPureStateSyncSettings _settings;
        private readonly ShooterPureStateSyncDecodeBuffer _decodeBuffer = new();

        public Fixture(SyncPipelineBenchmarkOptions options)
        {
            _options = options;
            var context = new SveltoWorldContext();
            var entities = new ShooterEntityManager(context, new ShooterEntityLimitOptions(options.Entities + 16));
            _state = new ShooterBattleState(entities) { CurrentFrame = 1 };
            entities.BeginStructuralChanges();
            try
            {
                var width = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(options.Entities)));
                for (var i = 0; i < options.Entities; i++)
                {
                    var transform = new ShooterSveltoTransformComponent
                    {
                        X = i % width,
                        Y = i / width,
                        DirectionX = 1f
                    };
                    var health = new ShooterSveltoHealthComponent { Current = 10, Max = 10, Alive = 1 };
                    var navigation = new ShooterSveltoNavigationComponent { VelocityX = 0.25f, MaxSpeed = 1f, Radius = 0.25f };
                    entities.AddEnemy(100_000 + i, in transform, in health, in navigation);
                }
            }
            finally
            {
                entities.EndStructuralChanges();
            }

            _settings = new ShooterPureStateSyncSettings(
                maxEntityCount: options.Entities,
                activeSyncBudget: options.Entities,
                baselineIntervalFrames: 60,
                deltaIntervalFrames: 1,
                lowFrequencyIntervalFrames: 10,
                interpolationDelayFrames: 3);
            _exporter = new ShooterPureStateSnapshotExporter(_state, EmptySnapshotReadPort.Instance, ZeroStateHashProvider.Instance, entities);
        }

        public int PayloadBytes { get; private set; }
        public int ProjectedEntities => _projection.Store.EntityCount;

        public PipelineMeasurement Execute(bool measure)
        {
            _state.CurrentFrame++;
            var result = new PipelineMeasurement();
            ShooterPureStateSnapshotPayload exported = default;
            byte[] encoded = Array.Empty<byte>();
            ShooterPureStateSnapshotPayload decoded = default;
            ShooterSnapshotViewBatch batch = default;

            Measure(result, "export", measure, () =>
            {
                exported = _exporter.ExportTransient(
                    worldId: 1,
                    isFullBaseline: _options.FullBaseline,
                    settings: _settings,
                    computeStateHash: false);
            });
            Measure(result, "encode", measure, () => encoded = ShooterPureStateSyncCodec.SerializeTransient(in exported, _serializationBuffer));
            PayloadBytes = encoded.Length;
            Measure(result, "decode", measure, () =>
            {
                decoded = _decodeBuffer.Decode(encoded.AsSpan());
            });
            Measure(result, "map", measure, () => batch = _mapper.Map(in decoded));
            Measure(result, "projection", measure, () => _projection.Apply(in batch));
            Measure(result, "release", measure, batch.ReleasePooledResources);
            return result;
        }

        private static void Measure(PipelineMeasurement result, string phase, bool measure, Action action)
        {
            if (!measure)
            {
                action();
                return;
            }

            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var timestampStart = Stopwatch.GetTimestamp();
            action();
            result.Phases[phase] = new PhaseMeasurement(
                Stopwatch.GetTimestamp() - timestampStart,
                GC.GetAllocatedBytesForCurrentThread() - allocationStart);
        }
    }

    private sealed class PipelineMeasurement
    {
        public Dictionary<string, PhaseMeasurement> Phases { get; } = new(StringComparer.Ordinal);
    }

    private readonly record struct PhaseMeasurement(long TimestampTicks, long AllocatedBytes);

    private sealed class PhaseSamples
    {
        private readonly long[] _timestampTicks;
        private long _allocatedBytes;
        private int _count;

        public PhaseSamples(int capacity)
        {
            _timestampTicks = new long[capacity];
        }

        public void Add(long timestampTicks, long allocatedBytes)
        {
            _timestampTicks[_count++] = timestampTicks;
            _allocatedBytes += allocatedBytes;
        }

        public SyncPipelinePhaseMetrics ToMetrics()
        {
            var sorted = _timestampTicks.AsSpan(0, _count).ToArray();
            Array.Sort(sorted);
            return new SyncPipelinePhaseMetrics
            {
                MeanMilliseconds = sorted.Average() * 1000d / Stopwatch.Frequency,
                P50Milliseconds = Percentile(sorted, 0.50),
                P95Milliseconds = Percentile(sorted, 0.95),
                P99Milliseconds = Percentile(sorted, 0.99),
                AllocatedBytesPerIteration = _allocatedBytes / Math.Max(1, _count)
            };
        }

        private static double Percentile(long[] sorted, double percentile)
        {
            if (sorted.Length == 0) return 0d;
            var index = Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1);
            return sorted[index] * 1000d / Stopwatch.Frequency;
        }
    }

    private sealed class EmptySnapshotReadPort : IShooterSnapshotReadPort
    {
        public static EmptySnapshotReadPort Instance { get; } = new();
        public ShooterStateSnapshotPayload GetSnapshot() => default;
    }

    private sealed class ZeroStateHashProvider : IShooterStateHashProvider
    {
        public static ZeroStateHashProvider Instance { get; } = new();
        public uint ComputeStateHash() => 0;
    }
}
