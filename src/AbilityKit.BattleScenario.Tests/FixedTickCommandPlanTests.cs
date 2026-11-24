using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.BattleScenario.Tests;

public sealed class FixedTickCommandPlanTests
{
    [Fact]
    public void Compile_UsesExplicitTickRateAndPreservesDeclarationOrderWithinTick()
    {
        var scenario = new TestScenario
        {
            CaseId = "ordering",
            TickRate = 60,
            TimeoutMs = 90,
            Execution = new TestExecutionSpec { TickRate = 30, MaxDurationMs = 200, SettleDurationMs = 0 },
            Commands = new[]
            {
                new TestCommand { AtMs = 200, Name = "first", Parameters = new Dictionary<string, string> { ["value"] = "1" } },
                new TestCommand { AtMs = 100, Name = "earlier", Parameters = new Dictionary<string, string> { ["value"] = "2" } },
                new TestCommand { AtMs = 200, Name = "first", Parameters = new Dictionary<string, string> { ["value"] = "3" } },
            },
        };

        var plan = FixedTickCommandPlan.Compile<int>(scenario, Bind);

        Assert.Equal(new[] { ("earlier", 100, 3, 1, 2), ("first", 200, 6, 0, 1), ("first", 200, 6, 2, 3) },
            plan.Select(step => (step.Name, step.AtMs, step.Tick, step.SourceIndex, step.Action)));

        static bool Bind(TestCommand command, out int action) => int.TryParse(command.Parameters["value"], out action);
    }

    [Fact]
    public void Compile_ReportsAllInvalidCommandsAndDoesNotReturnPartialPlan()
    {
        var scenario = new TestScenario
        {
            CaseId = "preflight-errors",
            Execution = new TestExecutionSpec { TickRate = 100, MaxDurationMs = 100, SettleDurationMs = 0 },
            Commands = new[]
            {
                new TestCommand { Name = "known", AtMs = 0 },
                new TestCommand { Name = "known", AtMs = 11 },
                new TestCommand { Name = "known", AtMs = 110 },
                new TestCommand { Name = "unknown", AtMs = 100 },
            },
        };
        var executed = 0;

        var error = Assert.Throws<InvalidOperationException>(Execute);

        Assert.Contains("preflight-errors", error.Message);
        Assert.Contains("commands[1] 'known' @ 11ms", error.Message);
        Assert.Contains("commands[2].atMs exceeds execution.maxDurationMs", error.Message);
        Assert.Contains("commands[3] 'unknown' @ 100ms", error.Message);
        Assert.Equal(0, executed);

        void Execute()
        {
            foreach (var step in FixedTickCommandPlan.Compile<Action>(scenario, Bind))
                step.Action();
        }

        bool Bind(TestCommand command, out Action action)
        {
            action = () => executed++;
            return command.Name == "known";
        }
    }
}
