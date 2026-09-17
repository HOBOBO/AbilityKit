using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance.LiveSim;

public static class BattleFlowPredictionScenarioRunner
{
    public static BattleFlowPredictionRunResult Run(TestScenario scenario)
        => MobaBattleFlowPredictionScenarioRunner.Run(scenario);

    public static MobaPredictionAssertionResult Verify(
        IReadOnlyList<MobaPredictionAssertion> assertions, BattleFlowPredictionRunResult observation)
        => MobaBattleFlowPredictionScenarioRunner.Verify(assertions, observation);
}
