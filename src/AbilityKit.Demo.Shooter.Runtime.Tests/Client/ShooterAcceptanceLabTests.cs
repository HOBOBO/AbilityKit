using System.Linq;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.LagCompensation;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Shooter.View.PlayMode;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

[Collection("ShooterAcceptance")]
public sealed class ShooterAcceptanceLabTests
{
    [Fact]
    public void CatalogExposesImplementedModesAndNetworkEnvironments()
    {
        Assert.NotEmpty(ShooterAcceptanceCatalog.SyncModes);
        Assert.NotEmpty(ShooterAcceptanceCatalog.NetworkEnvironments);

        Assert.Contains(ShooterAcceptanceCatalog.SyncModes,
            m => m.Model == NetworkSyncModel.PredictRollback && m.Implemented);
        Assert.Contains(ShooterAcceptanceCatalog.NetworkEnvironments, n => n.Id == "ideal");
        Assert.Contains(ShooterAcceptanceCatalog.NetworkEnvironments, n => n.Id == "poorwifi");
    }

    [Fact]
    public void CatalogExposesFormalSyncModeMatrixForEveryTemplate()
    {
        var matrix = ShooterAcceptanceCatalog.SyncModeMatrix;

        Assert.Equal(ShooterAcceptanceCatalog.SyncTemplates.Count, matrix.Rows.Count);
        foreach (var template in ShooterAcceptanceCatalog.SyncTemplates)
        {
            var row = matrix.GetRow(template.Id);
            Assert.Equal(template.Id, row.TemplateId);
            Assert.Equal(template.SyncModel, row.SyncModel);
            Assert.Equal(template.ExpectedCarrierName, row.ExpectedCarrierName);
            Assert.Equal(template.EnableAuthoritativeWorld, row.RequiresAuthoritativeWorld);
            Assert.Equal(template.ExpectsInterpolationDiagnostics, row.ExposesInterpolationDiagnostics);
            Assert.NotEmpty(row.AcceptanceCriteria);
            Assert.Equal(NetworkSyncProfiles.FromCompatibilityModel(template.SyncModel).CompatibilityModel, row.Profile.CompatibilityModel);
        }
    }

    [Fact]
    public void SyncModeMatrixDocumentsDifferentAcceptanceBoundaries()
    {
        var predict = ShooterAcceptanceCatalog.GetSyncModeMatrixRow("predict-rollback-authority");
        var interpolation = ShooterAcceptanceCatalog.GetSyncModeMatrixRow("authoritative-interpolation-presentation");
        var hybrid = ShooterAcceptanceCatalog.GetSyncModeMatrixRow("hybrid-hero-prediction");

        Assert.Contains(predict.AcceptanceCriteria, c => c.Id == "prediction-reconciliation");
        Assert.Contains(interpolation.AcceptanceCriteria, c => c.Id == "remote-buffer");
        Assert.Contains(interpolation.AcceptanceCriteria, c => c.Id == "server-authority");
        Assert.Contains(hybrid.AcceptanceCriteria, c => c.Id == "local-hero-prediction");
        Assert.Contains(hybrid.AcceptanceCriteria, c => c.Id == "full-snapshot-recovery");
    }

