using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance.LiveSim;

public static class BattleScenarioPredictionRunner
{
    public static BattleScenarioPredictionRunResult Run(TestScenario scenario)
        => MobaBattleScenarioPredictionRunner.Run(scenario);

    public static MobaPredictionAssertionResult Verify(
        IReadOnlyList<MobaPredictionAssertion> assertions, BattleScenarioPredictionRunResult observation)
        => MobaBattleScenarioPredictionRunner.Verify(assertions, observation);
}
