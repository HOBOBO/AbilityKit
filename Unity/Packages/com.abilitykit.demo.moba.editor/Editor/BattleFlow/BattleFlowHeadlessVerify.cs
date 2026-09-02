#if UNITY_EDITOR
using AbilityKit.BattleFlow;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>headless 验证入口：`-executeMethod AbilityKit.Demo.Moba.Editor.BattleFlow.BattleFlowHeadlessVerify.RunVerify` 调用，
    /// 跑一个场景的 shell-out（编译 → MobaBattleFlowRunner.Run → .NET runner → 结果）并打印，附带断言积木累积验证。</summary>
    public static class BattleFlowHeadlessVerify
    {
        public static void RunVerify()
        {
            var scenario = BattleFlowCompiler.Compile("verify", new BattleBlock[]
            {
                new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
                new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
                new AssertTraceBlock { Kind = "DamageApply", ConfigId = 10010101 },
                new AssertStateBlock { Alias = "caster", Property = "hasBuff", Comparator = "eq", ExpectedValue = "true" },
            });

            var assertions = scenario.Expectations as MobaBattleFlowAssertions;
            Debug.Log("[BattleFlowVerify] mustContain=" + (assertions?.MustContain.Count ?? 0) + " state=" + (assertions?.State.Count ?? 0));

            var result = BattleFlowRunnerRegistry.Runner!.Run(scenario);
            Debug.Log("[BattleFlowVerify] passed=" + result.Passed + " summary=" + result.Summary);
        }
    }
}
#endif
