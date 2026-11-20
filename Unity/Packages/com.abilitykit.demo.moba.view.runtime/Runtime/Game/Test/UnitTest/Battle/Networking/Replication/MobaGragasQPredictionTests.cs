using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 在 Unity 无头 EditMode 的正式战斗世界验收已发布的古拉加斯 Q。
    /// </summary>
    public sealed class MobaGragasQPredictionTests
    {
        /// <summary>
        /// 分别从飞行与驻留状态连续纠偏两次，再用手动重施或自然到期结算。
        /// </summary>
        [TestCase("flight", true)]
        [TestCase("deployed", false)]
        public void PublishedQSettlesOnceAfterTwoCorrections(string checkpointPhase, bool manualDetonation) =>
            RunScenario(checkpointPhase, manualDetonation, correctAfterCleanup: false);

        /// <summary>
        /// 从正常清理后的世界请求倒回有弹体的检查点，验证失败边界保持状态不变。
        /// </summary>
        [TestCase("flight", true)]
        [TestCase("deployed", false)]
        public void PublishedQRejectsPostCleanupCorrectionBeforeImport(string checkpointPhase, bool manualDetonation) =>
            RunScenario(checkpointPhase, manualDetonation, correctAfterCleanup: true);

        /// <summary>
        /// 原施法实体已自然清理时，分别从弹体仍驻留和弹体已清理的世界尝试倒回。
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void PublishedQChecksLateDeploymentAfterCastEntityCleanup(bool correctAfterCleanup) =>
            RunScenario("deployed", manualDetonation: false, correctAfterCleanup: correctAfterCleanup,
                afterCastEntityCleanup: true);

        private static void RunScenario(string checkpointPhase, bool manualDetonation, bool correctAfterCleanup,
            bool afterCastEntityCleanup = false)
        {
            var scenario = new MobaAcceptanceExpectation
            {
                actors = new[]
                {
                    new MobaAcceptanceActorExpectation
                    {
                        playerId = "gragas_q_caster", heroId = 3005, attributeTemplateId = 3005,
                        basicAttackSkillId = 30050101,
                        skillIds = new[] { 30050101, 30050201, 30050301, 30050401, 30050501 },
                        spawnPosition = new MobaAcceptanceVector3Expectation { x = 100, z = 100 }
                    }
                }
            };
            using var harness = MobaSkillConfigTestHarness.CreateForScenario(scenario, worldId: "gragas_q_prediction");
            harness.EnterGameAndWarmup(10);
            var casterId = harness.AssertPlayerActorBound();
            var targetId = harness.SpawnScenarioActor("target", 0, "hero", 2, 1001, 1001,
                1, 0, 0, "gragas_q_target", 0, null, 0,
                new MobaAcceptanceVector3Expectation { x = 100, z = 106 });
            var caster = harness.AssertActorEntity(casterId);
            var target = harness.AssertActorEntity(targetId);
            if (caster.hasActorBrain) caster.RemoveActorBrain();
            if (target.hasActorBrain) target.RemoveActorBrain();
            harness.SetScenarioActorAttribute(targetId, "MAGIC_DEFENSE", 0);
            harness.SetScenarioActorAttribute(targetId, "PER_SECOND_BLOOD_R", 0);
            harness.Tick(1);
            GragasQPredictionReplayScenario.Run(harness.World, harness.Worlds, harness.PlayerId, casterId, targetId,
                checkpointPhase, manualDetonation, correct: !correctAfterCleanup, throughCleanup: true,
                fixedDelta: harness.FixedDelta, report: TestContext.Out.WriteLine, correctAfterCleanup: correctAfterCleanup,
                afterCastEntityCleanup: afterCastEntityCleanup);
        }
    }
}
