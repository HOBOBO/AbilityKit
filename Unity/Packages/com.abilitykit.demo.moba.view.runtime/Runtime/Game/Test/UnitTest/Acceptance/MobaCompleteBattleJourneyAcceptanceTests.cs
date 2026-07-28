using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaCompleteBattleJourneyAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract Daji = new HeroSkillContract(
            "Daji",
            heroId: 1005,
            attributeTemplateId: 1005,
            skillIds: new[] { 10050101, 10050201, 10050301 });

        [Test]
        public void DajiBattleJourney_ShouldCoverCombatDeathRespawnAndSettlement()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "moba_complete_battle_journey_world"))
            {
                harness.EnterGameAndWarmup(reason: "complete battle journey acceptance");
                var gameplay = harness.World.Services.Resolve<MobaGameplayService>();
                Assert.AreEqual(MobaGameplayPhase.Running, gameplay.Phase, "Formal game entry should start gameplay lifecycle.");

                var actorId = harness.AssertPlayerActorBound();
                harness.MoveScenarioActor(actorId, new MobaAcceptanceVector3Expectation { x = 0f, y = 0f, z = 0f });
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 6f);
                var targetHpBefore = harness.GetActorHp(targetActorId);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 2, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Daji skill 2 should enter the formal skill pipeline. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaTraceKind.EffectExecution,
                    configId: 10050201,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(10050201, 10050201, safetyFrames: 5) + 30,
                    message: "Daji skill 2 effect should execute during the battle journey.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, launcherId: 31050201, projectileId: 30050201);
                TickUntilProjectileHit(harness, targetActorId, targetHpBefore, buffId: 10050201, maxTicks: 90);

                var rules = harness.World.Services.Resolve<MobaCombatRulesService>();
                var lethal = HeroSkillHeadlessContract.ExecuteDamage(
                    harness,
                    actorId,
                    targetActorId,
                    baseDamage: harness.GetActorHp(targetActorId) * 10f + 1f,
                    reasonKind: DamageReasonKind.Environment);
                Assert.AreEqual(0f, lethal.TargetHp, 0.001f, "Lethal pipeline damage should enter the formal death path.");
                Assert.AreEqual(MobaCombatRuleFailure.Dead, rules.CanBeSearchedTarget(actorId, targetActorId).Failure);

                var lifecycle = harness.World.Services.Resolve<MobaUnitLifecycleService>();
                var respawnPosition = new Vec3(8f, 0f, 2f);
                var respawn = lifecycle.TryRespawn(targetActorId, in respawnPosition, healthRatio: 0.5f);
                Assert.IsTrue(respawn.Succeeded, "Dead hero should be accepted by the formal respawn transition.");
                Assert.Greater(respawn.RestoredHp, 0f);
                Assert.IsTrue(rules.CanBeSearchedTarget(actorId, targetActorId).Passed, "Respawned hero should re-enter combat rules.");
                Assert.AreEqual(respawnPosition.X, harness.AssertActorEntity(targetActorId).transform.Value.Position.X, 0.001f);
                Assert.AreEqual(respawnPosition.Z, harness.AssertActorEntity(targetActorId).transform.Value.Position.Z, 0.001f);

                var secondLethal = HeroSkillHeadlessContract.ExecuteDamage(
                    harness,
                    actorId,
                    targetActorId,
                    baseDamage: respawn.RestoredHp * 10f + 1f,
                    reasonKind: DamageReasonKind.Environment);
                Assert.AreEqual(0f, secondLethal.TargetHp, 0.001f, "Respawn must reset death de-duplication and allow a later death.");
                Assert.IsTrue(lifecycle.TryRespawn(targetActorId).Succeeded, "A second death should remain respawnable.");

                Assert.IsTrue(gameplay.End("team_defeated", winTeamId: 1), "The battle journey should reach formal settlement.");
                Assert.AreEqual(MobaGameplayPhase.Ended, gameplay.Phase);
                Assert.AreEqual(1, gameplay.LastResult.WinTeamId);
                Assert.AreEqual("team_defeated", gameplay.LastResult.Reason);
            }
        }

        private static void TickUntilProjectileHit(
            MobaSkillConfigTestHarness harness,
            int targetActorId,
            float hpBefore,
            int buffId,
            int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (harness.GetActorHp(targetActorId) < hpBefore && harness.HasActorBuff(targetActorId, buffId))
                {
                    return;
                }

                if (i < maxTicks)
                {
                    harness.Tick(1);
                }
            }

            Assert.Fail("Daji homing projectile should damage the target and apply its control Buff.");
        }
    }
}
