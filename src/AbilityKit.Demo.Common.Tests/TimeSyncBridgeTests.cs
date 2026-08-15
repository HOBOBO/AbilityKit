using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class TimeSyncBridgeTests
{
    private const long TicksPerSecond = 10_000_000L;

    [Fact]
    public void NewBridge_HasNotConverged()
    {
        var bridge = new TimeSyncBridge(TicksPerSecond);

        Assert.False(bridge.HasConverged);
        Assert.Equal(0, bridge.SampleCount);
        Assert.Equal(TicksPerSecond, bridge.TickFrequency);
    }

    [Fact]
    public void ObserveResponse_ConvergesAndEstimatesOffset()
    {
        var bridge = new TimeSyncBridge(TicksPerSecond);

        // RTT = 200, midpoint = 1100, server offset = 5000 - 1100 = 3900.
        var improved = bridge.ObserveResponse(clientSendTicks: 1000L, serverNowTicks: 5000L, clientReceiveTicks: 1200L);

        Assert.True(improved);
        Assert.True(bridge.HasConverged);
        Assert.Equal(1, bridge.SampleCount);
        Assert.Equal(3900d / TicksPerSecond, bridge.OffsetSeconds, precision: 9);
        Assert.Equal(200d / TicksPerSecond, bridge.BestRoundTripSeconds, precision: 9);
    }

    [Fact]
    public void ObserveResponse_KeepsLowestRoundTripSample()
    {
        var bridge = new TimeSyncBridge(TicksPerSecond);

        bridge.ObserveResponse(1000L, 5000L, 2000L); // RTT 1000
        var improved = bridge.ObserveResponse(3000L, 6000L, 3200L); // RTT 200, better

        Assert.True(improved);
        Assert.Equal(2, bridge.SampleCount);
        // Better sample: midpoint = 3100, offset = 6000 - 3100 = 2900.
        Assert.Equal(2900d / TicksPerSecond, bridge.OffsetSeconds, precision: 9);
    }

    [Fact]
    public void CreateClock_BeforeConvergence_ProducesUnstampedAnchors()
    {
        var bridge = new TimeSyncBridge(TicksPerSecond);
        var clock = bridge.CreateClock(deltaSeconds: 0.1d, timelineTicksPerStep: TicksPerSecond);

        var anchor = clock.Advance();

        Assert.False(anchor.HasServerTicks);
    }

    [Fact]
    public void CreateClock_AfterConvergence_StampsServerTicks()
    {
        var bridge = new TimeSyncBridge(TicksPerSecond);
        bridge.ObserveResponse(1000L, 5000L, 1200L); // offset 3900
        var clock = bridge.CreateClock(deltaSeconds: 0.1d, timelineTicksPerStep: TicksPerSecond);

        var anchor = clock.AnchorFor(2);

        Assert.True(anchor.HasServerTicks);
        Assert.Equal(2L * TicksPerSecond + 3900L, anchor.ServerTicks);
    }

    [Fact]
    public void DemoHarness_WithBridgeBackedClockFactory_StampsServerTicksOnAnchors()
    {
        var scenario = new DemoHarnessScenario(
            name: "Shooter server-clock demo",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 3,
            deltaSeconds: 0.25f);
        var carrier = new RecordingCarrier("Shooter", NetworkSyncModel.PredictRollback);

        var bridge = new TimeSyncBridge(TicksPerSecond);
        bridge.ObserveResponse(1000L, 5000L, 1200L); // converged, offset 3900
        var runner = new DemoHarnessRunner(s => bridge.CreateClock(s.DeltaSeconds, TicksPerSecond));

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(3, carrier.StepCalls);
        var lastAnchor = carrier.LastContext.TimeAnchor;
        Assert.True(lastAnchor.HasServerTicks);
        // Last step is frame index 2: timelineTicks = 2 * TicksPerSecond, + offset 3900.
        Assert.Equal(2L * TicksPerSecond + 3900L, lastAnchor.ServerTicks);
    }

    [Fact]
    public void DemoHarness_DefaultClockFactory_LeavesServerTicksUnstamped()
    {
        var scenario = new DemoHarnessScenario(
            name: "Shooter no server clock",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 2,
            deltaSeconds: 0.25f);
        var carrier = new RecordingCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.False(carrier.LastContext.TimeAnchor.HasServerTicks);
    }

    private sealed class RecordingCarrier : ISyncDemoCarrier
    {
        public RecordingCarrier(string carrierName, NetworkSyncModel syncModel)
        {
            CarrierName = carrierName;
            SyncModel = syncModel;
        }

        public string CarrierName { get; }

        public NetworkSyncModel SyncModel { get; }

        public int StepCalls { get; private set; }

        public DemoHarnessStepContext LastContext { get; private set; }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            StepCalls++;
            LastContext = context;

            var step = context.StepIndex + 1;
            return new DemoHarnessStepTelemetry(
                tickResult: new SyncTickResult(step, step * 4, (uint)step),
                reconciliationReport: SyncReconciliationReport.None,
                networkStats: new NetworkConditioningStats(
                    inboundReceived: 0,
                    inboundDelivered: 0,
                    inboundDropped: 0,
                    inboundReordered: 0,
                    outboundReceived: 0,
                    outboundDelivered: 0,
                    outboundDropped: 0,
                    outboundReordered: 0,
                    pendingCount: 0),
                remoteJitter: 0d,
                acceptedHits: 0,
                rejectedHits: 0);
        }
    }
}
