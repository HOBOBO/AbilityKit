using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterSnapshotAllocationDiagnosticsTests
{
    private const int Iterations = 32;
    private readonly ITestOutputHelper _output;

    public ShooterSnapshotAllocationDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PackedSnapshotExportReportsAllocationBudgetDiagnostics()
    {
        var runtime = CreateRuntime("packed-allocation-diagnostics", seed: 8101);
        WarmUp(runtime);

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            ShooterPackedSnapshotPayload last = default;
            for (var i = 0; i < Iterations; i++)
            {
                last = runtime.ExportPackedSnapshot(8101ul, isFullSnapshot: i == 0, authorityOverride: true);
            }

            Assert.True(last.EntityCount > 0);
            Assert.Equal(runtime.CurrentFrame, last.Frame);
            Assert.Equal(runtime.ComputeStateHash(), last.StateHash);
        });

        WriteAllocationDiagnostic("packed-snapshot-export", allocatedBytes);
        Assert.True(allocatedBytes >= 0);
        Assert.True(allocatedBytes / Iterations < 2_000_000L);
    }

    [Fact]
    public void PureStateSnapshotExportReportsAllocationBudgetDiagnostics()
    {
        var runtime = CreateRuntime("pure-state-allocation-diagnostics", seed: 8102);
        WarmUp(runtime);
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 128,
            activeSyncBudget: 128,
            baselineIntervalFrames: 60,
            deltaIntervalFrames: 1,
            lowFrequencyIntervalFrames: 10,
            interpolationDelayFrames: 3);
        var baseline = runtime.ExportPureStateSnapshot(8102ul, isFullBaseline: true, settings: settings);

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            ShooterPureStateSnapshotPayload last = default;
            for (var i = 0; i < Iterations; i++)
            {
                last = runtime.ExportPureStateSnapshot(
                    8102ul,
                    isFullBaseline: false,
                    settings: settings,
                    baselineFrame: baseline.Frame,
                    baselineHash: baseline.StateHash);
            }

            Assert.NotEmpty(last.Entities);
            Assert.Equal(runtime.CurrentFrame, last.Frame);
            Assert.Equal(runtime.ComputeStateHash(), last.StateHash);
        });

        WriteAllocationDiagnostic("pure-state-snapshot-export", allocatedBytes);
        Assert.True(allocatedBytes >= 0);
        Assert.True(allocatedBytes / Iterations < 2_000_000L);
    }

    [Fact]
    public void TransientStateSnapshotReusesBuffersWithoutSteadyStateArrayAllocation()
    {
        var runtime = CreateRuntime("state-transient-allocation-diagnostics", seed: 8104);
        WarmUp(runtime);
        var first = runtime.GetSnapshotTransient();
        var last = first;

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                last = runtime.GetSnapshotTransient();
            }
        });

        WriteAllocationDiagnostic("state-snapshot-transient", allocatedBytes);
        Assert.Same(first.Players, last.Players);
        Assert.Same(first.Bullets, last.Bullets);
        Assert.Same(first.Events, last.Events);
        Assert.Same(first.Enemies, last.Enemies);
        Assert.True(allocatedBytes / Iterations < 1_024L);
    }

    [Fact]
    public void OwnedStateSnapshotDoesNotAliasTransientBuffers()
    {
        var runtime = CreateRuntime("state-transient-ownership", seed: 8105);
        WarmUp(runtime);

        var transient = runtime.GetSnapshotTransient();
        var owned = runtime.GetSnapshot();

        Assert.NotSame(transient.Players, owned.Players);
        Assert.NotSame(transient.Bullets, owned.Bullets);
        Assert.NotSame(transient.Events, owned.Events);
        Assert.Equal(transient.Players, owned.Players);
        Assert.Equal(transient.Bullets, owned.Bullets);
        Assert.Equal(transient.Events, owned.Events);
    }

    [Fact]
    public void PlayerOnlyTransientSnapshotReusesTheFullSnapshotPlayerBuffer()
    {
        var runtime = CreateRuntime("player-only-transient", seed: 8107);
        WarmUp(runtime);

        var playersOnly = runtime.GetPlayerSnapshotsTransient();
        var full = runtime.GetSnapshotTransient();

        Assert.Same(playersOnly, full.Players);
        Assert.Equal(runtime.GetSnapshot().Players, playersOnly);
    }

    [Fact]
    public void PureStateTransientSerializeAvoidsExporterArrayAllocation()
    {
        var runtime = CreateRuntime("pure-state-transient-allocation-diagnostics", seed: 8106);
        WarmUp(runtime);
        var settings = new ShooterPureStateSyncSettings(128, 128, 60, 1, 10, 3);
        var baseline = runtime.ExportPureStateSnapshot(8106ul, isFullBaseline: true, settings: settings);
        var warmPayload = runtime.ExportPureStateSnapshotTransient(
            8106ul,
            isFullBaseline: false,
            settings: settings,
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);
        var serializationBuffer = new ReusableMemoryPackSerializationBuffer();
        ShooterPureStateSyncCodec.SerializeTransient(in warmPayload, serializationBuffer);
        byte[] lastBytes = Array.Empty<byte>();

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                var snapshot = runtime.ExportPureStateSnapshotTransient(
                    8106ul,
                    isFullBaseline: false,
                    settings: settings,
                    baselineFrame: baseline.Frame,
                    baselineHash: baseline.StateHash);
                lastBytes = ShooterPureStateSyncCodec.SerializeTransient(in snapshot, serializationBuffer);
            }
        });

        WriteAllocationDiagnostic("pure-state-transient-serialize", allocatedBytes);
        Assert.NotEmpty(lastBytes);
        Assert.True(allocatedBytes / Iterations < 1_024L);
    }

    [Fact]
    public void StateHashReportsAllocationBudgetDiagnostics()
    {
        var runtime = CreateRuntime("state-hash-allocation-diagnostics", seed: 8103);
        WarmUp(runtime);
        var expected = runtime.ComputeStateHash();

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            uint last = 0;
            for (var i = 0; i < Iterations; i++)
            {
                last = runtime.ComputeStateHash();
            }

            Assert.Equal(expected, last);
        });

        WriteAllocationDiagnostic("state-hash", allocatedBytes);
        Assert.True(allocatedBytes >= 0);
        Assert.True(allocatedBytes / Iterations < 256_000L);
    }

    private static ShooterBattleRuntimePort CreateRuntime(string matchId, int seed)
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            matchId,
            30,
            seed,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -2f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f),
                new ShooterStartPlayer(3, "P3", 0f, 2f),
                new ShooterStartPlayer(4, "P4", 0f, -2f)
            });

        Assert.True(runtime.StartGame(in start));
        return runtime;
    }

    private static void WarmUp(ShooterBattleRuntimePort runtime)
    {
        runtime.SubmitInput(0, new[]
        {
            new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true),
            new ShooterPlayerCommand(2, -1f, 0f, -1f, 0f, true)
        });
        Assert.True(runtime.Tick(1f / 30f));
        runtime.ExportPackedSnapshot(8191ul, isFullSnapshot: true, authorityOverride: true);
        runtime.ExportPureStateSnapshot(8191ul, isFullBaseline: true);
        runtime.ComputeStateHash();
    }

    private static long MeasureAllocatedBytes(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private void WriteAllocationDiagnostic(string path, long allocatedBytes)
    {
        _output.WriteLine($"{path}: total={allocatedBytes} bytes, per-iteration={allocatedBytes / Iterations} bytes, iterations={Iterations}");
    }
}
