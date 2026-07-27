using AbilityKit.Game.Battle;
using Xunit;

namespace AbilityKit.Game.Battle.Runtime.Tests;

public sealed class BattleCompositionContractTests
{
    [Fact]
    public void RuntimeStatus_RequiresCapabilitiesAndAllowedState()
    {
        var status = new BattleRuntimeStatus(
            BattleRuntimeCapability.GameStart |
            BattleRuntimeCapability.Input |
            BattleRuntimeCapability.Simulation |
            BattleRuntimeCapability.SnapshotOutput,
            BattleRuntimeState.Ready);

        Assert.True(status.Meets(BattleReadinessRequirement.GameStart));
        Assert.True(status.Meets(BattleReadinessRequirement.BattleLoop));

        var running = new BattleRuntimeStatus(status.Capabilities, BattleRuntimeState.Running);
        Assert.False(running.Meets(BattleReadinessRequirement.GameStart));
        Assert.True(running.Meets(BattleReadinessRequirement.BattleLoop));
    }

    [Fact]
    public void ContributionPlanner_UsesDependenciesThenStableOrder()
    {
        var contributions = new IBattleSystemContribution<string, string>[]
        {
            Contribute("late", 300),
            Contribute("stable.first", 100),
            Contribute("forced.after", 0, runsAfter: new[] { "late" }),
            Contribute("stable.second", 100),
        };

        var plan = BattleSystemContributionPlanner.Create<string, string>(contributions);

        Assert.Equal(
            new[] { "stable.first", "stable.second", "late", "forced.after" },
            plan.OrderedContributions.Select(item => item.Id));
        Assert.Equal(
            new[] { "stable.first:ctx", "stable.second:ctx", "late:ctx", "forced.after:ctx" },
            plan.CreateSystems("ctx"));
    }

    [Fact]
    public void ContributionPlanner_ReportsMissingAndCyclicDependencies()
    {
        var missing = Assert.Throws<InvalidOperationException>(() =>
            BattleSystemContributionPlanner.Create<string, string>(new[]
            {
                Contribute("input", 0, runsAfter: new[] { "missing" }),
            }));
        Assert.Contains("missing", missing.Message);

        var cycle = Assert.Throws<InvalidOperationException>(() =>
            BattleSystemContributionPlanner.Create<string, string>(new[]
            {
                Contribute("input", 0, runsAfter: new[] { "output" }),
                Contribute("output", 0, runsAfter: new[] { "input" }),
            }));
        Assert.Contains("input", cycle.Message);
        Assert.Contains("output", cycle.Message);
    }

    [Fact]
    public void StageGraph_OrdersDagAndReportsAvailableStages()
    {
        var graph = BattleStageGraph.Create(new[]
        {
            new BattleStageDefinition("combat", 200, new[] { "load" }),
            new BattleStageDefinition("diagnostics", 100, new[] { "load" }),
            new BattleStageDefinition("load", 0),
            new BattleStageDefinition("complete", 300, new[] { "combat", "diagnostics" }),
        });

        Assert.Equal(
            new[] { "load", "diagnostics", "combat", "complete" },
            graph.OrderedStages.Select(stage => stage.Id));
        Assert.Equal(new[] { "load" }, graph.GetAvailableStages(new HashSet<string>()).Select(stage => stage.Id));
        Assert.Equal(
            new[] { "diagnostics", "combat" },
            graph.GetAvailableStages(new HashSet<string> { "load" }).Select(stage => stage.Id));
    }

    [Fact]
    public void ValidationRegistry_UsesStableOrderAndAggregatesFindings()
    {
        var calls = new List<string>();
        var registry = new BattleValidationRegistry<List<string>>();
        registry.Register(new Validator("late", 100, BattleValidationSeverity.Warning, false));
        registry.Register(new Validator("first", 0, BattleValidationSeverity.Error, true));
        registry.Register(new Validator("second", 100, BattleValidationSeverity.Info, false));

        var report = registry.Validate(calls);

        Assert.Equal(new[] { "first", "late", "second" }, calls);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(1, report.InfoCount);
        Assert.True(report.BlocksStartup);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void HealthReporter_AggregatesWorstLevelAndPreservesEntries()
    {
        var report = BattleHealthReporter.Collect(new IBattleHealthProvider[]
        {
            new HealthProvider("runtime", BattleHealthLevel.Healthy),
            new HealthProvider("sync", BattleHealthLevel.Degraded),
            new HealthProvider("validation", BattleHealthLevel.Unhealthy),
        });

        Assert.Equal(BattleHealthLevel.Unhealthy, report.Level);
        Assert.False(report.IsHealthy);
        Assert.Equal(3, report.Entries.Count);
    }

    private static BattleSystemContribution<string, string> Contribute(
        string id,
        int order,
        IReadOnlyList<string>? runsAfter = null)
    {
        return new BattleSystemContribution<string, string>(
            id,
            order,
            context => id + ":" + context,
            runsAfter ?? Array.Empty<string>());
    }

    private sealed class Validator : IBattleValidator<List<string>>
    {
        private readonly BattleValidationSeverity _severity;
        private readonly bool _blocksStartup;

        public Validator(string name, int order, BattleValidationSeverity severity, bool blocksStartup)
        {
            Name = name;
            Order = order;
            _severity = severity;
            _blocksStartup = blocksStartup;
        }

        public string Name { get; }

        public int Order { get; }

        public void Validate(List<string> context, BattleValidationReport report)
        {
            context.Add(Name);
            report.Add(new BattleValidationFinding(Name, Name + ".code", _severity, Name, _blocksStartup));
        }
    }

    private sealed class HealthProvider : IBattleHealthProvider
    {
        private readonly BattleHealthLevel _level;

        public HealthProvider(string name, BattleHealthLevel level)
        {
            Name = name;
            _level = level;
        }

        public string Name { get; }

        public BattleHealthEntry CollectHealth()
        {
            return new BattleHealthEntry(Name, _level, Name);
        }
    }
}
