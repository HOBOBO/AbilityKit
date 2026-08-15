using System;
using System.Text.Json;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class SyncHealthEventTests
{
    [Fact]
    public void None_HasNoEvent()
    {
        var none = SyncHealthEvent.None;

        Assert.False(none.HasEvent);
        Assert.Equal(SyncHealthEventKind.None, none.Kind);
        Assert.Equal(SyncHealthSeverity.Info, none.Severity);
        Assert.Equal(0, none.Frame);
        Assert.Equal(0L, none.Value);
    }

    [Fact]
    public void Factories_SetSeverityAndPayload()
    {
        var info = SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, frame: 3, value: 7L);
        var warning = SyncHealthEvent.Warning(SyncHealthEventKind.InterpolationStarved, frame: 4);
        var error = SyncHealthEvent.Error(SyncHealthEventKind.InputRejected, frame: 5, value: 2L);

        Assert.True(info.HasEvent);
        Assert.Equal(SyncHealthSeverity.Info, info.Severity);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, info.Kind);
        Assert.Equal(3, info.Frame);
        Assert.Equal(7L, info.Value);

        Assert.Equal(SyncHealthSeverity.Warning, warning.Severity);
        Assert.Equal(SyncHealthSeverity.Error, error.Severity);
        Assert.Equal(2L, error.Value);
    }

    [Fact]
    public void NegativeFrame_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SyncHealthEvent(SyncHealthEventKind.SnapshotReceived, SyncHealthSeverity.Info, frame: -1));
    }

    [Fact]
    public void Equality_ComparesAllFields()
    {
        var a = SyncHealthEvent.Warning(SyncHealthEventKind.RollbackStarted, frame: 2, value: 4L);
        var b = SyncHealthEvent.Warning(SyncHealthEventKind.RollbackStarted, frame: 2, value: 4L);
        var c = SyncHealthEvent.Warning(SyncHealthEventKind.RollbackStarted, frame: 2, value: 5L);

        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a != c);
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void CorrelationContext_IsOptionalAndPropagatesWithoutBreakingLegacyConstruction()
    {
        var legacy = SyncHealthEvent.Warning(SyncHealthEventKind.SnapshotGap, frame: 8, value: 2L);
        var context = new SyncCorrelationContext(
            "run-1/client-1",
            runId: "run-1",
            sessionId: "session-1",
            accountId: "account-1",
            playerId: "7",
            roomId: "room-1",
            battleId: "battle-1",
            worldId: "42",
            observerId: "account-1:room-1",
            syncMode: "packed",
            tick: 8L,
            commandSequence: 3UL,
            snapshotSequence: 5L,
            snapshotBaseline: 4L,
            reliableEventSequence: 9L,
            reliableEventEpoch: "epoch-1");

        var correlated = legacy.WithContext(in context);

        Assert.False(legacy.Context.HasCorrelation);
        Assert.Equal("run-1/client-1", correlated.CorrelationId);
        Assert.Equal("battle-1", correlated.Context.BattleId);
        Assert.Equal(3UL, correlated.Context.CommandSequence);
        Assert.NotEqual(legacy, correlated);
    }

    [Fact]
    public void StepTelemetry_DefaultsToEmptyHealthEvents()
    {
        var telemetry = new DemoHarnessStepTelemetry(
            tickResult: new SyncTickResult(1, 4, 1u),
            reconciliationReport: SyncReconciliationReport.None,
            networkStats: NewStats());

        Assert.Empty(telemetry.HealthEvents);
    }

    [Fact]
    public void StepTelemetry_CarriesHealthEvents()
    {
        var telemetry = new DemoHarnessStepTelemetry(
            tickResult: new SyncTickResult(1, 4, 1u),
            reconciliationReport: SyncReconciliationReport.None,
            networkStats: NewStats(),
            remoteJitter: 0d,
            acceptedHits: 0,
            rejectedHits: 0,
            SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived),
            SyncHealthEvent.Warning(SyncHealthEventKind.InterpolationStarved, frame: 1));

        Assert.Equal(2, telemetry.HealthEvents.Count);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, telemetry.HealthEvents[0].Kind);
        Assert.Equal(SyncHealthEventKind.InterpolationStarved, telemetry.HealthEvents[1].Kind);
    }

    [Fact]
    public void EventBuffer_RetainsNewestEventsAndReportsFullHistory()
    {
        var buffer = new SyncHealthEventBuffer(2);
        var first = SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, frame: 1);
        var second = SyncHealthEvent.Warning(SyncHealthEventKind.SnapshotDropped, frame: 2);
        var third = SyncHealthEvent.Error(SyncHealthEventKind.SnapshotGap, frame: 3);

        buffer.Publish(in first);
        buffer.Publish(in second);
        buffer.Publish(SyncHealthEvent.None);
        buffer.Publish(in third);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(second, buffer[0]);
        Assert.Equal(third, buffer[1]);

        var report = buffer.CreateReport();
        Assert.Equal(3L, report.EventCount);
        Assert.Equal(1L, report.InfoCount);
        Assert.Equal(1L, report.WarningCount);
        Assert.Equal(1L, report.ErrorCount);
        Assert.Equal(1L, report.IgnoredEventCount);
        Assert.Equal(1L, report.OverwrittenEventCount);
        Assert.Equal(3, report.Kinds.Length);
        Assert.Equal(2, report.RetainedEvents.Length);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, report.Kinds[0].Kind);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.Equal(3L, document.RootElement.GetProperty("EventCount").GetInt64());
        Assert.Equal(
            (int)SyncHealthEventKind.SnapshotGap,
            document.RootElement.GetProperty("Kinds")[2].GetProperty("Kind").GetInt32());
    }

    [Fact]
    public void EventBuffer_ReportKeepsBoundedFirstCorrelationAndObserverReliableMetrics()
    {
        var buffer = new SyncHealthEventBuffer(1);
        var firstContext = new SyncCorrelationContext("run-1/client-1", runId: "run-1", tick: 4L);
        var secondContext = new SyncCorrelationContext("run-1/client-2", runId: "run-1", tick: 9L);
        var dropped = new SyncHealthEvent(
            SyncHealthEventKind.ObserverSnapshotDropped,
            SyncHealthSeverity.Warning,
            frame: 4,
            value: 2L,
            firstContext);
        var coalesced = new SyncHealthEvent(
            SyncHealthEventKind.ObserverSnapshotCoalesced,
            SyncHealthSeverity.Info,
            frame: 6,
            value: 1L,
            secondContext);
        var baseline = new SyncHealthEvent(
            SyncHealthEventKind.ObserverBaselineInvalidated,
            SyncHealthSeverity.Error,
            frame: 7,
            value: 1L,
            secondContext);
        var reliableGap = new SyncHealthEvent(
            SyncHealthEventKind.ReliableEventGap,
            SyncHealthSeverity.Error,
            frame: 9,
            value: 3L,
            secondContext);

        buffer.Publish(in dropped);
        buffer.Publish(in coalesced);
        buffer.Publish(in baseline);
        buffer.Publish(in reliableGap);

        var report = buffer.CreateReport();
        Assert.Equal(2, report.SchemaVersion);
        Assert.Equal(4, report.FirstFrame);
        Assert.Equal(9, report.LastFrame);
        Assert.Equal(SyncHealthSeverity.Error, report.HighestSeverity);
        Assert.Equal("run-1/client-1", report.FirstCorrelation.CorrelationId);
        Assert.Equal(1L, report.ObserverDroppedCount);
        Assert.Equal(1L, report.ObserverCoalescedCount);
        Assert.Equal(1L, report.ObserverBaselineInvalidatedCount);
        Assert.Equal(1L, report.ReliableGapCount);
        Assert.Single(report.RetainedEvents);
        Assert.Equal("run-1/client-2", report.RetainedEvents[0].CorrelationId);
    }

    [Fact]
    public void EventBuffer_ResetClearsRetainedEventsAndSummary()
    {
        var buffer = new SyncHealthEventBuffer(1);
        var healthEvent = SyncHealthEvent.Warning(
            SyncHealthEventKind.InterpolationStarved,
            frame: 7);
        buffer.Publish(in healthEvent);
        buffer.Publish(in healthEvent);

        buffer.Reset();

        Assert.Empty(buffer);
        var report = buffer.CreateReport();
        Assert.Equal(0L, report.EventCount);
        Assert.Equal(0L, report.OverwrittenEventCount);
        Assert.Empty(report.Kinds);
        Assert.Empty(report.RetainedEvents);
        Assert.Equal(-1, report.FirstFrame);
        Assert.False(report.FirstCorrelation.HasCorrelation);
    }

    [Fact]
    public void EventListView_ReflectsSourcesWithoutRecreatingTheView()
    {
        IReadOnlyList<SyncHealthEvent> primary = Array.Empty<SyncHealthEvent>();
        IReadOnlyList<SyncHealthEvent> secondary = Array.Empty<SyncHealthEvent>();
        var view = new SyncHealthEventListView(() => primary, () => secondary);

        primary = new[]
        {
            SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, frame: 1),
        };
        secondary = new[]
        {
            SyncHealthEvent.Warning(SyncHealthEventKind.InputRejected, frame: 2),
        };

        Assert.Equal(2, view.Count);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, view[0].Kind);
        Assert.Equal(SyncHealthEventKind.InputRejected, view[1].Kind);
    }

    [Fact]
    public void Metrics_AggregateHealthEventCountsBySeverity()
    {
        var carrier = new HealthEmittingCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var scenario = new DemoHarnessScenario(
            "Shooter",
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            carrierName: "Shooter",
            stepCount: 4,
            deltaSeconds: 0.05f);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        // Step 0: Info, Step 1: Warning, Step 2: Error, Step 3: Info + None (None ignored).
        Assert.Equal(4, result.Metrics.HealthEventCount);
        Assert.Equal(1, result.Metrics.HealthWarningCount);
        Assert.Equal(1, result.Metrics.HealthErrorCount);
        Assert.Equal(4L, result.Metrics.HealthReport.EventCount);
        Assert.Equal(1L, result.Metrics.HealthReport.IgnoredEventCount);
        Assert.Equal(4, result.Metrics.HealthReport.Kinds.Length);
    }

    private static NetworkConditioningStats NewStats()
    {
        return new NetworkConditioningStats(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed class HealthEmittingCarrier : ISyncDemoCarrier
    {
        public HealthEmittingCarrier(string carrierName, NetworkSyncModel syncModel)
        {
            CarrierName = carrierName;
            SyncModel = syncModel;
        }

        public string CarrierName { get; }

        public NetworkSyncModel SyncModel { get; }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            var step = context.StepIndex + 1;
            var tick = new SyncTickResult(step, step * 4, (uint)step);
            var stats = NewStats();

            return context.StepIndex switch
            {
                0 => new DemoHarnessStepTelemetry(tick, SyncReconciliationReport.None, stats, 0d, 0, 0,
                    SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, context.StepIndex)),
                1 => new DemoHarnessStepTelemetry(tick, SyncReconciliationReport.None, stats, 0d, 0, 0,
                    SyncHealthEvent.Warning(SyncHealthEventKind.InterpolationStarved, context.StepIndex)),
                2 => new DemoHarnessStepTelemetry(tick, SyncReconciliationReport.None, stats, 0d, 0, 0,
                    SyncHealthEvent.Error(SyncHealthEventKind.InputRejected, context.StepIndex)),
                _ => new DemoHarnessStepTelemetry(tick, SyncReconciliationReport.None, stats, 0d, 0, 0,
                    SyncHealthEvent.Info(SyncHealthEventKind.FullSnapshotApplied, context.StepIndex),
                    SyncHealthEvent.None),
            };
        }
    }
}