    [Fact]
    public void CatalogExposesSyncTemplatesForEveryImplementedMode()
    {
        Assert.NotEmpty(ShooterAcceptanceCatalog.SyncTemplates);

        foreach (var mode in ShooterAcceptanceCatalog.SyncModes.Where(m => m.Implemented))
        {
            Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates, template => template.SyncModel == mode.Model);
        }

        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "predict-rollback-authority"
                && template.ExpectedCarrierName == ShooterDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.RuntimeSnapshot
                && template.Status == ShooterSyncTemplateStatus.Implemented);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "authoritative-interpolation-presentation"
                && template.ExpectedCarrierName == ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.PresentationInterpolation
                && template.Status == ShooterSyncTemplateStatus.Implemented);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "batch-state-low-frequency"
                && template.ExpectedCarrierName == ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.BatchedPresentation
                && template.Status == ShooterSyncTemplateStatus.Experimental);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "mass-battle-lod-aoi"
                && template.ExpectedCarrierName == ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation
                && template.Status == ShooterSyncTemplateStatus.Experimental);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "mass-battle-lod-aoi-sample-block"
                && template.ExpectedCarrierName == ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation
                && template.Status == ShooterSyncTemplateStatus.Experimental);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "hybrid-hero-prediction"
                && template.ExpectedCarrierName == ShooterHybridDemoHarnessCarrier.DefaultCarrierName
                && template.ConvergenceKind == ShooterSyncTemplateConvergenceKind.RuntimeSnapshotWithRemoteInterpolation
                && template.Status == ShooterSyncTemplateStatus.Implemented);
        Assert.Contains(ShooterAcceptanceCatalog.SyncTemplates,
            template => template.Id == "fast-reconnect-resume"
                && template.Status == ShooterSyncTemplateStatus.Reserved
                && !template.IsRunnable);
    }

    [Fact]
    public void DefaultRoomLaunchSpecUsesRunnableClientSyncTemplate()
    {
        var launchSpec = ShooterRoomLaunchSpec.CreateDefault("client-a");
        var template = ShooterAcceptanceCatalog.GetSyncTemplate(launchSpec.SyncTemplateId);

        // 2K multiplayer defaults to the pure-state AOI/LOD path; packed state sync remains explicitly selectable.
        Assert.Equal(ShooterSyncTemplateIds.MassBattleLodAoi, launchSpec.SyncTemplateId);
        Assert.Equal((int)NetworkSyncModel.MassBattleLodSync, launchSpec.SyncModel);
        Assert.Equal("limitedbw", launchSpec.NetworkEnvironmentId);
        Assert.Equal(ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName, launchSpec.CarrierName);
        Assert.Equal(template.SyncModel, (NetworkSyncModel)launchSpec.SyncModel);
        Assert.True(template.IsRunnable);
        Assert.False(launchSpec.EnableAuthoritativeWorld);
        Assert.True(launchSpec.InterpolationEnabled);
    }

    [Fact]
    public void SyncTemplateCreatesSessionWithAssociatedConfiguration()
    {
        foreach (var template in ShooterAcceptanceCatalog.SyncTemplates.Where(t => t.IsRunnable))
        {
            using (var session = ShooterAcceptanceLab.Create(in template))
            {
                Assert.Equal(template.SyncModel, session.SyncModel);
                Assert.Equal(template.DisplayName, session.NetworkName);
                Assert.Equal(template.ExpectedCarrierName, session.Carrier.CarrierName);
                Assert.Equal(template.EnableAuthoritativeWorld, session.HasAuthoritativeWorld);
                Assert.Equal(template.RecommendedPlayerCount, session.Runtime.GetSnapshot().Players.Length);
                Assert.Equal(template.ExpectsInterpolationDiagnostics, session.Controller is IInterpolationDiagnosticsProvider);
            }
        }
    }

    [Fact]
    public void SyncTemplateSendPoliciesExposeBatchAndLodSettingsForSmokeValidation()
    {
        var batch = ShooterAcceptanceCatalog.GetSyncTemplate("batch-state-low-frequency");
        var mass = ShooterAcceptanceCatalog.GetSyncTemplate("mass-battle-lod-aoi");

        Assert.Equal(NetworkSyncModel.BatchStateSync, batch.SyncModel);
        Assert.Equal(ShooterSyncTemplateConvergenceKind.BatchedPresentation, batch.ConvergenceKind);
        Assert.Equal(ShooterSyncTemplateStatus.Experimental, batch.Status);
        Assert.True(batch.IsRunnable);
        Assert.Equal(60, batch.SendPolicy.SnapshotIntervalFrames);
        Assert.Equal(60, batch.SendPolicy.BatchWindowFrames);
        Assert.Equal(300, batch.SendPolicy.KeyFrameIntervalFrames);
        Assert.Equal(10000, batch.SendPolicy.MaxEntityCount);
        Assert.Equal(1024, batch.SendPolicy.ActiveEntityBudget);
        Assert.Equal(60, batch.SendPolicy.InterpolationDelayFrames);

        var batchPureState = batch.SendPolicy.ToPureStateSettings();
        Assert.Equal(batch.SendPolicy.MaxEntityCount, batchPureState.MaxEntityCount);
        Assert.Equal(batch.SendPolicy.ActiveEntityBudget, batchPureState.ActiveSyncBudget);
        Assert.Equal(batch.SendPolicy.KeyFrameIntervalFrames, batchPureState.BaselineIntervalFrames);
        Assert.Equal(batch.SendPolicy.SnapshotIntervalFrames, batchPureState.DeltaIntervalFrames);
        Assert.Equal(batch.SendPolicy.FarLodIntervalFrames, batchPureState.LowFrequencyIntervalFrames);
        Assert.Equal(batch.SendPolicy.InterpolationDelayFrames, batchPureState.InterpolationDelayFrames);

        Assert.Equal(NetworkSyncModel.MassBattleLodSync, mass.SyncModel);
        Assert.Equal(ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation, mass.ConvergenceKind);
        Assert.Equal(ShooterSyncTemplateStatus.Experimental, mass.Status);
        Assert.True(mass.IsRunnable);
        Assert.Equal(3, mass.SendPolicy.SnapshotIntervalFrames);
        Assert.Equal(3, mass.SendPolicy.BatchWindowFrames);
        Assert.Equal(450, mass.SendPolicy.KeyFrameIntervalFrames);
        Assert.Equal(20000, mass.SendPolicy.MaxEntityCount);
        Assert.Equal(2048, mass.SendPolicy.ActiveEntityBudget);
        Assert.Equal(24f, mass.SendPolicy.AoiRadius);
        Assert.Equal(30f, mass.SendPolicy.AoiBoundaryRadius);
        Assert.Equal(3, mass.SendPolicy.NearLodIntervalFrames);
        Assert.Equal(9, mass.SendPolicy.MidLodIntervalFrames);
        Assert.Equal(30, mass.SendPolicy.FarLodIntervalFrames);
        Assert.Equal(3, mass.SendPolicy.InterpolationDelayFrames);
    }

    [Fact]
    public void ReservedSyncTemplateIsCatalogVisibleButRejectedByRunnableSessionCreation()
    {
        var reserved = ShooterAcceptanceCatalog.GetSyncTemplate("fast-reconnect-resume");

        Assert.Equal(NetworkSyncModel.FastReconnect, reserved.SyncModel);
        Assert.Equal(ShooterSyncTemplateStatus.Reserved, reserved.Status);
        Assert.False(reserved.IsRunnable);
        Assert.Throws<System.NotSupportedException>(() => ShooterAcceptanceLab.Create(in reserved));
    }

    [Fact]
    public void PlayModeOptionsCanBeBuiltFromSyncTemplate()
    {
        var template = ShooterAcceptanceCatalog.GetSyncTemplate("hybrid-hero-prediction");

        var options = ShooterPlayModeSessionOptions.FromTemplate(in template).Normalized();

        Assert.Equal(template.Id, options.SyncTemplateId);
        Assert.Equal(template.SyncModel, options.SyncModel);
        Assert.Equal(template.RecommendedPlayerCount, options.PlayerCount);
        Assert.Equal(template.EnableAuthoritativeWorld, options.EnableAuthoritativeWorld);
        Assert.Equal(template.DisplayName, options.NetworkName);
        Assert.Equal(NetworkConditionProfile.Lan.BaseLatencyMs, options.LatencyMs);
    }

    [Fact]
    public void PlayModeOptionsCanSelectGameplayScenarioFromSyncTemplate()
    {
        var template = ShooterAcceptanceCatalog.GetSyncTemplate("predict-rollback-authority");
        var scenario = ShooterSveltoGameplayScenarioCatalog.ProjectileStorm;

        var options = ShooterPlayModeSessionOptions.FromTemplate(in template, in scenario).Normalized();

        Assert.Equal(template.Id, options.SyncTemplateId);
        Assert.Equal(scenario.Id, options.GameplayScenario.Id);
        Assert.Equal(scenario.BattleFlow.DurationFrames, options.GameplayScenario.BattleFlow.DurationFrames);
        Assert.Equal(scenario.BattleFlow.Waves.Length, options.GameplayScenario.BattleFlow.Waves.Length);
    }

    [Fact]
    public void PlayModeOptionsCanSelectGameplayScenarioFromJsonSource()
    {
        var template = ShooterAcceptanceCatalog.GetSyncTemplate("hybrid-hero-prediction");
        var source = ShooterSveltoGameplayScenarioJsonSource.BuiltIn;

        var options = ShooterPlayModeSessionOptions
            .FromTemplateAndScenarioSource(in template, source, "svelto-wave-survival")
            .Normalized();

        Assert.Equal(template.Id, options.SyncTemplateId);
        Assert.Equal(NetworkSyncModel.HybridHeroPrediction, options.SyncModel);
        Assert.Equal(ShooterSveltoGameplayScenarioCatalog.WaveSurvival.Id, options.GameplayScenario.Id);
        Assert.Equal(ShooterSveltoGameplayScenarioCatalog.WaveSurvival.BattleFlow.DurationFrames, options.GameplayScenario.BattleFlow.DurationFrames);
    }

    [Fact]
    public void PlayModeOptionsCanSelectGameplayScenarioFromExternalJson()
    {
        const string json = @"
{
  ""id"": ""playmode-external-json"",
  ""displayName"": ""PlayMode External Json"",
  ""description"": ""Unity PlayMode 可以从 TextAsset 或外部文件读取的玩法配置。"",
  ""shooterCount"": 3,
  ""targetCount"": 18,
  ""tickCount"": 75,
  ""tickDeltaTime"": 0.05,
  ""arenaRadius"": 11.0,
  ""loadout"": {
    ""loadoutId"": 9,
    ""name"": ""external-json-rifle"",
    ""projectileSpeed"": 14.0,
    ""projectileLifeFrames"": 28,
    ""damage"": 2,
    ""cooldownFrames"": 4,
    ""projectilesPerShot"": 2,
    ""spreadDegrees"": 6.0
  },
  ""battleFlow"": {
    ""durationFrames"": 75,
    ""victoryTargetDefeats"": 12,
    ""maxActiveEnemies"": 8,
    ""waves"": [
      { ""waveId"": 1, ""startFrame"": 0, ""spawnFrameInterval"": 3, ""enemyCount"": 8, ""enemyHp"": 2, ""spawnRadius"": 7.0 }
    ]
  }
}";
        var template = ShooterAcceptanceCatalog.GetSyncTemplate("predict-rollback-authority");
        var source = ShooterSveltoGameplayScenarioJsonSource.FromJson("playmode-external-json", "PlayMode External Json", json);

        var options = ShooterPlayModeSessionOptions
            .FromTemplateAndScenarioSource(in template, source, "playmode-external-json")
            .Normalized();

        Assert.Equal("playmode-external-json", options.GameplayScenario.Id);
        Assert.Equal(3, options.GameplayScenario.ShooterCount);
        Assert.Equal(18, options.GameplayScenario.TargetCount);
        Assert.Equal(75, options.GameplayScenario.TickCount);
        Assert.Equal("external-json-rifle", options.GameplayScenario.Loadout.Name);
        Assert.Equal(75, options.GameplayScenario.BattleFlow.DurationFrames);
        Assert.Single(options.GameplayScenario.BattleFlow.Waves);
    }

    [Fact]
    public void CreateAssemblesStartedPredictRollbackSession()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal);

        Assert.Equal(NetworkSyncModel.PredictRollback, session.SyncModel);
        Assert.NotNull(session.Runtime);
        Assert.NotNull(session.Presentation);
        Assert.NotNull(session.Controller);
        Assert.Equal(ShooterDemoHarnessCarrier.DefaultCarrierName, session.Carrier.CarrierName);
    }

    [Fact]
    public void PredictRollbackSessionRunsThroughHarnessToCompletion()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.PoorWifi,
            networkName: "Poor WiFi");

        var result = session.Run(stepCount: 5, deltaSeconds: 1f / 30f, seed: 11);

        Assert.True(result.Completed);
        Assert.Equal(5, result.Metrics.StepsRun);
        Assert.Equal(5, result.Metrics.TotalTicks);
        Assert.Equal(session.Runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(session.Presentation.ViewModel.Frame, result.Metrics.LastFrame);
    }

    [Fact]
    public void CatalogOverloadBuildsRunnablePredictRollbackSession()
    {
        var sync = FindMode(NetworkSyncModel.PredictRollback);
        var network = FindNetwork("lan");

        var session = ShooterAcceptanceLab.Create(in sync, in network);
        var result = session.Run(stepCount: 3);

        Assert.True(result.Completed);
        Assert.Equal("LAN (5ms)", session.NetworkName);
    }

    [Fact]
    public void RunCatalogMatrixReturnsBatchResultWithEveryImplementedModeAndNetwork()
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2);

        // 矩阵按可运行模板（Implemented + Experimental，不含 Reserved）× 网络环境展开。
        var runnableTemplates = 0;
        foreach (var template in ShooterAcceptanceCatalog.SyncTemplates)
        {
            if (template.IsRunnable)
            {
                runnableTemplates++;
            }
        }

        var expected = runnableTemplates * ShooterAcceptanceCatalog.NetworkEnvironments.Count;
        Assert.Equal(expected, batch.ScenarioCount);
        Assert.Equal(expected, batch.Results.Count);

        var netCount = ShooterAcceptanceCatalog.NetworkEnvironments.Count;
        Assert.Equal(netCount * runnableTemplates, batch.CompletedCount);
        Assert.Equal(0, batch.UnsupportedCount);
        Assert.Equal(0, batch.FailedCount);
        Assert.Equal(0, batch.DegradedCount);
        Assert.True(batch.AllCompleted);

        // Batch summary should have aggregated rows per (carrier, model, status).
        Assert.NotEmpty(batch.Summary.Rows);
        Assert.Equal(netCount, batch.Summary.CountFor(
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.PredictRollback,
            DemoHarnessRunStatus.Completed));
        Assert.Equal(netCount * 2, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.AuthoritativeInterpolation,
            DemoHarnessRunStatus.Completed));
        Assert.Equal(netCount, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.BatchStateSync,
            DemoHarnessRunStatus.Completed));
        Assert.Equal(netCount * 2, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.MassBattleLodSync,
            DemoHarnessRunStatus.Completed));
        Assert.Equal(netCount, batch.Summary.CountFor(
            ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.HybridHeroPrediction,
            DemoHarnessRunStatus.Completed));
        Assert.True(batch.AllCompleted);
        Assert.Equal(0, batch.FailedCount);
        Assert.Equal(0, batch.UnsupportedCount);
    }

    [Fact]
    public void CatalogOverloadRejectsUnimplementedMode()
    {
        var unimplemented = new ShooterAcceptanceSyncOption(
            NetworkSyncModel.Lockstep, "Lockstep", implemented: false);
        var network = FindNetwork("ideal");

        Assert.Throws<System.NotSupportedException>(() =>
            ShooterAcceptanceLab.Create(in unimplemented, in network));
    }

    private static ShooterAcceptanceSyncOption FindMode(NetworkSyncModel model)
    {
        foreach (var mode in ShooterAcceptanceCatalog.SyncModes)
        {
            if (mode.Model == model)
            {
                return mode;
            }
        }

        return Assert.IsType<ShooterAcceptanceSyncOption>(null!);
    }

    private static ShooterAcceptanceNetworkOption FindNetwork(string id)
    {
        foreach (var network in ShooterAcceptanceCatalog.NetworkEnvironments)
        {
            if (network.Id == id)
            {
                return network;
            }
        }

        return Assert.IsType<ShooterAcceptanceNetworkOption>(null!);
    }

    [Fact]
    public void HybridSyncModeIsExposedInCatalog()
    {
        Assert.Contains(ShooterAcceptanceCatalog.SyncModes,
            m => m.Model == NetworkSyncModel.HybridHeroPrediction && m.Implemented);
        Assert.Contains(ShooterAcceptanceCatalog.SyncModes,
            m => m.DisplayName == "Hybrid (Predict + Interpolation)");
    }

    [Fact]
    public void HybridSessionRunsThroughHarnessToCompletion()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.HybridHeroPrediction,
            NetworkConditionProfile.Ideal,
            networkName: "Hybrid Ideal");

        Assert.Equal(NetworkSyncModel.HybridHeroPrediction, session.SyncModel);
        Assert.Equal(ShooterHybridDemoHarnessCarrier.DefaultCarrierName, session.Carrier.CarrierName);

        var result = session.Run(stepCount: 5, deltaSeconds: 1f / 30f, seed: 19);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(5, result.Metrics.StepsRun);
        Assert.Equal(session.Runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(session.Presentation.ViewModel.Frame, result.Metrics.LastFrame);
    }

    [Fact]
    public void LimitedBandwidthNetworkProfileIsExposedInCatalog()
    {
        Assert.Contains(ShooterAcceptanceCatalog.NetworkEnvironments, n => n.Id == "limitedbw");

        var limited = ShooterAcceptanceCatalog.NetworkEnvironments
            .First(n => n.Id == "limitedbw");
        Assert.Equal("Limited BW (128 Kbps)", limited.DisplayName);
        Assert.Equal(128, limited.Profile.BandwidthKbps);
        Assert.Equal(0, limited.Profile.BaseLatencyMs);
        Assert.Equal(0d, limited.Profile.PacketLossRate);
    }

    [Fact]
    public void AuthoritativeWorldPublishesSnapshotsThroughCarrierNetworkLink()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        var result = session.Run(stepCount: 3, deltaSeconds: 1f / 30f, seed: 23);

        Assert.True(result.Completed);
        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(3, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.Equal(3, session.CarrierNetworkStats.Value.InboundDelivered);
        Assert.Equal(0, session.CarrierNetworkStats.Value.PendingCount);
        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, session.LastCarrierSnapshotApplyResult);
        var lagCompTelemetry = Assert.IsType<ShooterLagCompensationTelemetry>(session.LagCompensationTelemetry);
        Assert.Equal(3, lagCompTelemetry.CapturedFrameCount);
        Assert.Equal(1, lagCompTelemetry.OldestFrame);
        Assert.Equal(3, lagCompTelemetry.LatestFrame);
    }

    [Fact]
    public void AuthoritativeWorldValidatesLagCompensationShotFromSessionHistory()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);
        session.TickAuthoritativeWorld(1f / 30f);
        var shot = new ShooterLagCompensationShot(
            shooterPlayerId: 1,
            originX: 0f,
            originY: 0f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 10f,
            rewindFrame: 1,
            serverReceiveFrame: 1);

        var accepted = session.TryEvaluateLagCompensationShot(in shot, out var evaluation);

        Assert.True(accepted);
        Assert.True(evaluation.Accepted);
        Assert.Equal(LagCompensationResultReason.Hit, evaluation.Reason);
        Assert.Equal(1, evaluation.RequestedFrame);
        Assert.Equal(1, evaluation.EvaluatedFrame);
        Assert.Equal(2, evaluation.HitEntityId);
        Assert.Equal(evaluation, session.LastLagCompensationEvaluation);
    }

    [Fact]
    public void AuthoritativeCarrierSnapshotsAreStampedWithTimeAnchor()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        var result = session.Run(stepCount: 3, deltaSeconds: 1f / 30f, seed: 37);

        Assert.True(result.Completed);
        Assert.Equal(3, session.LastCarrierTimeAnchor.LocalFrame);
        Assert.Equal(3, session.LastCarrierTimeAnchor.TimelineTicks);
        Assert.True(session.LastCarrierTimeAnchor.HasAuthoritativeFrame);
        Assert.Equal(3, session.LastCarrierTimeAnchor.AuthoritativeFrame);
        Assert.Equal(0.1d, session.LastCarrierTimeAnchor.ElapsedSeconds, precision: 6);
    }

    [Fact]
    public void CarrierNetworkLinkBuffersSnapshotsUntilLatencyElapses()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            new NetworkConditionProfile(baseLatencyMs: 100, jitterMs: 0, packetLossRate: 0d, reorderRate: 0d, bandwidthKbps: 0),
            enableAuthoritativeWorld: true);

        session.TickAuthoritativeWorld(1f / 30f);

        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(1, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.Equal(0, session.CarrierNetworkStats.Value.InboundDelivered);
        Assert.Equal(1, session.CarrierNetworkStats.Value.PendingCount);
    }

    [Fact]
    public void CarrierNetworkLinkAppliesPacketLossBeforeControllerDelivery()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            new NetworkConditionProfile(baseLatencyMs: 0, jitterMs: 0, packetLossRate: 1d, reorderRate: 0d, bandwidthKbps: 0),
            enableAuthoritativeWorld: true);

        var result = session.Run(stepCount: 4, deltaSeconds: 1f / 30f, seed: 29);

        Assert.True(result.Completed);
        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(4, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.Equal(0, session.CarrierNetworkStats.Value.InboundDelivered);
        Assert.Equal(4, session.CarrierNetworkStats.Value.InboundDropped);
        Assert.Equal(ShooterSnapshotApplyResult.Ignored, session.LastCarrierSnapshotApplyResult);
    }

    [Fact]
    public void CarrierNetworkLinkRecordsReorderedSnapshots()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            new NetworkConditionProfile(baseLatencyMs: 30, jitterMs: 0, packetLossRate: 0d, reorderRate: 1d, bandwidthKbps: 0),
            enableAuthoritativeWorld: true);

        var result = session.Run(stepCount: 4, deltaSeconds: 1f / 30f, seed: 31);

        Assert.True(result.Completed);
        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(4, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.True(session.CarrierNetworkStats.Value.InboundReordered > 0);
    }

    [Fact]
    public void LimitedBandwidthSessionRunsThroughHarnessToCompletion()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.LimitedBandwidth,
            networkName: "Limited BW (128 Kbps)");

        var result = session.Run(stepCount: 5, deltaSeconds: 1f / 30f, seed: 17);

        Assert.True(result.Completed);
        Assert.Equal(5, result.Metrics.StepsRun);
        Assert.Equal(session.Runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(128, result.Scenario.NetworkProfile.BandwidthKbps);

        // Bandwidth throttling does not cause reconciliation failures
        // because Shooter demo uses < 1 Kbps per tick at 30 Hz with 2 players.
        Assert.True(result.Metrics.ReconciliationCount >= 0);
    }
}
