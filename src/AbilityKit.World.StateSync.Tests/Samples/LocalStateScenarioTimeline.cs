using AbilityKit.BattleScenario;
using AbilityKit.Scenario;

namespace AbilityKit.World.StateSync.Tests;

internal static class LocalStateScenarioTimeline
{
    private const string CommandPrefix = "local.statesync.";

    internal static TestScenario Load(string asset) => BattleScenarioCompiler.CompileFile(
        Path.Combine(AppContext.BaseDirectory, "Samples", "Scenarios", asset));

    internal delegate bool TryBindLocalCommand<TAction>(string name, out TAction action);

    internal static IReadOnlyList<FixedTickCommand<TAction>> Compile<TAction>(
        TestScenario scenario, TryBindLocalCommand<TAction> tryBind)
    {
        var execution = scenario.ResolveExecution();
        if (execution.EndCondition?.Kind != TestEndConditionKinds.TimelineComplete || execution.SettleDurationMs != 0 ||
            scenario.Timeline.Count != 0 || scenario.Setup.Count != 0 || scenario.Actors.Count != 0 ||
            scenario.Obstacles.Count != 0 || scenario.NetworkWatches.Count != 0 || scenario.Expectations != null)
            throw new InvalidOperationException("本地状态同步夹具只执行命令时间线，无静态世界或收尾阶段。");

        return FixedTickCommandPlan.Compile<TAction>(scenario, Bind);

        bool Bind(TestCommand command, out TAction action)
        {
            if (command.Name.StartsWith(CommandPrefix, StringComparison.Ordinal) &&
                command.Parameters.Count == 0 && command.ActorAlias == null && command.TargetAlias == null)
                return tryBind(command.Name[CommandPrefix.Length..], out action);

            action = default!;
            return false;
        }
    }

    internal static string LocalName(string name) => name[CommandPrefix.Length..];
}
