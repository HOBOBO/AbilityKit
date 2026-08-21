using System.Diagnostics;
using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleServerPerformanceDiagnosticsTests
{
    [Fact]
    public void CompleteTick_ReportsAchievedRateAndPerStageDistribution()
    {
        var diagnostics = new BattleServerPerformanceDiagnostics(windowTickCount: 5);
        var interval = Stopwatch.Frequency / 30;
        var startedAt = Stopwatch.Frequency;
        var stageSamples = new[] { 0.1d, 1d, 2d, 10d, 100d };
        BattleServerPerformanceSnapshot snapshot = default;

        for (var i = 0; i < 5; i++)
        {
            var tickStartedAt = startedAt + interval * i;
            diagnostics.BeginTick(100 + i, tickStartedAt);
            diagnostics.RecordStageMilliseconds(BattleServerStage.WorldTick, stageSamples[i]);

            var completedAt = tickStartedAt + (long)(2d * Stopwatch.Frequency / 1000d);
            var completed = diagnostics.CompleteTick(completedAt, 30, out snapshot);
            Assert.Equal(i == 4, completed);
        }

        Assert.Equal(100, snapshot.FirstFrame);
        Assert.Equal(104, snapshot.LastFrame);
        Assert.Equal(5, snapshot.TickCount);
        Assert.Equal(30, snapshot.TargetTickRate);
        Assert.InRange(snapshot.AchievedTickRate, 29.99d, 30.01d);
        Assert.Equal(4, snapshot.TickInterval.Count);
        Assert.Equal(33.4d, snapshot.TickInterval.P99Milliseconds);
        Assert.Equal(5, snapshot.TickTotal.Count);
        Assert.Equal(2d, snapshot.TickTotal.P99Milliseconds);
        Assert.Equal(5, snapshot.WorldTick.Count);
        Assert.Equal(2d, snapshot.WorldTick.P50Milliseconds);
        Assert.Equal(100d, snapshot.WorldTick.P95Milliseconds);
        Assert.Equal(100d, snapshot.WorldTick.P99Milliseconds);
        Assert.Equal(100d, snapshot.WorldTick.MaxMilliseconds);
    }

    [Fact]
    public void CompletedWindow_ResetsStageAndSupersessionCounters()
    {
        var diagnostics = new BattleServerPerformanceDiagnostics(windowTickCount: 2);
        var startedAt = Stopwatch.Frequency;

        diagnostics.BeginTick(1, startedAt);
        diagnostics.RecordStageMilliseconds(BattleServerStage.SnapshotBuild, 4d);
        diagnostics.RecordSnapshotQueuedBehindInFlight();
        Assert.False(diagnostics.CompleteTick(startedAt + 1, 30, out _));

        diagnostics.BeginTick(2, startedAt + Stopwatch.Frequency / 30);
        diagnostics.RecordStageMilliseconds(BattleServerStage.SnapshotBuild, 8d);
        diagnostics.RecordSnapshotQueuedBehindInFlight();
        Assert.True(diagnostics.CompleteTick(startedAt + Stopwatch.Frequency / 30 + 1, 30, out var first));

        Assert.Equal(2, first.SnapshotBuild.Count);
        Assert.Equal(2, first.SnapshotQueuedBehindInFlightCount);

        diagnostics.BeginTick(3, startedAt + Stopwatch.Frequency);
        Assert.False(diagnostics.CompleteTick(startedAt + Stopwatch.Frequency + 1, 30, out _));
        diagnostics.BeginTick(4, startedAt + Stopwatch.Frequency + Stopwatch.Frequency / 30);
        Assert.True(diagnostics.CompleteTick(startedAt + Stopwatch.Frequency + Stopwatch.Frequency / 30 + 1, 30, out var second));

        Assert.Equal(0, second.SnapshotBuild.Count);
        Assert.Equal(0, second.SnapshotQueuedBehindInFlightCount);
    }

    [Fact]
    public void Clear_DiscardsPartialWindow()
    {
        var diagnostics = new BattleServerPerformanceDiagnostics(windowTickCount: 2);
        var startedAt = Stopwatch.Frequency;

        diagnostics.BeginTick(50, startedAt);
        diagnostics.RecordStageMilliseconds(BattleServerStage.SnapshotDelivery, 250d);
        Assert.False(diagnostics.CompleteTick(startedAt + 1, 30, out _));

        diagnostics.Clear();

        diagnostics.BeginTick(60, startedAt + Stopwatch.Frequency);
        Assert.False(diagnostics.CompleteTick(startedAt + Stopwatch.Frequency + 1, 30, out _));
        diagnostics.BeginTick(61, startedAt + Stopwatch.Frequency + Stopwatch.Frequency / 30);
        Assert.True(diagnostics.CompleteTick(startedAt + Stopwatch.Frequency + Stopwatch.Frequency / 30 + 1, 30, out var snapshot));

        Assert.Equal(60, snapshot.FirstFrame);
        Assert.Equal(0, snapshot.SnapshotDelivery.Count);
    }

    [Fact]
    public void RecordingPartialWindow_DoesNotAllocatePerTick()
    {
        var diagnostics = new BattleServerPerformanceDiagnostics(windowTickCount: 10_000);
        var timestamp = Stopwatch.Frequency;
        var interval = Stopwatch.Frequency / 30;

        for (var i = 0; i < 16; i++)
        {
            diagnostics.BeginTick(i, timestamp);
            diagnostics.RecordStageMilliseconds(BattleServerStage.WorldTick, 2d);
            Assert.False(diagnostics.CompleteTick(timestamp + 1, 30, out _));
            timestamp += interval;
        }

        diagnostics.Clear();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            diagnostics.BeginTick(i, timestamp);
            diagnostics.RecordStageMilliseconds(BattleServerStage.InputSubmit, 0.1d);
            diagnostics.RecordStageMilliseconds(BattleServerStage.WorldTick, 2d);
            diagnostics.RecordStageMilliseconds(BattleServerStage.SnapshotBuild, 4d);
            diagnostics.RecordStageMilliseconds(BattleServerStage.SnapshotDelivery, 8d);
            diagnostics.CompleteTick(timestamp + 1, 30, out _);
            timestamp += interval;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ShooterStageSink_ReportsNamedSystemTimingsAtWindowBoundary()
    {
        var diagnostics = new BattleServerPerformanceDiagnostics(windowTickCount: 2);
        var startedAt = Stopwatch.Frequency;

        for (var i = 0; i < 2; i++)
        {
            diagnostics.BeginTick(i, startedAt + i * Stopwatch.Frequency / 30);
            diagnostics.RecordShooterStageMilliseconds("ShooterEnemyWaveBattleSystem.Spawn", 0.25d);
            diagnostics.RecordShooterStageMilliseconds("ShooterEnemyWaveBattleSystem.Attack", 0.5d);
            diagnostics.RecordShooterStageMilliseconds("ShooterEnemyRvoSolveBattleSystem", 4d);
            Assert.Equal(i == 1, diagnostics.CompleteTick(startedAt + i * Stopwatch.Frequency / 30 + 1, 30, out var snapshot));

            if (i == 1)
            {
                Assert.NotNull(snapshot.ShooterStages);
                Assert.Equal(2, snapshot.ShooterStages!["EnemyWave"].Count);
                Assert.Equal(2, snapshot.ShooterStages["EnemyWaveAttack"].Count);
                Assert.Equal(2, snapshot.ShooterStages["RvoSolve"].Count);
                Assert.Equal(4d, snapshot.ShooterStages["RvoSolve"].MaxMilliseconds);
            }
        }
    }
}
