using System.Diagnostics;

namespace AbilityKit.Orleans.Grains.Battle;

internal enum BattleServerStage
{
    InputSubmit,
    WorldTick,
    ReliableEvents,
    SnapshotBuild,
    SnapshotDelivery,
    ShooterFrameBegin,
    ShooterBotAi,
    ShooterEnemyWaveSpawn,
    ShooterEnemyMovementIntent,
    ShooterRvoSolve,
    ShooterRvoNeighborCollect,
    ShooterRvoAcceleratedValidation,
    ShooterRvoOrcaSolve,
    ShooterEnemyMovementIntegration,
    ShooterSimulation,
    ShooterEnemyLifecycleCleanup,
    ShooterEnemyWaveAttack,
    ShooterMatchState
}

internal readonly record struct BattleStageTimingSummary(
    long Count,
    double MeanMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds);

internal readonly record struct BattleServerPerformanceSnapshot(
    int FirstFrame,
    int LastFrame,
    int TickCount,
    int TargetTickRate,
    double AchievedTickRate,
    BattleStageTimingSummary TickInterval,
    BattleStageTimingSummary TickTotal,
    BattleStageTimingSummary InputSubmit,
    BattleStageTimingSummary WorldTick,
    BattleStageTimingSummary ReliableEvents,
    BattleStageTimingSummary SnapshotBuild,
    BattleStageTimingSummary SnapshotDelivery,
    long SnapshotQueuedBehindInFlightCount)
{
    public IReadOnlyDictionary<string, BattleStageTimingSummary>? ShooterStages { get; init; }
}

/// <summary>
/// Fixed-memory timing window for the authoritative battle clock. Histograms are
/// deliberately coarse: their purpose is to identify the stage responsible for
/// a frame-time order-of-magnitude change without allocating per tick.
/// </summary>
internal sealed class BattleServerPerformanceDiagnostics
{
    private const int DefaultWindowTickCount = 150;

    private readonly int _windowTickCount;
    private readonly BattleTimingHistogram _tickIntervals = new();
    private readonly BattleTimingHistogram _tickTotals = new();
    private readonly BattleTimingHistogram _inputSubmit = new();
    private readonly BattleTimingHistogram _worldTick = new();
    private readonly BattleTimingHistogram _reliableEvents = new();
    private readonly BattleTimingHistogram _snapshotBuild = new();
    private readonly BattleTimingHistogram _snapshotDelivery = new();
    private readonly BattleTimingHistogram[] _shooterStages = CreateShooterStageHistograms();

    private int _firstFrame;
    private int _lastFrame;
    private int _tickCount;
    private long _firstTickTimestamp;
    private long _lastTickTimestamp;
    private long _currentTickStartedAt;
    private long _snapshotQueuedBehindInFlightCount;

    public BattleServerPerformanceDiagnostics(int windowTickCount = DefaultWindowTickCount)
    {
        _windowTickCount = Math.Max(2, windowTickCount);
    }

    public void BeginTick(int frame, long timestamp)
    {
        if (_tickCount == 0)
        {
            _firstFrame = frame;
            _firstTickTimestamp = timestamp;
        }
        else if (_lastTickTimestamp > 0 && timestamp >= _lastTickTimestamp)
        {
            _tickIntervals.RecordTimestampDelta(_lastTickTimestamp, timestamp);
        }

        _lastFrame = frame;
        _lastTickTimestamp = timestamp;
        _currentTickStartedAt = timestamp;
    }

    public bool CompleteTick(long timestamp, int targetTickRate, out BattleServerPerformanceSnapshot snapshot)
    {
        if (_currentTickStartedAt > 0 && timestamp >= _currentTickStartedAt)
        {
            _tickTotals.RecordTimestampDelta(_currentTickStartedAt, timestamp);
        }

        _currentTickStartedAt = 0;
        _tickCount++;
        if (_tickCount < _windowTickCount)
        {
            snapshot = default;
            return false;
        }

        var elapsedTicks = _lastTickTimestamp - _firstTickTimestamp;
        var achievedTickRate = elapsedTicks > 0 && _tickCount > 1
            ? (_tickCount - 1) * (double)Stopwatch.Frequency / elapsedTicks
            : 0d;

        snapshot = new BattleServerPerformanceSnapshot(
            _firstFrame,
            _lastFrame,
            _tickCount,
            targetTickRate,
            achievedTickRate,
            _tickIntervals.CreateSummary(),
            _tickTotals.CreateSummary(),
            _inputSubmit.CreateSummary(),
            _worldTick.CreateSummary(),
            _reliableEvents.CreateSummary(),
            _snapshotBuild.CreateSummary(),
            _snapshotDelivery.CreateSummary(),
            _snapshotQueuedBehindInFlightCount)
        {
            ShooterStages = CreateShooterStageSummaries()
        };

        ResetWindow();
        return true;
    }

