using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBattleCompositionAdapterTests
{
    [Fact]
    public void ValidationProjectionPreservesSeverityCountsAndStartupBlocking()
    {
        var source = new MobaRuntimeValidationReport();
        source.Info("runtime", "summary", "ready", code: "moba.ready");
        source.Warning("runtime", "trace", "retained", code: "moba.trace.retained");
        source.Error("runtime", "config", "missing", blocksStartup: true, code: "moba.config.missing");

        var report = MobaBattleCompositionAdapters.ToBattleValidationReport(source);

        Assert.Equal(1, report.InfoCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(1, report.ErrorCount);
        Assert.True(report.BlocksStartup);
        Assert.Equal("moba.config.missing", report.Findings[2].Code);
        Assert.Equal(BattleValidationSeverity.Error, report.Findings[2].Severity);
        Assert.True(report.Findings[2].BlocksStartup);
    }

    [Fact]
    public void HealthProjectionMapsErrorsToUnhealthyAndExportsMetrics()
    {
        var summary = CreateSummary(
            hasSkillRuntime: true,
            hasTraceRegistry: true,
            validationBlocksStartup: true,
            validationErrors: 2,
            validationWarnings: 1);

        var entry = MobaBattleCompositionAdapters.ToBattleHealthEntry(in summary);

        Assert.Equal(BattleHealthLevel.Unhealthy, entry.Level);
        Assert.Equal(MobaRuntimeHealthSummaryValidator.SourceName, entry.Source);
        Assert.Equal(2d, entry.Metrics["validation.errors"]);
        Assert.Equal(1d, entry.Metrics["validation.blocks_startup"]);
    }

    [Fact]
    public void HealthProjectionDistinguishesUnknownDegradedAndHealthy()
    {
        var unknown = CreateSummary(hasSkillRuntime: false, hasTraceRegistry: false);
        var degraded = CreateSummary(hasSkillRuntime: true, hasTraceRegistry: true, validationWarnings: 1);
        var healthy = CreateSummary(hasSkillRuntime: true, hasTraceRegistry: true);

        Assert.Equal(BattleHealthLevel.Unknown, MobaBattleCompositionAdapters.ToBattleHealthEntry(in unknown).Level);
        Assert.Equal(BattleHealthLevel.Degraded, MobaBattleCompositionAdapters.ToBattleHealthEntry(in degraded).Level);
        Assert.Equal(BattleHealthLevel.Healthy, MobaBattleCompositionAdapters.ToBattleHealthEntry(in healthy).Level);
    }

    [Fact]
    public void RuntimeHealthValidatorCanParticipateInGenericHealthAggregation()
    {
        IBattleHealthProvider provider = new MobaRuntimeHealthSummaryValidator();

        var report = BattleHealthReporter.Collect(new[] { provider });

        Assert.Equal(BattleHealthLevel.Unknown, report.Level);
        Assert.Single(report.Entries);
        Assert.Equal(MobaRuntimeHealthSummaryValidator.SourceName, report.Entries[0].Source);
    }

    private static MobaRuntimeHealthSummary CreateSummary(
        bool hasSkillRuntime,
        bool hasTraceRegistry,
        bool validationBlocksStartup = false,
        int validationErrors = 0,
        int validationWarnings = 0)
    {
        return new MobaRuntimeHealthSummary(
            hasSkillRuntime,
            activeSkillRuntimes: 3,
            waitingSkillRuntimes: 0,
            pendingSkillChildren: 0,
            hasTraceRegistry,
            traceRoots: 4,
            activeTraceRoots: 2,
            retainedTraceRoots: 0,
            retainedEndedTraceRoots: 0,
            staleRetainedTraceRoots: 0,
            hasValidationHistory: true,
            validationBlocksStartup,
            validationErrors,
            validationWarnings,
            validationInfos: 1);
    }
}
