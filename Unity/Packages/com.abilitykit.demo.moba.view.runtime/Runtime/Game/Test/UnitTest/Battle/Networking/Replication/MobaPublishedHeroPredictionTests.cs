using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 通过正式 Unity 无头世界验证已发布技能的回放和后续结算。
    /// </summary>
    public sealed class MobaPublishedHeroPredictionTests
    {
        [TestCase("LuxQ")]
        [TestCase("KarthusE")]
        [TestCase("KarthusR")]
        [TestCase("AnnieR")]
        [TestCase("VeigarE")]
        public void PublishedSkillReplaysLiveStateTwiceAndSettles(string skill) => RunScenario(skill);

        [Test]
        public void PublishedAnnieRReplaysAcrossExpiryAndReclaimsAfterRestoreFloor() => RunScenario("AnnieRExpiry");

        private static void RunScenario(string skill)
        {
            var spec = PublishedHeroPredictionScenario.Describe(skill);
            var loadout = PublishedHeroPredictionScenario.Loadout(spec.Hero);
            var scenario = new MobaAcceptanceExpectation
            {
                actors = new[]
                {
                    new MobaAcceptanceActorExpectation
                    {
                        playerId = "published_hero_caster", heroId = spec.Hero, attributeTemplateId = spec.Hero,
                        basicAttackSkillId = loadout[0], skillIds = loadout,
                        spawnPosition = new MobaAcceptanceVector3Expectation { x = 100, z = 100 }
                    }
                }
            };
            using var harness = MobaSkillConfigTestHarness.CreateForScenario(scenario, worldId: "published_hero_prediction");
            harness.EnterGameAndWarmup(10);
            var casterId = harness.AssertPlayerActorBound();
            DisableBrain(casterId);
            var targets = spec.Targets.Select((position, index) =>
            {
                var alias = PublishedHeroPredictionScenario.TargetPlayerName(index);
                var id = harness.SpawnScenarioActor(alias, 0, "hero", 2, 1001, 1001,
                    1, 0, 0, alias, 0, null, 0, new MobaAcceptanceVector3Expectation { x = position.X, z = position.Z });
                DisableBrain(id);
                if (skill == "VeigarE")
                {
                    harness.World.Services.Resolve<MobaPlayerActorMapService>().Bind(new PlayerId(alias), id);
                    harness.SetScenarioActorAttribute(id, "MOVE_SPEED", PublishedHeroPredictionScenario.SweptCrossingFixtureMoveSpeed);
                }
                harness.SetScenarioActorAttribute(id, "MAGIC_DEFENSE", 0);
                harness.SetScenarioActorAttribute(id, "PER_SECOND_BLOOD_R", 0);
                return id;
            }).ToArray();
            harness.Tick(1);
            PublishedHeroPredictionScenario.Run(skill, harness.World, harness.Worlds, harness.PlayerId, casterId, targets,
                harness.FixedDelta, TestContext.Out.WriteLine);

            void DisableBrain(int id)
            {
                var actor = harness.AssertActorEntity(id);
                if (actor.hasActorBrain) actor.RemoveActorBrain();
            }
        }
    }
}
