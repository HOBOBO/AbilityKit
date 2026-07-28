using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.AoiLodBenchmarks;

public enum BenchmarkScenario
{
    Steady,
    Churn
}

public sealed record BenchmarkCase(int Entities, int Observers, BenchmarkScenario Scenario)
{
    public string Id => $"e{Entities}-o{Observers}-{Scenario.ToString().ToLowerInvariant()}";
}

public sealed record BenchmarkOptions
{
    public int Seed { get; init; } = 0x5A17;
    public int WarmupIterations { get; init; } = 3;
    public int MeasurementIterations { get; init; } = 8;
    public int TicksPerIteration { get; init; } = 8;
    public int EntityBudget { get; init; } = 128;
    public float VisibleRadius { get; init; } = 24f;
    public float BoundaryRadius { get; init; } = 28f;

    public static IReadOnlyList<BenchmarkCase> ExpandFullMatrix()
    {
        var result = new List<BenchmarkCase>(18);
        foreach (var entities in new[] { 100, 1000, 10000 })
        foreach (var observers in new[] { 1, 16, 64 })
        foreach (var scenario in Enum.GetValues<BenchmarkScenario>())
            result.Add(new BenchmarkCase(entities, observers, scenario));
        return result;
    }

    public static IReadOnlyList<BenchmarkCase> ExpandSmokeMatrix() =>
        new[]
        {
            new BenchmarkCase(100, 1, BenchmarkScenario.Steady),
            new BenchmarkCase(100, 1, BenchmarkScenario.Churn),
            new BenchmarkCase(1000, 16, BenchmarkScenario.Steady),
            new BenchmarkCase(1000, 16, BenchmarkScenario.Churn)
        };
}

public sealed record BenchmarkMetrics
{
    public required double MeanTickMilliseconds { get; init; }
    public required double MedianTickMilliseconds { get; init; }
    public required double NormalizedNanosecondsPerEntityObserver { get; init; }
    public required long ThreadAllocatedBytesPerTick { get; init; }
    public required double AllocatedBytesPerEntityObserver { get; init; }
    public double ExportMillisecondsPerTick { get; init; }
    public double SerializationMillisecondsPerTick { get; init; }
    public double ObservationMillisecondsPerTick { get; init; }
    public long ExportAllocatedBytesPerTick { get; init; }
    public long SerializationAllocatedBytesPerTick { get; init; }
    public long ObservationAllocatedBytesPerTick { get; init; }
    public required long PayloadBytesPerTick { get; init; }
    public required long EnterCount { get; init; }
    public required long LeaveCount { get; init; }
    public long ChangedCount => EnterCount + LeaveCount;
    public required int StarvedEntitiesAtEnd { get; init; }
    public required int MaxUnsentTicks { get; init; }
    public required ulong DeterminismDigest { get; init; }
}

public sealed record Thresholds
{
    public required string Tier { get; init; }
    public required double MaxNormalizedNanoseconds { get; init; }
    public required double MaxAllocatedBytesPerEntityObserver { get; init; }
    public required int MaxPayloadBytesPerObserverTick { get; init; }
    public required double MaxChurnFractionPerTick { get; init; }
    public required int MaxStarvedEntitiesPerObserver { get; init; }
    public required int MaxUnsentTicks { get; init; }
    public double CpuBaselineRegressionRatio { get; init; } = 1.75;

    public static Thresholds Smoke => new()
    {
        Tier = "ci-smoke",
        MaxNormalizedNanoseconds = 100_000,
        MaxAllocatedBytesPerEntityObserver = 4_096,
        MaxPayloadBytesPerObserverTick = 16_384,
        MaxChurnFractionPerTick = 0.35,
        MaxStarvedEntitiesPerObserver = 10_000,
        MaxUnsentTicks = 64,
        CpuBaselineRegressionRatio = 2.0
    };

    public static Thresholds Full => new()
    {
        Tier = "full-matrix-alert",
        MaxNormalizedNanoseconds = 75_000,
        MaxAllocatedBytesPerEntityObserver = 3_072,
        MaxPayloadBytesPerObserverTick = 16_384,
        MaxChurnFractionPerTick = 0.35,
        MaxStarvedEntitiesPerObserver = 10_000,
        MaxUnsentTicks = 64,
        CpuBaselineRegressionRatio = 1.75
    };
}

