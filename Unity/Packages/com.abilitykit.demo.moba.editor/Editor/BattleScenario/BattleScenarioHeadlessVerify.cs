#if UNITY_EDITOR
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    /// <summary>headless 验证入口：`-executeMethod AbilityKit.Demo.Moba.Editor.BattleScenario.BattleScenarioHeadlessVerify.RunVerify` 调用，
    /// 跑一个场景的 shell-out（编译 → MobaBattleScenarioRunner.Run → .NET runner → 结果）并打印，附带断言积木累积验证。</summary>
    public static class BattleScenarioHeadlessVerify
    {
        public static void RunVerify()
        {
            var scenario = BattleScenarioCompiler.Compile("verify", new BattleBlock[]
            {
                new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
                new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
                new AssertTraceBlock { Kind = "DamageApply", ConfigId = 10010101 },
                new AssertStateBlock { Alias = "caster", Property = "hasBuff", Comparator = "eq", ExpectedValue = "true" },
            });

            var assertions = scenario.Expectations as MobaBattleScenarioAssertions;
            Debug.Log("[BattleScenarioVerify] mustContain=" + (assertions?.MustContain.Count ?? 0) + " state=" + (assertions?.State.Count ?? 0));

            var result = BattleScenarioRunnerRegistry.Runner!.Run(scenario);
            Debug.Log("[BattleScenarioVerify] passed=" + result.Passed + " summary=" + result.Summary);
        }
    }
}
#endif
