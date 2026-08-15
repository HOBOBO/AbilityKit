using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaDemoHarnessCarrierTests
{
    [Fact]
    public void AuthoritativeInterpolationControllerRunsThroughDemoHarnessCarrier()
    {
        var controller = new MobaClientAuthoritativeInterpolationSyncController(Config());
        controller.ObserveRemote(Sample(frame: 0, x: 0f, z: 0f));
        controller.ObserveRemote(Sample(frame: 2, x: 10f, z: 20f));
        var carrier = new MobaDemoHarnessCarrier(controller);
        var scenario = new DemoHarnessScenario(
            name: "Moba authoritative interpolation ideal",
            syncModel: NetworkSyncModel.AuthoritativeInterpolation,
            networkProfile: NetworkConditionProfile.Ideal,
            carrierName: MobaDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 1,
            deltaSeconds: 1f,
            seed: 11);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(1, result.Metrics.StepsRun);
        Assert.Equal(controller.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(0, result.Metrics.TotalTicks);
        Assert.Equal(0, result.Metrics.ReconciliationCount);
        Assert.True(controller.IsStarted);
        Assert.True(controller.HasPublishedRemoteFrame);
        Assert.Equal(0, carrier.LastTimeAnchor.LocalFrame);
        Assert.Equal(0L, carrier.LastTimeAnchor.TimelineTicks);
        Assert.Equal(0d, carrier.LastTimeAnchor.ElapsedSeconds, precision: 6);
        Assert.False(carrier.LastTimeAnchor.HasAuthoritativeFrame);
        Assert.False(carrier.LastTimeAnchor.HasServerTicks);
    }

    [Fact]
    public void CarrierDeclaresInterpolationCapabilityAndRejectsRollbackProfile()
    {
        var controller = new MobaClientAuthoritativeInterpolationSyncController(Config());
        var carrier = new MobaDemoHarnessCarrier(controller);

        var supported = carrier.Supports(NetworkSyncProfiles.AuthoritativeInterpolation, NetworkConditionProfile.Ideal);
        var unsupported = carrier.Supports(NetworkSyncProfiles.PredictRollback, NetworkConditionProfile.Ideal);

        Assert.Equal(SyncDemoCapabilityStatus.Supported, supported.Status);
        Assert.Equal(SyncDemoCapabilityStatus.Unsupported, unsupported.Status);
        Assert.Equal(MobaDemoHarnessCarrier.AuthoritativeInterpolationRequiredReason, unsupported.Reason);
    }

    [Fact]
    public void RunnerMarksMassBattleLodScenarioAsDegradedForMobaCarrier()
    {
        var controller = new MobaClientAuthoritativeInterpolationSyncController(Config());
        controller.ObserveRemote(Sample(frame: 0, x: 0f, z: 0f));
        controller.ObserveRemote(Sample(frame: 2, x: 10f, z: 20f));
        var carrier = new MobaDemoHarnessCarrier(controller);
        var scenario = new DemoHarnessScenario(
            name: "Moba mass battle lod degraded",
            syncModel: NetworkSyncModel.MassBattleLodSync,
            networkProfile: NetworkConditionProfile.Mobile4G,
            carrierName: MobaDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 1,
            deltaSeconds: 1f);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Degraded, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(MobaDemoHarnessCarrier.MassBattleLodDegradedReason, result.Reason);
        Assert.Contains("DistanceAoi", result.Reason);
        Assert.Contains("PriorityBudget", result.Reason);
        Assert.Contains("LodFrequency", result.Reason);
        Assert.Contains("RequestAoiSlice", result.Reason);
        Assert.Equal(1, result.Metrics.StepsRun);
    }

    [Fact]
    public void CarrierExposesInjectedNetworkAndHitMetricsToHarness()
    {
        var controller = new MobaClientAuthoritativeInterpolationSyncController(Config());
        controller.ObserveRemote(Sample(frame: 0, x: 0f, z: 0f));
        controller.ObserveRemote(Sample(frame: 2, x: 10f, z: 20f));
        var stats = new NetworkConditioningStats(
            inboundReceived: 12,
            inboundDelivered: 11,
            inboundDropped: 1,
            inboundReordered: 2,
            outboundReceived: 9,
            outboundDelivered: 7,
            outboundDropped: 2,
            outboundReordered: 1,
            pendingCount: 4);
        var carrier = new MobaDemoHarnessCarrier(
            controller,
            networkStats: () => stats,
            remoteJitter: () => 0.5d,
            acceptedHits: () => 3L,
            rejectedHits: () => 2L);
        var scenario = new DemoHarnessScenario(
            name: "Moba authoritative interpolation mobile",
            syncModel: NetworkSyncModel.AuthoritativeInterpolation,
            networkProfile: NetworkConditionProfile.Mobile4G,
            carrierName: MobaDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 2,
            deltaSeconds: 1f);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(2, result.Metrics.StepsRun);
        Assert.Equal(1, carrier.LastTimeAnchor.LocalFrame);
        Assert.Equal(1L, carrier.LastTimeAnchor.TimelineTicks);
        Assert.Equal(1d, carrier.LastTimeAnchor.ElapsedSeconds, precision: 6);
        Assert.Equal(0.5d, result.Metrics.AverageRemoteJitter, precision: 6);
        Assert.Equal(0.5d, result.Metrics.MaxRemoteJitter, precision: 6);
        Assert.Equal(6, result.Metrics.AcceptedHits);
        Assert.Equal(4, result.Metrics.RejectedHits);
        Assert.Equal(12, result.Metrics.NetworkStats.InboundReceived);
        Assert.Equal(11, result.Metrics.NetworkStats.InboundDelivered);
        Assert.Equal(1, result.Metrics.NetworkStats.InboundDropped);
        Assert.Equal(2, result.Metrics.NetworkStats.InboundReordered);
        Assert.Equal(9, result.Metrics.NetworkStats.OutboundReceived);
        Assert.Equal(7, result.Metrics.NetworkStats.OutboundDelivered);
        Assert.Equal(2, result.Metrics.NetworkStats.OutboundDropped);
        Assert.Equal(1, result.Metrics.NetworkStats.OutboundReordered);
        Assert.Equal(4, result.Metrics.NetworkStats.PendingCount);
    }

    private static InterpolationConfig Config() =>
        new InterpolationConfig(
            ticksPerSecond: 1L,
            interpolationDelayTicks: 1L,
            bufferCapacity: 16,
            catchUpRate: 0d);

    private static MobaRemoteSnapshotSample Sample(int frame, float x, float z)
    {
        var actors = new[]
        {
            new GatewayStateSyncActorSnapshot(
                actorId: 1,
                x: x,
                y: 0f,
                z: z,
                rotation: 0f,
                velocityX: 0f,
                velocityZ: 0f,
                hp: 100f,
                hpMax: 100f,
                teamId: 1),
        };

        return new MobaRemoteSnapshotSample(worldId: 7UL, frame: frame, actors: actors);
    }
}