public sealed record ThresholdFailure(string Metric, double Actual, double Limit, string Message);

public sealed record BenchmarkResult(
    BenchmarkCase Case,
    BenchmarkMetrics Metrics,
    IReadOnlyList<ThresholdFailure> Failures)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record BenchmarkEnvironment(
    string MachineName,
    string OsDescription,
    string ProcessArchitecture,
    string FrameworkDescription,
    int ProcessorCount,
    bool ServerGc);

public sealed record BenchmarkReport
{
    public const string Schema = "abilitykit.shooter-aoi-lod-benchmark.v1";

    public string SchemaVersion { get; init; } = Schema;
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string Profile { get; init; }
    public required BenchmarkEnvironment Environment { get; init; }
    public required BenchmarkOptions Options { get; init; }
    public required Thresholds Thresholds { get; init; }
    public required IReadOnlyDictionary<string, string> MetricDefinitions { get; init; }
    public required IReadOnlyList<BenchmarkResult> Results { get; init; }
    public bool Passed => Results.All(result => result.Passed);
}

public static class BenchmarkThresholdEvaluator
{
    public static IReadOnlyList<ThresholdFailure> Evaluate(
        BenchmarkCase benchmarkCase,
        BenchmarkMetrics metrics,
        BenchmarkOptions options,
        Thresholds thresholds,
        BenchmarkMetrics? baseline = null)
    {
        var failures = new List<ThresholdFailure>();
        AddMaximum(failures, "cpu.normalizedNanoseconds", metrics.NormalizedNanosecondsPerEntityObserver, thresholds.MaxNormalizedNanoseconds);
        AddMaximum(failures, "allocation.bytesPerEntityObserver", metrics.AllocatedBytesPerEntityObserver, thresholds.MaxAllocatedBytesPerEntityObserver);
        AddMaximum(failures, "payload.bytesPerObserverTick", metrics.PayloadBytesPerTick / (double)benchmarkCase.Observers, thresholds.MaxPayloadBytesPerObserverTick);

        var measuredTicks = options.MeasurementIterations * options.TicksPerIteration;
        var churnFraction = metrics.ChangedCount / (double)Math.Max(1L, (long)benchmarkCase.Entities * benchmarkCase.Observers * measuredTicks);
        var deterministicChurnLimit = benchmarkCase.Scenario == BenchmarkScenario.Steady ? 0d : thresholds.MaxChurnFractionPerTick;
        AddMaximum(failures, "aoi.churnFractionPerTick", churnFraction, deterministicChurnLimit);
        var maxStarvedEntities = benchmarkCase.Scenario == BenchmarkScenario.Steady
            ? 0
            : thresholds.MaxStarvedEntitiesPerObserver;
        AddMaximum(failures, "starvation.entitiesPerObserver", metrics.StarvedEntitiesAtEnd / (double)benchmarkCase.Observers, maxStarvedEntities);
        AddMaximum(failures, "starvation.maxUnsentTicks", metrics.MaxUnsentTicks, thresholds.MaxUnsentTicks);

        if (baseline is not null && baseline.NormalizedNanosecondsPerEntityObserver > 0)
        {
            AddMaximum(
                failures,
                "cpu.baselineRegressionRatio",
                metrics.NormalizedNanosecondsPerEntityObserver / baseline.NormalizedNanosecondsPerEntityObserver,
                thresholds.CpuBaselineRegressionRatio);
        }

        return failures;
    }

    private static void AddMaximum(List<ThresholdFailure> failures, string metric, double actual, double limit)
    {
        if (actual <= limit)
            return;
        failures.Add(new ThresholdFailure(metric, actual, limit, $"{metric}={actual:F3} exceeds {limit:F3}."));
    }
}