    public void RecordStage(BattleServerStage stage, long startedAt, long completedAt)
    {
        if (startedAt <= 0 || completedAt < startedAt)
        {
            return;
        }

        ResolveHistogram(stage).RecordTimestampDelta(startedAt, completedAt);
    }

    internal void RecordStageMilliseconds(BattleServerStage stage, double milliseconds)
    {
        ResolveHistogram(stage).RecordMilliseconds(milliseconds);
    }

    internal void RecordShooterStageMilliseconds(string stageName, double milliseconds)
    {
        if (!TryResolveShooterStage(stageName, out var stage))
        {
            return;
        }

        _shooterStages[(int)stage - (int)BattleServerStage.ShooterFrameBegin].RecordMilliseconds(milliseconds);
    }

    public void RecordSnapshotQueuedBehindInFlight()
    {
        _snapshotQueuedBehindInFlightCount++;
    }

    public void Clear()
    {
        ResetWindow();
    }

    private BattleTimingHistogram ResolveHistogram(BattleServerStage stage)
    {
        return stage switch
        {
            BattleServerStage.InputSubmit => _inputSubmit,
            BattleServerStage.WorldTick => _worldTick,
            BattleServerStage.ReliableEvents => _reliableEvents,
            BattleServerStage.SnapshotBuild => _snapshotBuild,
            BattleServerStage.SnapshotDelivery => _snapshotDelivery,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }

    private void ResetWindow()
    {
        _tickIntervals.Clear();
        _tickTotals.Clear();
        _inputSubmit.Clear();
        _worldTick.Clear();
        _reliableEvents.Clear();
        _snapshotBuild.Clear();
        _snapshotDelivery.Clear();
        for (var i = 0; i < _shooterStages.Length; i++)
        {
            _shooterStages[i].Clear();
        }
        _firstFrame = 0;
        _lastFrame = 0;
        _tickCount = 0;
        _firstTickTimestamp = 0;
        _lastTickTimestamp = 0;
        _currentTickStartedAt = 0;
        _snapshotQueuedBehindInFlightCount = 0;
    }

    private IReadOnlyDictionary<string, BattleStageTimingSummary> CreateShooterStageSummaries()
    {
        var summaries = new Dictionary<string, BattleStageTimingSummary>(StringComparer.Ordinal);
        for (var i = 0; i < _shooterStages.Length; i++)
        {
            var summary = _shooterStages[i].CreateSummary();
            if (summary.Count > 0)
            {
                summaries[ResolveShooterStageName(i)] = summary;
            }
        }

        return summaries;
    }

    private static BattleTimingHistogram[] CreateShooterStageHistograms()
    {
        var count = (int)BattleServerStage.ShooterMatchState - (int)BattleServerStage.ShooterFrameBegin + 1;
        var histograms = new BattleTimingHistogram[count];
        for (var i = 0; i < histograms.Length; i++)
        {
            histograms[i] = new BattleTimingHistogram();
        }

        return histograms;
    }

    private static bool TryResolveShooterStage(string stageName, out BattleServerStage stage)
    {
        stage = stageName switch
        {
            "ShooterFrameBeginBattleSystem" => BattleServerStage.ShooterFrameBegin,
            "ShooterBotAiServiceBattleSystem" => BattleServerStage.ShooterBotAi,
            "ShooterEnemyWaveBattleSystem.Spawn" => BattleServerStage.ShooterEnemyWaveSpawn,
            "ShooterEnemyWaveBattleSystem.Attack" => BattleServerStage.ShooterEnemyWaveAttack,
            "ShooterEnemyMovementIntentBattleSystem" => BattleServerStage.ShooterEnemyMovementIntent,
            "ShooterEnemyRvoSolveBattleSystem" => BattleServerStage.ShooterRvoSolve,
            "ShooterEnemyRvoSolveBattleSystem.NeighborCollect" => BattleServerStage.ShooterRvoNeighborCollect,
            "ShooterEnemyRvoSolveBattleSystem.AcceleratedValidation" => BattleServerStage.ShooterRvoAcceleratedValidation,
            "ShooterEnemyRvoSolveBattleSystem.OrcaSolve" => BattleServerStage.ShooterRvoOrcaSolve,
            "ShooterEnemyMovementIntegrationBattleSystem" => BattleServerStage.ShooterEnemyMovementIntegration,
            "ShooterSimulationBattleSystem" => BattleServerStage.ShooterSimulation,
            "ShooterEnemyLifecycleCleanupBattleSystem" => BattleServerStage.ShooterEnemyLifecycleCleanup,
            "ShooterMatchStateBattleSystem" => BattleServerStage.ShooterMatchState,
            _ => default
        };

        return stageName is "ShooterFrameBeginBattleSystem"
            or "ShooterBotAiServiceBattleSystem"
            or "ShooterEnemyWaveBattleSystem.Spawn"
            or "ShooterEnemyWaveBattleSystem.Attack"
            or "ShooterEnemyMovementIntentBattleSystem"
            or "ShooterEnemyRvoSolveBattleSystem"
            or "ShooterEnemyRvoSolveBattleSystem.NeighborCollect"
            or "ShooterEnemyRvoSolveBattleSystem.AcceleratedValidation"
            or "ShooterEnemyRvoSolveBattleSystem.OrcaSolve"
            or "ShooterEnemyMovementIntegrationBattleSystem"
            or "ShooterSimulationBattleSystem"
            or "ShooterEnemyLifecycleCleanupBattleSystem"
            or "ShooterMatchStateBattleSystem";
    }

    private static string ResolveShooterStageName(int index)
    {
        return ((BattleServerStage)((int)BattleServerStage.ShooterFrameBegin + index)) switch
        {
            BattleServerStage.ShooterFrameBegin => "FrameBegin",
            BattleServerStage.ShooterBotAi => "BotAi",
            BattleServerStage.ShooterEnemyWaveSpawn => "EnemyWave",
            BattleServerStage.ShooterEnemyMovementIntent => "EnemyMovementIntent",
            BattleServerStage.ShooterRvoSolve => "RvoSolve",
            BattleServerStage.ShooterRvoNeighborCollect => "RvoNeighborCollect",
            BattleServerStage.ShooterRvoAcceleratedValidation => "RvoAcceleratedValidation",
            BattleServerStage.ShooterRvoOrcaSolve => "RvoOrcaSolve",
            BattleServerStage.ShooterEnemyMovementIntegration => "EnemyMovementIntegration",
            BattleServerStage.ShooterSimulation => "Simulation",
            BattleServerStage.ShooterEnemyLifecycleCleanup => "EnemyLifecycleCleanup",
            BattleServerStage.ShooterEnemyWaveAttack => "EnemyWaveAttack",
            BattleServerStage.ShooterMatchState => "MatchState",
            _ => $"Stage{index}"
        };
    }

    private sealed class BattleTimingHistogram
    {
        // Millisecond upper bounds. The final bucket represents values above 2 s.
        private static readonly double[] UpperBounds =
        {
            0.05d, 0.1d, 0.25d, 0.5d, 1d, 2d, 4d, 8d,
            16d, 25d, 33.4d, 50d, 100d, 250d, 500d, 1000d, 2000d
        };

        private readonly long[] _buckets = new long[UpperBounds.Length + 1];
        private long _count;
        private double _sumMilliseconds;
        private double _maxMilliseconds;

        public void RecordTimestampDelta(long startedAt, long completedAt)
        {
            RecordMilliseconds((completedAt - startedAt) * 1000d / Stopwatch.Frequency);
        }

        public void RecordMilliseconds(double milliseconds)
        {
            if (!double.IsFinite(milliseconds) || milliseconds < 0d)
            {
                return;
            }

            var bucket = 0;
            while (bucket < UpperBounds.Length && milliseconds > UpperBounds[bucket])
            {
                bucket++;
            }

            _buckets[bucket]++;
            _count++;
            _sumMilliseconds += milliseconds;
            _maxMilliseconds = Math.Max(_maxMilliseconds, milliseconds);
        }

        public BattleStageTimingSummary CreateSummary()
        {
            if (_count == 0)
            {
                return default;
            }

            return new BattleStageTimingSummary(
                _count,
                _sumMilliseconds / _count,
                ResolvePercentile(0.50d),
                ResolvePercentile(0.95d),
                ResolvePercentile(0.99d),
                _maxMilliseconds);
        }

        public void Clear()
        {
            Array.Clear(_buckets);
            _count = 0;
            _sumMilliseconds = 0d;
            _maxMilliseconds = 0d;
        }

        private double ResolvePercentile(double percentile)
        {
            var target = Math.Max(1L, (long)Math.Ceiling(_count * percentile));
            long cumulative = 0;
            for (var i = 0; i < _buckets.Length; i++)
            {
                cumulative += _buckets[i];
                if (cumulative < target)
                {
                    continue;
                }

                return i < UpperBounds.Length ? UpperBounds[i] : _maxMilliseconds;
            }

            return _maxMilliseconds;
        }
    }
}
