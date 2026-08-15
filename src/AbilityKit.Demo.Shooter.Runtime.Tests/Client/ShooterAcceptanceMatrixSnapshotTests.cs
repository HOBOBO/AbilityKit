using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Demo.Harness;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

/// <summary>
/// Golden-baseline tests that lock down the expected Shooter acceptance matrix distribution.
/// When a sync mode is added, a network environment is added,
/// or an existing combination changes status (e.g. a mode that was Unsupported becomes
/// Degraded), these tests will fail, forcing the developer to consciously update the
/// baseline rather than silently accepting a regression.
/// </summary>
[Collection("ShooterAcceptance")]
public sealed class ShooterAcceptanceMatrixSnapshotTests
{
    [Fact]
    public void FullMatrixSnapshotMatchesExpectedBaseline()
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2);

        // === Golden baseline ===
        // Catalog: 5 runnable modes × 6 network environments = 30 scenarios.
        // PredictRollback: 6 Completed (ShooterDemoHarnessCarrier supports it).
        // AuthoritativeInterpolation: 6 Completed (ShooterInterpolationDemoHarnessCarrier supports it).
        // BatchStateSync: 6 Completed through the low-frequency interpolation-compatible carrier.
        // MassBattleLodSync: 6 Completed through the LOD/batch interpolation-compatible carrier.
        // HybridHeroPrediction: 6 Completed (ShooterHybridDemoHarnessCarrier requires the dedicated Hybrid controller).

        Assert.Equal(30, batch.ScenarioCount);
        Assert.Equal(30, batch.CompletedCount);
        Assert.Equal(0, batch.DegradedCount);
        Assert.Equal(0, batch.UnsupportedCount);
        Assert.Equal(0, batch.FailedCount);
        Assert.True(batch.AllCompleted);
    }

    [Fact]
    public void BatchSummaryRowsAggregateByCarrierModelAndStatus()
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2);

        // PredictRollback × 6 Completed + AuthoritativeInterpolation × 6 Completed
        // + BatchStateSync × 6 Completed + MassBattleLodSync × 6 Completed
        // + HybridHeroPrediction × 6 Completed = 5 summary rows.
        Assert.Equal(5, batch.Summary.Rows.Count);

        // PredictRollback: 6 Completed.
        Assert.Equal(6, batch.Summary.CountFor(
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.PredictRollback,
            DemoHarnessRunStatus.Completed));

        // AuthoritativeInterpolation: 6 Completed.
        Assert.Equal(6, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.AuthoritativeInterpolation,
            DemoHarnessRunStatus.Completed));

        // BatchStateSync: 6 Completed.
        Assert.Equal(6, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.BatchStateSync,
            DemoHarnessRunStatus.Completed));

        // MassBattleLodSync: 6 Completed.
        Assert.Equal(6, batch.Summary.CountFor(
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.MassBattleLodSync,
            DemoHarnessRunStatus.Completed));

        // HybridHeroPrediction: 6 Completed.
        Assert.Equal(6, batch.Summary.CountFor(
            ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.HybridHeroPrediction,
            DemoHarnessRunStatus.Completed));

        // No other statuses for PredictRollback.
        Assert.Equal(0, batch.Summary.CountFor(
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.PredictRollback,
            DemoHarnessRunStatus.Failed));
        Assert.Equal(0, batch.Summary.CountFor(
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.PredictRollback,
            DemoHarnessRunStatus.Unsupported));
        Assert.Equal(0, batch.Summary.CountFor(
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            NetworkSyncModel.PredictRollback,
            DemoHarnessRunStatus.Degraded));
    }

    [Fact]
    public void MatrixScenarioCountFollowsRunnableTemplateCatalogAndNetworks()
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2);
        var runnableTemplateCount = 0;
        foreach (var template in ShooterAcceptanceCatalog.SyncTemplates)
        {
            if (template.IsRunnable)
            {
                runnableTemplateCount++;
            }
        }

        var expectedScenarioCount = runnableTemplateCount * ShooterAcceptanceCatalog.NetworkEnvironments.Count;

        Assert.Equal(expectedScenarioCount, batch.ScenarioCount);
        Assert.DoesNotContain(batch.Results, result => result.Scenario.SyncModel == NetworkSyncModel.FastReconnect);
    }

    [Fact]
    public void PerResultMetricsArePopulatedForEveryCompletedScenario()
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 3, deltaSeconds: 1f / 30f);

        // All runnable catalog scenarios complete through their smoke-test carriers.
        foreach (var result in batch.Results)
        {
            Assert.True(result.Completed);
            Assert.Equal(3, result.Metrics.StepsRun);
            Assert.Equal(3, result.Metrics.TotalTicks);
            Assert.True(result.Metrics.LastFrame > 0);
            Assert.True(result.Metrics.ReconciliationCount >= 0);
            Assert.True(result.Metrics.NetworkStats.PendingCount >= 0);
        }
    }

    [Fact]
    public void SnapshotIsReproducibleWithDeterministicSeed()
    {
        var first = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2, seed: 42);
        var second = ShooterAcceptanceLab.RunCatalogMatrix(stepCount: 2, seed: 42);

        Assert.Equal(first.ScenarioCount, second.ScenarioCount);
        Assert.Equal(first.CompletedCount, second.CompletedCount);
        Assert.Equal(first.UnsupportedCount, second.UnsupportedCount);
        Assert.Equal(first.DegradedCount, second.DegradedCount);
        Assert.Equal(first.FailedCount, second.FailedCount);

        // Per-result metrics should be identical across runs.
        for (var i = 0; i < first.Results.Count; i++)
        {
            var a = first.Results[i];
            var b = second.Results[i];
            Assert.Equal(a.Status, b.Status);
            Assert.Equal(a.Metrics.StepsRun, b.Metrics.StepsRun);
            Assert.Equal(a.Metrics.TotalTicks, b.Metrics.TotalTicks);
            Assert.Equal(a.Metrics.LastFrame, b.Metrics.LastFrame);
            Assert.Equal(a.Metrics.ReconciliationCount, b.Metrics.ReconciliationCount);
        }
    }
}