public static class ShooterAoiLodBenchmarkRunner
{
    public static readonly IReadOnlyDictionary<string, string> MetricDefinitions = new Dictionary<string, string>
    {
        ["cpu"] = "Summed production time for one tick of all observers, including exporter AOI/LOD selection and ShooterPureStateSyncCodec serialization while excluding benchmark observation bookkeeping; median and mean are reported. normalizedNanoseconds divides mean tick nanoseconds by entities*observers.",
        ["threadAllocation"] = "GC.GetAllocatedBytesForCurrentThread deltas measured only around exporter and codec calls, averaged per tick; normalized value divides by entities*observers.",
        ["phaseBreakdown"] = "Per-tick time and thread allocation are split into export, serialization and benchmark observation phases. Observation is diagnostic overhead and is excluded from production totals.",
        ["payloadBytes"] = "Sum of serialized ShooterPureStateSyncCodec payload lengths for all observers, averaged per measured tick.",
        ["aoiChurn"] = "Complete geometric interest-set transitions independent of send budget: enter is outside-to-visible and leave is boundary-visible-to-outside; changedCount=enter+leave across measured ticks and observers.",
        ["starvation"] = "For entities geometrically eligible for an observer, consecutive ticks absent from that observer's budgeted payload. Reports eligible entities never sent by the end and maximum consecutive unsent ticks."
    };

