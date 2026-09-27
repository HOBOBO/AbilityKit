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
            hasOptionalHealth: true,
            validationBlocksStartup: true,
            validationErrors: 2,
            validationWarnings: 1);

        var entry = MobaBattleCompositionAdapters.ToBattleHealthEntry(in summary);

        Assert.Equal(BattleHealthLevel.Unhealthy, entry.Level);
        Assert.Equal(MobaRuntimeHealthSummaryValidator.SourceName, entry.Source);
        Assert.Equal(2d, entry.Metrics["validation.errors"]);
        Assert.Equal(1d, entry.Metrics["validation.blocks_startup"]);
        Assert.Equal(1d, entry.Metrics["optional.available"]);
        Assert.Equal(4d, entry.Metrics["trace.roots"]);
    }

    [Fact]
    public void HealthProjectionDistinguishesUnknownDegradedAndHealthy()
    {
        var unknown = CreateSummary(hasSkillRuntime: false, hasOptionalHealth: false);
        var degraded = CreateSummary(hasSkillRuntime: true, hasOptionalHealth: true, validationWarnings: 1);
        var healthy = CreateSummary(hasSkillRuntime: true, hasOptionalHealth: true);
        var healthyWithoutOptionalHealth = CreateSummary(hasSkillRuntime: true, hasOptionalHealth: false);

        Assert.Equal(BattleHealthLevel.Unknown, MobaBattleCompositionAdapters.ToBattleHealthEntry(in unknown).Level);
        Assert.Equal(BattleHealthLevel.Degraded, MobaBattleCompositionAdapters.ToBattleHealthEntry(in degraded).Level);
        Assert.Equal(BattleHealthLevel.Healthy, MobaBattleCompositionAdapters.ToBattleHealthEntry(in healthy).Level);
        Assert.Equal(BattleHealthLevel.Healthy, MobaBattleCompositionAdapters.ToBattleHealthEntry(in healthyWithoutOptionalHealth).Level);
        Assert.Equal(0d, MobaBattleCompositionAdapters.ToBattleHealthEntry(in healthyWithoutOptionalHealth).Metrics["optional.available"]);
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
        bool hasOptionalHealth,
        bool validationBlocksStartup = false,
        int validationErrors = 0,
        int validationWarnings = 0)
    {
        var optionalHealth = hasOptionalHealth
            ? new MobaOptionalHealthContribution(
                "trace.observation",
                new Dictionary<string, double> { ["trace.roots"] = 4d },
                Array.Empty<MobaOptionalHealthFinding>())
            : default;

        return new MobaRuntimeHealthSummary(
            hasSkillRuntime,
            activeSkillRuntimes: 3,
            waitingSkillRuntimes: 0,
            pendingSkillChildren: 0,
            optionalHealth,
            hasValidationHistory: true,
            validationBlocksStartup,
            validationErrors,
            validationWarnings,
            validationInfos: 1);
    }
}
