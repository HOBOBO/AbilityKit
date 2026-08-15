using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class DemoHarnessRunnerTests
{
    [Fact]
    public void RunAggregatesGameplayAgnosticMetricsAcrossScenarioSteps()
    {
        var scenario = new DemoHarnessScenario(
            name: "Shooter poor wifi rollback",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.PoorWifi,
            carrierName: "Shooter",
            stepCount: 3,
            deltaSeconds: 1f / 30f,
            seed: 42);
        var carrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(string.Empty, result.FailureReason);
        Assert.Equal("Shooter poor wifi rollback", result.Scenario.Name);
        Assert.Equal(NetworkSyncModel.PredictRollback, result.Scenario.SyncModel);
        Assert.Equal("Shooter", result.Scenario.CarrierName);
        Assert.Equal(42, result.Scenario.Seed);

        Assert.Equal(3, carrier.StepCalls);
        Assert.Equal(3, result.Metrics.StepsRun);
        Assert.Equal(6, result.Metrics.TotalTicks);
        Assert.Equal(12, result.Metrics.LastFrame);
        Assert.Equal(1, result.Metrics.ReconciliationCount);
        Assert.Equal(1, result.Metrics.FullSnapshotRequestCount);
        Assert.Equal(4, result.Metrics.TotalReplayTicks);
        Assert.Equal(4, result.Metrics.MaxReplayTicks);
        Assert.Equal(0.2d, result.Metrics.AverageRemoteJitter, precision: 6);
        Assert.Equal(0.3d, result.Metrics.MaxRemoteJitter, precision: 6);
        Assert.Equal(2, result.Metrics.AcceptedHits);
        Assert.Equal(1, result.Metrics.RejectedHits);
        Assert.Equal(30, result.Metrics.NetworkStats.InboundReceived);
        Assert.Equal(27, result.Metrics.NetworkStats.InboundDelivered);
        Assert.Equal(3, result.Metrics.NetworkStats.InboundDropped);
        Assert.Equal(1, result.Metrics.NetworkStats.InboundReordered);
        Assert.Equal(15, result.Metrics.NetworkStats.OutboundReceived);
        Assert.Equal(14, result.Metrics.NetworkStats.OutboundDelivered);
        Assert.Equal(1, result.Metrics.NetworkStats.OutboundDropped);
        Assert.Equal(2, result.Metrics.NetworkStats.OutboundReordered);
        Assert.Equal(5, result.Metrics.NetworkStats.PendingCount);
    }

    [Fact]
    public void RunProvidesStepTimeAnchorWhileKeepingElapsedSecondsCompatibility()
    {
        var scenario = new DemoHarnessScenario(
            name: "Shooter timed rollback",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 3,
            deltaSeconds: 0.25f);
        var carrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.True(result.Completed);
        Assert.Equal(3, carrier.StepCalls);
        Assert.Equal(2, carrier.LastContext.StepIndex);
        Assert.Equal(0.5f, carrier.LastContext.ElapsedSeconds);
        Assert.Equal(0.25f, carrier.LastContext.DeltaSeconds);
        Assert.Equal(2, carrier.LastContext.TimeAnchor.LocalFrame);
        Assert.Equal(2L, carrier.LastContext.TimeAnchor.TimelineTicks);
        Assert.Equal(0.5d, carrier.LastContext.TimeAnchor.ElapsedSeconds, precision: 6);
        Assert.False(carrier.LastContext.TimeAnchor.HasAuthoritativeFrame);
        Assert.False(carrier.LastContext.TimeAnchor.HasServerTicks);
    }

    [Fact]
    public void SyncTimeAnchorCanCarryAuthoritativeFrameAndServerTicks()
    {
        var anchor = SyncTimeAnchor
            .FromLocalFrame(localFrame: 12, timelineTicks: 1200L, elapsedSeconds: 0.2d)
            .WithAuthoritativeFrame(10)
            .WithServerTicks(987654L);

        Assert.Equal(12, anchor.LocalFrame);
        Assert.Equal(1200L, anchor.TimelineTicks);
        Assert.Equal(0.2d, anchor.ElapsedSeconds, precision: 6);
        Assert.True(anchor.HasAuthoritativeFrame);
        Assert.Equal(10, anchor.AuthoritativeFrame);
        Assert.True(anchor.HasServerTicks);
        Assert.Equal(987654L, anchor.ServerTicks);
        Assert.Equal(anchor, new SyncTimeAnchor(12, 1200L, 0.2d, 10, true, 987654L, true));
    }

    [Fact]
    public void RunFailsWithoutSteppingWhenCarrierNameDoesNotMatchScenario()
    {
        var scenario = Scenario(NetworkSyncModel.AuthoritativeInterpolation, "Moba");
        var carrier = new ScriptedCarrier("Shooter", NetworkSyncModel.AuthoritativeInterpolation);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.False(result.Completed);
        Assert.Contains("Carrier mismatch", result.FailureReason);
        Assert.Equal(0, carrier.StepCalls);
        Assert.Equal(0, result.Metrics.StepsRun);
    }

    [Fact]
    public void RunFailsWithoutSteppingWhenSyncModelDoesNotMatchScenario()
    {
        var scenario = Scenario(NetworkSyncModel.AuthoritativeInterpolation, "Moba");
        var carrier = new ScriptedCarrier("Moba", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.False(result.Completed);
        Assert.Contains("Sync profile mismatch", result.FailureReason);
        Assert.Equal(0, carrier.StepCalls);
        Assert.Equal(0, result.Metrics.StepsRun);
    }

    [Fact]
    public void RunManyRunsMatchingScenariosAcrossMultipleCarriers()
    {
        var shooterScenario = new DemoHarnessScenario(
            name: "Shooter rollback matrix row",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.PoorWifi,
            carrierName: "Shooter",
            stepCount: 3,
            deltaSeconds: 1f / 30f,
            seed: 7);
        var mobaScenario = new DemoHarnessScenario(
            name: "Moba interpolation matrix row",
            syncModel: NetworkSyncModel.AuthoritativeInterpolation,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Moba",
            stepCount: 2,
            deltaSeconds: 1f / 60f,
            seed: 8);
        var shooterCarrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var mobaCarrier = new ScriptedCarrier("Moba", NetworkSyncModel.AuthoritativeInterpolation);
        var runner = new DemoHarnessRunner();

        var batch = runner.RunMany(
            new[] { shooterScenario, mobaScenario },
            new[] { mobaCarrier, shooterCarrier });

        Assert.True(batch.AllCompleted);
        Assert.Equal(2, batch.ScenarioCount);
        Assert.Equal(2, batch.CompletedCount);
        Assert.Equal(0, batch.FailedCount);
        Assert.Equal(2, batch.Results.Count);
        Assert.Equal("Shooter rollback matrix row", batch.Results[0].Scenario.Name);
        Assert.Equal("Moba interpolation matrix row", batch.Results[1].Scenario.Name);
        Assert.True(batch.Results[0].Completed);
        Assert.True(batch.Results[1].Completed);
        Assert.Equal(3, shooterCarrier.StepCalls);
        Assert.Equal(2, mobaCarrier.StepCalls);
        Assert.Equal(3, batch.Results[0].Metrics.StepsRun);
        Assert.Equal(2, batch.Results[1].Metrics.StepsRun);
    }

    [Fact]
    public void RunManyReturnsFailureForMissingCarrierAndContinuesOtherScenarios()
    {
        var missingScenario = new DemoHarnessScenario(
            name: "Moba rollback unsupported row",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.PoorWifi,
            carrierName: "Moba",
            stepCount: 2,
            deltaSeconds: 1f / 60f);
        var shooterScenario = new DemoHarnessScenario(
            name: "Shooter rollback row",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 2,
            deltaSeconds: 1f / 60f);
        var shooterCarrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var batch = runner.RunMany(
            new[] { missingScenario, shooterScenario },
            new ISyncDemoCarrier?[] { null, shooterCarrier });

        Assert.False(batch.AllCompleted);
        Assert.Equal(2, batch.ScenarioCount);
        Assert.Equal(1, batch.CompletedCount);
        Assert.Equal(1, batch.FailedCount);
        Assert.False(batch.Results[0].Completed);
        Assert.Contains("No carrier found", batch.Results[0].FailureReason);
        Assert.Equal(0, batch.Results[0].Metrics.StepsRun);
        Assert.True(batch.Results[1].Completed);
        Assert.Equal(2, shooterCarrier.StepCalls);
        Assert.Equal(2, batch.Results[1].Metrics.StepsRun);
    }

    [Fact]
    public void ScenarioMatrixBuildsProfileFirstCartesianRows()
    {
        var profiles = new[]
        {
            NetworkSyncProfiles.PredictRollback,
            NetworkSyncProfiles.AuthoritativeInterpolation
        };
        var networks = new[]
        {
            NetworkConditionProfile.Lan,
            NetworkConditionProfile.PoorWifi
        };

        var scenarios = DemoHarnessScenarioMatrix.Build(
            namePrefix: "matrix",
            syncProfiles: profiles,
            networkProfiles: networks,
            carrierNames: new[] { "Shooter", "Moba" },
            stepCount: 4,
            deltaSeconds: 1f / 20f,
            seed: 13);

        Assert.Equal(8, scenarios.Count);
        Assert.Equal("matrix Shooter PredictRollback network#0", scenarios[0].Name);
        Assert.Equal("matrix Shooter PredictRollback network#1", scenarios[1].Name);
        Assert.Equal("matrix Shooter AuthoritativeInterpolation network#0", scenarios[2].Name);
        Assert.Equal("matrix Moba PredictRollback network#0", scenarios[4].Name);
        Assert.Equal(NetworkSyncProfiles.PredictRollback, scenarios[0].SyncProfile);
        Assert.Equal(NetworkConditionProfile.PoorWifi, scenarios[1].NetworkProfile);
        Assert.Equal("Moba", scenarios[4].CarrierName);
        Assert.Equal(4, scenarios[0].StepCount);
        Assert.Equal(1f / 20f, scenarios[0].DeltaSeconds);
        Assert.Equal(13, scenarios[0].Seed);
    }

    [Fact]
    public void ScenarioMatrixBuildRunnableKeepsSupportedAndDegradedCarrierRows()
    {
        var shooterCarrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var mobaCarrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.AuthoritativeInterpolation,
            SyncDemoCapabilityResult.Unsupported("Moba carrier rejects this profile."),
            SyncDemoCapabilityResult.Degraded("Runs without AOI budget."));

        var scenarios = DemoHarnessScenarioMatrix.BuildRunnable(
            namePrefix: string.Empty,
            syncProfiles: new[]
            {
                NetworkSyncProfiles.PredictRollback,
                NetworkSyncProfiles.AuthoritativeInterpolation,
                NetworkSyncProfiles.MassBattleLodSync
            },
            networkProfiles: new[] { NetworkConditionProfile.Lan },
            carriers: new ISyncDemoCarrier?[] { null, shooterCarrier, mobaCarrier },
            stepCount: 2,
            deltaSeconds: 1f / 60f,
            seed: 22);

        Assert.Equal(2, scenarios.Count);
        Assert.Equal("Shooter PredictRollback network#0", scenarios[0].Name);
        Assert.Equal("Moba MassBattleLodSync network#0", scenarios[1].Name);
        Assert.Equal(NetworkSyncModel.PredictRollback, scenarios[0].SyncModel);
        Assert.Equal(NetworkSyncModel.MassBattleLodSync, scenarios[1].SyncModel);
        Assert.Equal("Shooter", scenarios[0].CarrierName);
        Assert.Equal("Moba", scenarios[1].CarrierName);
        Assert.Equal(22, scenarios[1].Seed);
    }

    [Fact]
    public void ScenarioCanCarryCustomSyncProfileWithoutLosingPolicyDimensions()
    {
        var profile = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.EventStream,
            InterestPolicy.OwnerRelevant | InterestPolicy.PriorityBudget,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation);
        var scenario = new DemoHarnessScenario(
            name: "custom moba profile",
            syncProfile: profile,
            networkProfile: NetworkConditionProfile.Mobile4G,
            carrierName: "Moba",
            stepCount: 2,
            deltaSeconds: 1f / 60f,
            seed: 91);
        var carrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.AuthoritativeInterpolation,
            SyncDemoCapabilityResult.Supported);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, scenario.SyncModel);
        Assert.Equal(profile, scenario.SyncProfile);
        Assert.Equal(profile, carrier.LastProfile);
        Assert.Equal(NetworkConditionProfile.Mobile4G, carrier.LastNetworkProfile);
        Assert.Equal(91, result.Scenario.Seed);
        Assert.True(result.Completed);
        Assert.Equal(2, carrier.StepCalls);
    }

    [Fact]
    public void RunUsesCarrierCapabilitiesWhenCarrierSupportsRequestedProfile()
    {
        var scenario = Scenario(NetworkSyncModel.AuthoritativeInterpolation, "Moba");
        var carrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.PredictRollback,
            SyncDemoCapabilityResult.Supported);
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(string.Empty, result.FailureReason);
        Assert.Equal(string.Empty, result.Reason);
        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, carrier.LastProfile.CompatibilityModel);
        Assert.Equal(NetworkConditionProfile.Lan, carrier.LastNetworkProfile);
        Assert.Equal(2, carrier.StepCalls);
    }

    [Fact]
    public void RunReturnsUnsupportedWithoutSteppingWhenCapabilityRejectsProfile()
    {
        var scenario = Scenario(NetworkSyncModel.PredictRollback, "Moba");
        var carrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.PredictRollback,
            SyncDemoCapabilityResult.Unsupported("Moba carrier currently supports authoritative interpolation only."));
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Unsupported, result.Status);
        Assert.False(result.Completed);
        Assert.Equal(string.Empty, result.FailureReason);
        Assert.Contains("authoritative interpolation", result.Reason);
        Assert.Equal(0, carrier.StepCalls);
    }

    [Fact]
    public void RunKeepsMetricsAndReasonWhenCapabilityIsDegraded()
    {
        var scenario = Scenario(NetworkSyncModel.MassBattleLodSync, "Moba");
        var carrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.AuthoritativeInterpolation,
            SyncDemoCapabilityResult.Degraded("Moba carrier does not yet implement AOI; running as all-entities playback."));
        var runner = new DemoHarnessRunner();

        var result = runner.Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Degraded, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(string.Empty, result.FailureReason);
        Assert.Contains("AOI", result.Reason);
        Assert.Equal(2, carrier.StepCalls);
        Assert.Equal(2, result.Metrics.StepsRun);
    }

    [Fact]
    public void RunManySeparatesUnsupportedFromFailedResults()
    {
        var unsupportedScenario = Scenario(NetworkSyncModel.PredictRollback, "Moba");
        var failedScenario = Scenario(NetworkSyncModel.PredictRollback, "Missing");
        var degradedScenario = Scenario(NetworkSyncModel.MassBattleLodSync, "Moba");
        var carrier = new CapabilityCarrier(
            "Moba",
            NetworkSyncModel.AuthoritativeInterpolation,
            SyncDemoCapabilityResult.Unsupported("Profile is not supported."),
            SyncDemoCapabilityResult.Degraded("Runs without AOI budget."));
        var runner = new DemoHarnessRunner();

        var batch = runner.RunMany(
            new[] { unsupportedScenario, failedScenario, degradedScenario },
            new ISyncDemoCarrier?[] { carrier });

        Assert.False(batch.AllCompleted);
        Assert.Equal(3, batch.ScenarioCount);
        Assert.Equal(1, batch.CompletedCount);
        Assert.Equal(1, batch.UnsupportedCount);
        Assert.Equal(1, batch.DegradedCount);
        Assert.Equal(1, batch.FailedCount);
        Assert.Equal(DemoHarnessRunStatus.Unsupported, batch.Results[0].Status);
        Assert.Equal(DemoHarnessRunStatus.Failed, batch.Results[1].Status);
        Assert.Equal(DemoHarnessRunStatus.Degraded, batch.Results[2].Status);
        Assert.Equal(3, batch.Summary.Rows.Count);
        Assert.Equal(1, batch.Summary.CountFor("Moba", NetworkSyncModel.PredictRollback, DemoHarnessRunStatus.Unsupported));
        Assert.Equal(1, batch.Summary.CountFor("Missing", NetworkSyncModel.PredictRollback, DemoHarnessRunStatus.Failed));
        Assert.Equal(1, batch.Summary.CountFor("Moba", NetworkSyncModel.MassBattleLodSync, DemoHarnessRunStatus.Degraded));
        Assert.Equal(0, batch.Summary.CountFor("Shooter", NetworkSyncModel.PredictRollback, DemoHarnessRunStatus.Completed));
    }

    [Fact]
    public void BatchSummaryAggregatesRepeatedCarrierProfileStatusRows()
    {
        var firstScenario = new DemoHarnessScenario(
            name: "Shooter rollback lan",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 1,
            deltaSeconds: 1f / 60f);
        var secondScenario = new DemoHarnessScenario(
            name: "Shooter rollback poor wifi",
            syncModel: NetworkSyncModel.PredictRollback,
            networkProfile: NetworkConditionProfile.PoorWifi,
            carrierName: "Shooter",
            stepCount: 1,
            deltaSeconds: 1f / 60f);
        var thirdScenario = new DemoHarnessScenario(
            name: "Shooter interpolation mismatch",
            syncModel: NetworkSyncModel.AuthoritativeInterpolation,
            networkProfile: NetworkConditionProfile.Lan,
            carrierName: "Shooter",
            stepCount: 1,
            deltaSeconds: 1f / 60f);
        var carrier = new ScriptedCarrier("Shooter", NetworkSyncModel.PredictRollback);
        var runner = new DemoHarnessRunner();

        var batch = runner.RunMany(
            new[] { firstScenario, secondScenario, thirdScenario },
            new ISyncDemoCarrier?[] { carrier });

        Assert.Equal(2, batch.Summary.Rows.Count);
        Assert.Equal(2, batch.Summary.CountFor("Shooter", NetworkSyncModel.PredictRollback, DemoHarnessRunStatus.Completed));
        Assert.Equal(1, batch.Summary.CountFor("Shooter", NetworkSyncModel.AuthoritativeInterpolation, DemoHarnessRunStatus.Failed));
        Assert.Equal(2, carrier.StepCalls);
    }

    [Fact]
    public void CarrierSelectorSelectsFirstRunnableCarrierByCapabilities()
    {
        var rejected = new CapabilityCarrier(
            "Rejected",
            NetworkSyncModel.PredictRollback,
            SyncDemoCapabilityResult.Unsupported("Rejected for this profile."));
        var selected = new CapabilityCarrier(
            "Selected",
            NetworkSyncModel.PredictRollback,
            SyncDemoCapabilityResult.Degraded("Runs with reduced diagnostics."));
        var fallback = new ScriptedCarrier("Fallback", NetworkSyncModel.AuthoritativeInterpolation);

        var found = DemoHarnessCarrierSelector.TrySelectFirst(
            new ISyncDemoCarrier?[] { null, rejected, selected, fallback },
            NetworkSyncProfiles.AuthoritativeInterpolation,
            NetworkConditionProfile.Mobile4G,
            out var carrier);

        Assert.True(found);
        Assert.Same(selected, carrier);
        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, rejected.LastProfile.CompatibilityModel);
        Assert.Equal(NetworkConditionProfile.Mobile4G, selected.LastNetworkProfile);
    }

    [Fact]
    public void CarrierSelectorFallsBackToCompatibilityModelAndThrowsWhenMissing()
    {
        var rollback = new ScriptedCarrier("Rollback", NetworkSyncModel.PredictRollback);
        var interpolation = new ScriptedCarrier("Interpolation", NetworkSyncModel.AuthoritativeInterpolation);

        var selected = DemoHarnessCarrierSelector.SelectFirstOrThrow(
            new ISyncDemoCarrier?[] { null, rollback, interpolation },
            NetworkSyncProfiles.AuthoritativeInterpolation,
            NetworkConditionProfile.Lan,
            "test carrier");

        Assert.Same(interpolation, selected);
        Assert.False(DemoHarnessCarrierSelector.TrySelectFirst(
            new ISyncDemoCarrier?[] { null, rollback },
            NetworkSyncProfiles.AuthoritativeInterpolation,
            NetworkConditionProfile.Lan,
            out _));
        Assert.Throws<NotSupportedException>(() => DemoHarnessCarrierSelector.SelectFirstOrThrow(
            new ISyncDemoCarrier?[] { rollback },
            NetworkSyncProfiles.AuthoritativeInterpolation,
            NetworkConditionProfile.Lan,
            "test carrier"));
    }

    [Fact]
    public void CarrierSelectorRejectsInvalidInputs()
    {
        var carrier = new ScriptedCarrier("Rollback", NetworkSyncModel.PredictRollback);

        Assert.Throws<ArgumentNullException>(() => DemoHarnessCarrierSelector.CanRun(
            null!,
            NetworkSyncProfiles.PredictRollback,
            NetworkConditionProfile.Lan));
        Assert.Throws<ArgumentNullException>(() => DemoHarnessCarrierSelector.TrySelectFirst(
            null!,
            NetworkSyncProfiles.PredictRollback,
            NetworkConditionProfile.Lan,
            out _));
        Assert.Throws<ArgumentException>(() => DemoHarnessCarrierSelector.SelectFirstOrThrow(
            new[] { carrier },
            NetworkSyncProfiles.PredictRollback,
            NetworkConditionProfile.Lan,
            string.Empty));
    }

    [Fact]
    public void ScenarioMatrixRejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => DemoHarnessScenarioMatrix.Build(
            null!,
            Array.Empty<NetworkSyncProfile>(),
            Array.Empty<NetworkConditionProfile>(),
            Array.Empty<string>(),
            1,
            1f));
        Assert.Throws<ArgumentNullException>(() => DemoHarnessScenarioMatrix.Build(
            "matrix",
            null!,
            Array.Empty<NetworkConditionProfile>(),
            Array.Empty<string>(),
            1,
            1f));
        Assert.Throws<ArgumentNullException>(() => DemoHarnessScenarioMatrix.BuildRunnable(
            "matrix",
            Array.Empty<NetworkSyncProfile>(),
            null!,
            Array.Empty<ISyncDemoCarrier>(),
            1,
            1f));
        Assert.Throws<ArgumentException>(() => DemoHarnessScenarioMatrix.Build(
            "matrix",
            new[] { NetworkSyncProfiles.PredictRollback },
            new[] { NetworkConditionProfile.Lan },
            new[] { string.Empty },
            1,
            1f));
    }

    [Fact]
    public void RunManyRejectsNullInputs()
    {
        var runner = new DemoHarnessRunner();

        Assert.Throws<ArgumentNullException>(() => runner.RunMany(null!, Array.Empty<ISyncDemoCarrier>()));
        Assert.Throws<ArgumentNullException>(() => runner.RunMany(Array.Empty<DemoHarnessScenario>(), null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ScenarioRejectsNonPositiveStepCount(int stepCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DemoHarnessScenario(
            "invalid",
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            "Shooter",
            stepCount,
            1f / 60f));
    }

    private static DemoHarnessScenario Scenario(NetworkSyncModel syncModel, string carrierName)
    {
        return new DemoHarnessScenario(
            "scenario",
            syncModel,
            NetworkConditionProfile.Lan,
            carrierName,
            stepCount: 2,
            deltaSeconds: 1f / 60f);
    }

    private sealed class ScriptedCarrier : ISyncDemoCarrier
    {
        public ScriptedCarrier(string carrierName, NetworkSyncModel syncModel)
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
            var report = context.StepIndex == 1
                ? new SyncReconciliationReport(
                    SyncReconciliationReason.AuthoritativeHashMismatch,
                    SyncRecoveryState.AwaitingFullSnapshot,
                    needsFullSnapshot: true,
                    clientFrame: 8,
                    authoritativeFrame: 6,
                    clientStateHash: 100u,
                    authoritativeStateHash: 200u,
                    replayTicks: 4)
                : SyncReconciliationReport.None;

            return new DemoHarnessStepTelemetry(
                tickResult: new SyncTickResult(step, step * 4, (uint)step),
                reconciliationReport: report,
                networkStats: new NetworkConditioningStats(
                    inboundReceived: step * 10,
                    inboundDelivered: step * 9,
                    inboundDropped: step,
                    inboundReordered: context.StepIndex == 2 ? 1 : 0,
                    outboundReceived: step * 5,
                    outboundDelivered: step * 5 - 1,
                    outboundDropped: context.StepIndex == 2 ? 1 : 0,
                    outboundReordered: context.StepIndex == 2 ? 2 : 0,
                    pendingCount: context.StepIndex == 2 ? 5 : 0),
                remoteJitter: step * 0.1d,
                acceptedHits: context.StepIndex == 0 ? 1 : context.StepIndex == 2 ? 1 : 0,
                rejectedHits: context.StepIndex == 1 ? 1 : 0);
        }
    }

    private sealed class CapabilityCarrier : ISyncDemoCarrier, ISyncDemoCarrierCapabilities
    {
        private readonly SyncDemoCapabilityResult _defaultResult;
        private readonly SyncDemoCapabilityResult? _massBattleResult;

        public CapabilityCarrier(
            string carrierName,
            NetworkSyncModel syncModel,
            SyncDemoCapabilityResult defaultResult,
            SyncDemoCapabilityResult? massBattleResult = null)
        {
            CarrierName = carrierName;
            SyncModel = syncModel;
            _defaultResult = defaultResult;
            _massBattleResult = massBattleResult;
        }

        public string CarrierName { get; }

        public NetworkSyncModel SyncModel { get; }

        public NetworkSyncProfile LastProfile { get; private set; }

        public NetworkConditionProfile LastNetworkProfile { get; private set; }

        public int StepCalls { get; private set; }

        public SyncDemoCapabilityResult Supports(in NetworkSyncProfile profile, in NetworkConditionProfile networkProfile)
        {
            LastProfile = profile;
            LastNetworkProfile = networkProfile;

            if (profile.CompatibilityModel == NetworkSyncModel.MassBattleLodSync && _massBattleResult.HasValue)
            {
                return _massBattleResult.Value;
            }

            return _defaultResult;
        }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            StepCalls++;
            var step = context.StepIndex + 1;
            return new DemoHarnessStepTelemetry(
                tickResult: new SyncTickResult(step, step, (uint)step),
                reconciliationReport: SyncReconciliationReport.None,
                networkStats: default);
        }
    }
}