    public static BenchmarkReport Run(
        string profile,
        IReadOnlyList<BenchmarkCase> cases,
        BenchmarkOptions options,
        Thresholds thresholds,
        IReadOnlyDictionary<string, BenchmarkMetrics>? baselines = null)
    {
        var results = new List<BenchmarkResult>(cases.Count);
        foreach (var benchmarkCase in cases)
        {
            var metrics = RunCase(benchmarkCase, options);
            BenchmarkMetrics? baseline = null;
            baselines?.TryGetValue(benchmarkCase.Id, out baseline);
            var failures = BenchmarkThresholdEvaluator.Evaluate(benchmarkCase, metrics, options, thresholds, baseline);
            results.Add(new BenchmarkResult(benchmarkCase, metrics, failures));
        }

        return new BenchmarkReport
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Profile = profile,
            Environment = CaptureEnvironment(),
            Options = options,
            Thresholds = thresholds,
            MetricDefinitions = MetricDefinitions,
            Results = results
        };
    }

    public static BenchmarkMetrics RunCase(BenchmarkCase benchmarkCase, BenchmarkOptions options)
    {
        Validate(benchmarkCase, options);
        var fixture = new BenchmarkFixture(benchmarkCase, options);
        for (var iteration = 0; iteration < options.WarmupIterations; iteration++)
            for (var tick = 0; tick < options.TicksPerIteration; tick++)
                fixture.ExecuteTick(measure: true);

        fixture.ResetMeasurements();
        var elapsedMilliseconds = new double[options.MeasurementIterations * options.TicksPerIteration];
        long exportAllocatedBytes = 0;
        long serializationAllocatedBytes = 0;
        long observationAllocatedBytes = 0;
        long exportTimestampTicks = 0;
        long serializationTimestampTicks = 0;
        long observationTimestampTicks = 0;
        long payloadBytes = 0;
        var sample = 0;
        for (var iteration = 0; iteration < options.MeasurementIterations; iteration++)
        {
            for (var tick = 0; tick < options.TicksPerIteration; tick++)
            {
                var measurement = fixture.ExecuteTick(measure: true);
                payloadBytes += measurement.PayloadBytes;
                exportAllocatedBytes += measurement.ExportAllocatedBytes;
                serializationAllocatedBytes += measurement.SerializationAllocatedBytes;
                observationAllocatedBytes += measurement.ObservationAllocatedBytes;
                exportTimestampTicks += measurement.ExportTimestampTicks;
                serializationTimestampTicks += measurement.SerializationTimestampTicks;
                observationTimestampTicks += measurement.ObservationTimestampTicks;
                elapsedMilliseconds[sample++] = TimestampTicksToMilliseconds(
                    measurement.ExportTimestampTicks + measurement.SerializationTimestampTicks);
            }
        }

        Array.Sort(elapsedMilliseconds);
        var measuredTicks = elapsedMilliseconds.Length;
        var mean = elapsedMilliseconds.Average();
        var median = measuredTicks % 2 == 0
            ? (elapsedMilliseconds[measuredTicks / 2 - 1] + elapsedMilliseconds[measuredTicks / 2]) / 2d
            : elapsedMilliseconds[measuredTicks / 2];
        var scale = (double)benchmarkCase.Entities * benchmarkCase.Observers;
        var productionAllocatedBytes = exportAllocatedBytes + serializationAllocatedBytes;
        return new BenchmarkMetrics
        {
            MeanTickMilliseconds = mean,
            MedianTickMilliseconds = median,
            NormalizedNanosecondsPerEntityObserver = mean * 1_000_000d / scale,
            ThreadAllocatedBytesPerTick = productionAllocatedBytes / measuredTicks,
            AllocatedBytesPerEntityObserver = productionAllocatedBytes / measuredTicks / scale,
            ExportMillisecondsPerTick = TimestampTicksToMilliseconds(exportTimestampTicks) / measuredTicks,
            SerializationMillisecondsPerTick = TimestampTicksToMilliseconds(serializationTimestampTicks) / measuredTicks,
            ObservationMillisecondsPerTick = TimestampTicksToMilliseconds(observationTimestampTicks) / measuredTicks,
            ExportAllocatedBytesPerTick = exportAllocatedBytes / measuredTicks,
            SerializationAllocatedBytesPerTick = serializationAllocatedBytes / measuredTicks,
            ObservationAllocatedBytesPerTick = observationAllocatedBytes / measuredTicks,
            PayloadBytesPerTick = payloadBytes / measuredTicks,
            EnterCount = fixture.EnterCount,
            LeaveCount = fixture.LeaveCount,
            StarvedEntitiesAtEnd = fixture.CountNeverSentEligible(),
            MaxUnsentTicks = fixture.MaxUnsentTicks,
            DeterminismDigest = fixture.Digest
        };
    }

    private static double TimestampTicksToMilliseconds(long ticks) =>
        ticks * 1000d / Stopwatch.Frequency;

    public static void WriteReport(string path, BenchmarkReport report)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    public static IReadOnlyDictionary<string, BenchmarkMetrics> ReadBaselines(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new Dictionary<string, BenchmarkMetrics>();
        var report = JsonSerializer.Deserialize<BenchmarkReport>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Baseline report is empty.");
        return report.Results.ToDictionary(result => result.Case.Id, result => result.Metrics, StringComparer.Ordinal);
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static BenchmarkEnvironment CaptureEnvironment() => new(
        Environment.MachineName,
        RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        RuntimeInformation.FrameworkDescription,
        Environment.ProcessorCount,
        System.Runtime.GCSettings.IsServerGC);

    private static void Validate(BenchmarkCase benchmarkCase, BenchmarkOptions options)
    {
        if (benchmarkCase.Entities <= 0 || benchmarkCase.Observers <= 0)
            throw new ArgumentOutOfRangeException(nameof(benchmarkCase));
        if (options.WarmupIterations < 0 || options.MeasurementIterations <= 0 || options.TicksPerIteration <= 0 || options.EntityBudget <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
    }

    private sealed class BenchmarkFixture
    {
        private readonly BenchmarkCase _case;
        private readonly BenchmarkOptions _options;
        private readonly MutableSnapshotPort _snapshotPort;
        private readonly ShooterBattleState _state;
        private readonly ShooterPureStateSnapshotExporter[] _exporters;
        private readonly AoiInterestSet[] _interestSets;
        private readonly ObserverState[] _observers;
        private readonly HashSet<AoiEntityKey>[] _geometricVisible;
        private readonly HashSet<AoiEntityKey>[] _geometricScratch;
        private readonly Dictionary<AoiEntityKey, int>[] _lastSentTick;
        private readonly ReusableMemoryPackSerializationBuffer _serializationBuffer = new();
        private int _tick;
        private int _measurementTick;

        public BenchmarkFixture(BenchmarkCase benchmarkCase, BenchmarkOptions options)
        {
            _case = benchmarkCase;
            _options = options;
            _snapshotPort = new MutableSnapshotPort(CreateSnapshot(benchmarkCase.Entities, options.Seed));
            _state = new ShooterBattleState(new UnusedEntityManager());
            _exporters = new ShooterPureStateSnapshotExporter[benchmarkCase.Observers];
            _interestSets = new AoiInterestSet[benchmarkCase.Observers];
            _observers = CreateObservers(benchmarkCase.Observers, options.Seed);
            _geometricVisible = new HashSet<AoiEntityKey>[benchmarkCase.Observers];
            _geometricScratch = new HashSet<AoiEntityKey>[benchmarkCase.Observers];
            _lastSentTick = new Dictionary<AoiEntityKey, int>[benchmarkCase.Observers];
            for (var i = 0; i < benchmarkCase.Observers; i++)
            {
                _exporters[i] = new ShooterPureStateSnapshotExporter(_state, _snapshotPort, ZeroHashProvider.Instance);
                _interestSets[i] = new AoiInterestSet();
                _geometricVisible[i] = new HashSet<AoiEntityKey>();
                _geometricScratch[i] = new HashSet<AoiEntityKey>();
                _lastSentTick[i] = new Dictionary<AoiEntityKey, int>();
            }
        }

        public long EnterCount { get; private set; }
        public long LeaveCount { get; private set; }
        public int MaxUnsentTicks { get; private set; }
        public ulong Digest { get; private set; } = 1469598103934665603UL;

        public void ResetMeasurements()
        {
            EnterCount = 0;
            LeaveCount = 0;
            MaxUnsentTicks = 0;
            Digest = 1469598103934665603UL;
            _measurementTick = 0;
            foreach (var sent in _lastSentTick)
                sent.Clear();
            for (var observerIndex = 0; observerIndex < _case.Observers; observerIndex++)
            {
                var observer = _observers[observerIndex];
                var scope = new ShooterPureStateInterestScope(
                    0,
                    observer.X,
                    observer.Y,
                    _options.VisibleRadius,
                    _options.BoundaryRadius,
                    _options.EntityBudget);
                RefreshGeometricVisible(observerIndex, scope);
            }
        }

        public TickMeasurement ExecuteTick(bool measure)
        {
            _tick++;
            if (measure)
                _measurementTick++;
            _state.CurrentFrame = _tick;
            if (_case.Scenario == BenchmarkScenario.Churn)
                MoveForChurn();
            _snapshotPort.SetFrame(_tick);

            var measurement = new TickMeasurement();
            for (var observerIndex = 0; observerIndex < _case.Observers; observerIndex++)
            {
                var observer = _observers[observerIndex];
                var scope = new ShooterPureStateInterestScope(
                    0,
                    observer.X,
                    observer.Y,
                    _options.VisibleRadius,
                    _options.BoundaryRadius,
                    _options.EntityBudget);
                ShooterPureStateSnapshotPayload payload;
                ArraySegment<byte> bytes;
                if (measure)
                {
                    var allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    var timestamp = Stopwatch.GetTimestamp();
                    payload = _exporters[observerIndex].ExportTransient(
                        1,
                        isFullBaseline: false,
                        settings: CreateSettings(),
                        interestScope: scope,
                        aoiInterestSet: _interestSets[observerIndex],
                        computeStateHash: false);
                    measurement.ExportTimestampTicks += Stopwatch.GetTimestamp() - timestamp;
                    measurement.ExportAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStart;

                    allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    timestamp = Stopwatch.GetTimestamp();
                    bytes = ShooterPureStateSyncCodec.SerializeTransientSegment(in payload, _serializationBuffer);
                    measurement.SerializationTimestampTicks += Stopwatch.GetTimestamp() - timestamp;
                    measurement.SerializationAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                    measurement.PayloadBytes += bytes.Count;

                    allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    timestamp = Stopwatch.GetTimestamp();
                    Observe(observerIndex, scope, payload, bytes.Count);
                    measurement.ObservationTimestampTicks += Stopwatch.GetTimestamp() - timestamp;
                    measurement.ObservationAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                }
                else
                {
                    payload = _exporters[observerIndex].ExportTransient(
                        1,
                        isFullBaseline: false,
                        settings: CreateSettings(),
                        interestScope: scope,
                        aoiInterestSet: _interestSets[observerIndex],
                        computeStateHash: false);
                    bytes = ShooterPureStateSyncCodec.SerializeTransientSegment(in payload, _serializationBuffer);
                    measurement.PayloadBytes += bytes.Count;
                }
            }
            return measurement;
        }

        public struct TickMeasurement
        {
            public int PayloadBytes;
            public long ExportTimestampTicks;
            public long SerializationTimestampTicks;
            public long ObservationTimestampTicks;
            public long ExportAllocatedBytes;
            public long SerializationAllocatedBytes;
            public long ObservationAllocatedBytes;
        }

        public int CountNeverSentEligible()
        {
            var count = 0;
            for (var observer = 0; observer < _case.Observers; observer++)
                foreach (var key in _geometricVisible[observer])
                    if (!_lastSentTick[observer].ContainsKey(key))
                        count++;
            return count;
        }

        private void Observe(int observerIndex, ShooterPureStateInterestScope scope, ShooterPureStateSnapshotPayload payload, int bytes)
        {
            var previous = _geometricVisible[observerIndex];
            var current = _geometricScratch[observerIndex];
            ComputeGeometricVisible(scope, previous, current);
            foreach (var key in current)
                if (!previous.Contains(key))
                    EnterCount++;
            foreach (var key in previous)
                if (!current.Contains(key))
                    LeaveCount++;
            _geometricVisible[observerIndex] = current;
            _geometricScratch[observerIndex] = previous;

            var sent = _lastSentTick[observerIndex];
            var entityCount = payload.EffectiveEntityCount;
            for (var i = 0; i < entityCount; i++)
            {
                var entity = payload.Entities[i];
                var key = new AoiEntityKey(entity.EntityKind, entity.EntityId);
                if (entity.DeltaKind != ShooterPureStateDeltaKinds.Despawn)
                    sent[key] = _measurementTick;
                Mix((uint)entity.EntityKind);
                Mix((uint)entity.EntityId);
                Mix((uint)entity.DeltaKind);
            }
            Mix((uint)bytes);

            foreach (var key in current)
            {
                var unsentTicks = sent.TryGetValue(key, out var lastTick) ? _measurementTick - lastTick : _measurementTick;
                MaxUnsentTicks = Math.Max(MaxUnsentTicks, unsentTicks);
            }
        }

        private void RefreshGeometricVisible(int observerIndex, ShooterPureStateInterestScope scope)
        {
            var previous = _geometricVisible[observerIndex];
            var current = _geometricScratch[observerIndex];
            ComputeGeometricVisible(scope, previous, current);
            _geometricVisible[observerIndex] = current;
            _geometricScratch[observerIndex] = previous;
        }

        private void ComputeGeometricVisible(
            ShooterPureStateInterestScope scope,
            HashSet<AoiEntityKey> previous,
            HashSet<AoiEntityKey> result)
        {
            result.Clear();
            foreach (var player in _snapshotPort.Snapshot.Players)
                AddIfVisible(result, previous, new AoiEntityKey(ShooterPackedEntityKinds.Player, player.PlayerId), player.X, player.Y, scope);
            foreach (var bullet in _snapshotPort.Snapshot.Bullets)
                AddIfVisible(result, previous, new AoiEntityKey(ShooterPackedEntityKinds.Projectile, bullet.BulletId), bullet.X, bullet.Y, scope);
        }

        private static void AddIfVisible(HashSet<AoiEntityKey> result, HashSet<AoiEntityKey> previous, AoiEntityKey key, float x, float y, ShooterPureStateInterestScope scope)
        {
            var dx = x - scope.CenterX;
            var dy = y - scope.CenterY;
            var radius = previous.Contains(key) ? scope.BoundaryRadius : scope.VisibleRadius;
            if (dx * dx + dy * dy <= radius * radius)
                result.Add(key);
        }

        private void MoveForChurn()
        {
            var phase = (_tick % 12) - 6;
            for (var i = 0; i < _snapshotPort.Snapshot.Bullets.Length; i += 7)
            {
                var bullet = _snapshotPort.Snapshot.Bullets[i];
                bullet.X += phase < 0 ? -7f : 7f;
                _snapshotPort.Snapshot.Bullets[i] = bullet;
            }
            for (var i = 0; i < _observers.Length; i++)
            {
                var observer = _observers[i];
                observer.X += ((i + _tick) & 1) == 0 ? 1.5f : -1.5f;
                _observers[i] = observer;
            }
        }

        private ShooterPureStateSyncSettings CreateSettings() => new(
            _case.Entities,
            _options.EntityBudget,
            60,
            1,
            15,
            3);

        private void Mix(uint value)
        {
            Digest ^= value;
            Digest *= 1099511628211UL;
        }

        private static ShooterStateSnapshotPayload CreateSnapshot(int entityCount, int seed)
        {
            var random = new Random(seed);
            var playerCount = Math.Min(64, Math.Max(1, entityCount / 10));
            var players = new ShooterPlayerSnapshot[playerCount];
            for (var i = 0; i < players.Length; i++)
                players[i] = new ShooterPlayerSnapshot(i + 1, NextCoordinate(random), NextCoordinate(random), 1f, 0f, 100, i, true);
            var bullets = new ShooterBulletSnapshot[entityCount - playerCount];
            for (var i = 0; i < bullets.Length; i++)
                bullets[i] = new ShooterBulletSnapshot(i + 1, (i % playerCount) + 1, NextCoordinate(random), NextCoordinate(random), 1f, 0f, 120);
            return new ShooterStateSnapshotPayload(0, players, bullets, Array.Empty<ShooterEventSnapshot>());
        }

        private static ObserverState[] CreateObservers(int count, int seed)
        {
            var random = new Random(seed ^ 0x31B7);
            var result = new ObserverState[count];
            for (var i = 0; i < count; i++)
                result[i] = new ObserverState(NextCoordinate(random) * 0.75f, NextCoordinate(random) * 0.75f);
            return result;
        }

        private static float NextCoordinate(Random random) => (float)(random.NextDouble() * 160d - 80d);
    }

    private struct ObserverState(float x, float y)
    {
        public float X = x;
        public float Y = y;
    }

    private sealed class MutableSnapshotPort(ShooterStateSnapshotPayload snapshot) : IShooterSnapshotReadPort
    {
        public ShooterStateSnapshotPayload Snapshot = snapshot;
        public ShooterStateSnapshotPayload GetSnapshot() => Snapshot;
        public void SetFrame(int frame) => Snapshot.Frame = frame;
    }

    private sealed class ZeroHashProvider : IShooterStateHashProvider
    {
        public static readonly ZeroHashProvider Instance = new();
        public uint ComputeStateHash() => 0;
    }

    private sealed class UnusedEntityManager : IShooterEntityManager
    {
        public ISveltoWorldContext SveltoContext => throw new NotSupportedException();
        public int MaxEntityCount => 0;
        public int PlayerCount => 0;
        public int ProjectileCount => 0;
        public int EnemyCount => 0;
        public IReadOnlyCollection<int> PlayerIds => Array.Empty<int>();
        public IReadOnlyCollection<int> ProjectileIds => Array.Empty<int>();
        public IReadOnlyCollection<int> EnemyIds => Array.Empty<int>();
        public void Clear() { }
        public void BeginStructuralChanges() { }
        public void EndStructuralChanges() { }
        public void SubmitStructuralChanges() { }
        public bool HasPlayer(int playerId) => false;
        public bool TryGetPlayer(int playerId, out ShooterSveltoPlayerComponent player) { player = default; return false; }
        public void AddPlayer(in ShooterSveltoPlayerComponent player) { }
        public void SetPlayer(in ShooterSveltoPlayerComponent player) { }
        public void RemovePlayer(int playerId) { }
        public bool HasProjectile(int bulletId) => false;
        public bool TryGetProjectile(int bulletId, out ShooterSveltoProjectileComponent projectile) { projectile = default; return false; }
        public void AddProjectile(in ShooterSveltoProjectileComponent projectile) { }
        public void SetProjectile(in ShooterSveltoProjectileComponent projectile) { }
        public void RemoveProjectile(int bulletId) { }
        public bool HasEnemy(int enemyId) => false;
        public bool TryGetEnemy(int enemyId, out ShooterSveltoTransformComponent transform, out ShooterSveltoHealthComponent health) { transform = default; health = default; return false; }
        public void AddEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health) { }
        public void SetEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health) { }
        public void RemoveEnemy(int enemyId) { }
    }
}
