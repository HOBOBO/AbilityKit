using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Protocol.Shooter;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterDemoHarnessCarrierTests
{
    [Fact]
    public void PredictRollbackControllerRunsThroughDemoHarnessCarrier()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-shooter-predict");
        Assert.True(controller.StartGame(in start));

        var carrier = new ShooterDemoHarnessCarrier(controller);
        var scenario = new DemoHarnessScenario(
            name: "Shooter predict rollback ideal",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Ideal,
            carrierName: ShooterDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 3,
            deltaSeconds: 1f / 30f,
            seed: 7);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(3, result.Metrics.StepsRun);
        Assert.Equal(3, result.Metrics.TotalTicks);
        Assert.Equal(3, result.Metrics.LastFrame);
        Assert.Equal(0, result.Metrics.ReconciliationCount);
        Assert.Equal(0, result.Metrics.NetworkStats.PendingCount);
        Assert.Equal(runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(presentation.ViewModel.Frame, result.Metrics.LastFrame);
        Assert.Equal(2, carrier.LastTimeAnchor.LocalFrame);
        Assert.Equal(2L, carrier.LastTimeAnchor.TimelineTicks);
        Assert.Equal(2d / 30d, carrier.LastTimeAnchor.ElapsedSeconds, precision: 6);
        Assert.False(carrier.LastTimeAnchor.HasAuthoritativeFrame);
        Assert.False(carrier.LastTimeAnchor.HasServerTicks);
    }

    [Fact]
    public void CarrierDeclaresPredictRollbackCapabilityAndRejectsInterpolationProfile()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-shooter-capability");
        Assert.True(controller.StartGame(in start));
        var carrier = new ShooterDemoHarnessCarrier(controller);

        var supported = carrier.Supports(NetworkSyncProfiles.PredictRollback, NetworkConditionProfile.Ideal);
        var unsupported = carrier.Supports(NetworkSyncProfiles.AuthoritativeInterpolation, NetworkConditionProfile.Ideal);

        Assert.Equal(SyncDemoCapabilityStatus.Supported, supported.Status);
        Assert.Equal(SyncDemoCapabilityStatus.Unsupported, unsupported.Status);
        Assert.Contains("predict rollback", unsupported.Reason);
    }

    [Fact]
    public void RunnerReturnsUnsupportedWhenShooterCarrierReceivesInterpolationScenario()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-shooter-unsupported");
        Assert.True(controller.StartGame(in start));
        var carrier = new ShooterDemoHarnessCarrier(controller);
        var scenario = new DemoHarnessScenario(
            name: "Shooter interpolation unsupported",
            syncModel: NetworkSyncModel.AuthoritativeInterpolation,
            networkProfile: NetworkConditionProfile.Ideal,
            carrierName: ShooterDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 2,
            deltaSeconds: 1f / 30f);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Unsupported, result.Status);
        Assert.False(result.Completed);
        Assert.Equal(0, result.Metrics.StepsRun);
        Assert.Contains("predict rollback", result.Reason);
    }

    [Fact]
    public void CarrierExposesInjectedNetworkAndHitMetricsToHarness()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-shooter-metrics");
        Assert.True(controller.StartGame(in start));

        var stats = new NetworkConditioningStats(
            inboundReceived: 10,
            inboundDelivered: 8,
            inboundDropped: 2,
            inboundReordered: 1,
            outboundReceived: 6,
            outboundDelivered: 5,
            outboundDropped: 1,
            outboundReordered: 0,
            pendingCount: 3);
        var carrier = new ShooterDemoHarnessCarrier(
            controller,
            networkStats: () => stats,
            remoteJitter: () => 0.25d,
            acceptedHits: () => 2L,
            rejectedHits: () => 1L);
        var scenario = new DemoHarnessScenario(
            name: "Shooter predict rollback poor wifi",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.PoorWifi,
            carrierName: ShooterDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 2,
            deltaSeconds: 1f / 30f);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(2, result.Metrics.StepsRun);
        Assert.Equal(0.25d, result.Metrics.AverageRemoteJitter, precision: 6);
        Assert.Equal(0.25d, result.Metrics.MaxRemoteJitter, precision: 6);
        Assert.Equal(4, result.Metrics.AcceptedHits);
        Assert.Equal(2, result.Metrics.RejectedHits);
        Assert.Equal(10, result.Metrics.NetworkStats.InboundReceived);
        Assert.Equal(8, result.Metrics.NetworkStats.InboundDelivered);
        Assert.Equal(2, result.Metrics.NetworkStats.InboundDropped);
        Assert.Equal(1, result.Metrics.NetworkStats.InboundReordered);
        Assert.Equal(6, result.Metrics.NetworkStats.OutboundReceived);
        Assert.Equal(5, result.Metrics.NetworkStats.OutboundDelivered);
        Assert.Equal(1, result.Metrics.NetworkStats.OutboundDropped);
        Assert.Equal(3, result.Metrics.NetworkStats.PendingCount);
    }

    [Fact]
    public void HybridCarrierRequiresHybridControllerForHybridProfileAndSupportsPredictRollbackInterop()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientHybridHeroPredictionSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-hybrid-capability");
        Assert.True(controller.StartGame(in start));
        var carrier = new ShooterHybridDemoHarnessCarrier(controller);

        var hybrid = carrier.Supports(NetworkSyncProfiles.HybridHeroPrediction, NetworkConditionProfile.Ideal);
        var predict = carrier.Supports(NetworkSyncProfiles.PredictRollback, NetworkConditionProfile.Ideal);
        var interpolation = carrier.Supports(NetworkSyncProfiles.AuthoritativeInterpolation, NetworkConditionProfile.Ideal);

        Assert.Equal(SyncDemoCapabilityStatus.Supported, hybrid.Status);
        Assert.Equal(SyncDemoCapabilityStatus.Supported, predict.Status);
        Assert.Equal(SyncDemoCapabilityStatus.Unsupported, interpolation.Status);
        Assert.Contains("hybrid or predict-rollback", interpolation.Reason);
    }

    [Fact]
    public void HybridCarrierRejectsHybridProfileWhenBackedByPredictRollbackController()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-hybrid-rejects-predict");
        Assert.True(controller.StartGame(in start));
        var carrier = new ShooterHybridDemoHarnessCarrier(controller);

        var hybrid = carrier.Supports(NetworkSyncProfiles.HybridHeroPrediction, NetworkConditionProfile.Ideal);

        Assert.Equal(SyncDemoCapabilityStatus.Unsupported, hybrid.Status);
        Assert.Contains("HybridHeroPrediction controller", hybrid.Reason);
    }

    [Fact]
    public void HybridCarrierRunsThroughDemoHarnessToCompletion()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientHybridHeroPredictionSyncController(
            runtime,
            presentation,
            tickRate: 30,
            decoder: null,
            gateway: null);
        var start = StartPayload("harness-hybrid-run");
        Assert.True(controller.StartGame(in start));

        var carrier = new ShooterHybridDemoHarnessCarrier(controller);
        var scenario = new DemoHarnessScenario(
            name: "Shooter hybrid ideal",
            syncModel: NetworkSyncModel.HybridHeroPrediction,
            networkProfile: NetworkConditionProfile.Ideal,
            carrierName: ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
            stepCount: 3,
            deltaSeconds: 1f / 30f,
            seed: 13);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(3, result.Metrics.StepsRun);
        Assert.Equal(3, result.Metrics.TotalTicks);
        Assert.Equal(3, result.Metrics.LastFrame);
        Assert.Equal(runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(presentation.ViewModel.Frame, result.Metrics.LastFrame);
    }

    private static ShooterStartGamePayload StartPayload(string sessionId)
    {
        return new ShooterStartGamePayload(
            sessionId,
            30,
            3901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            });
    }
}
