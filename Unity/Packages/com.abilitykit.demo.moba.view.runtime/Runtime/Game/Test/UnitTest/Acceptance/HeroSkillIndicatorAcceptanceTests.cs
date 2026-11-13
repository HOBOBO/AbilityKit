using System.Linq;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.View.Lib.Skill;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class HeroSkillIndicatorAcceptanceTests : MobaAcceptanceTestBase
    {
        [Test]
        public void EightShowcaseHeroes_ShouldUseOfficialIndicatorSemantics()
        {
            AssertHero("LianPo", 1001, 10010101, 10010201, 10010301,
                new[] { 22, 23, 3 });
            AssertHero("XiaoQiao", 1002, 10020101, 10020201, 10020301,
                new[] { 2, 3, 21 });
            AssertHero("ZhaoYun", 1003, 10030101, 10030201, 10030301,
                new[] { 8, 9, 10 });
            AssertHero("Mozi", 1004, 10040101, 10040201, 10040301,
                new[] { 19, 2, 20 });
            AssertHero("Daji", 1005, 10050101, 10050201, 10050301,
                new[] { 4, 17, 18 });
            AssertHero("YingZheng", 1006, 10060101, 10060201, 10060301,
                new[] { 5, 6, 7 });
            AssertHero("SunShangxiang", 1007, 10070101, 10070201, 10070301,
                new[] { 11, 12, 13 });
            AssertHero("Luban", 1008, 10080101, 10080201, 10080301,
                new[] { 14, 15, 16 });
        }

        [Test]
        public void JhinSkills_ShouldUseSkillSpecificAimAndIndicatorContracts()
        {
            var contract = new HeroSkillContract("Jhin", 3001, 3001,
                new[] { 30010101, 30010201, 30010301, 30010401 });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(contract, "official_indicator_Jhin"))
            {
                AssertJhinTemplate(harness, 30010101, 24, SkillAimMode.Direction,
                    SkillAimIndicatorShape.SelfCircle, SkillUsePointMode.None);
                AssertJhinTemplate(harness, 30010201, 25, SkillAimMode.Direction,
                    SkillAimIndicatorShape.DirectionLine, SkillUsePointMode.Aim);
                AssertJhinTemplate(harness, 30010301, 26, SkillAimMode.Point,
                    SkillAimIndicatorShape.TargetCircle, SkillUsePointMode.TargetPoint);
                AssertJhinTemplate(harness, 30010401, 27, SkillAimMode.Direction,
                    SkillAimIndicatorShape.Sector, SkillUsePointMode.Aim);
            }
        }

        [Test]
        public void JinxSkills_ShouldUseExpectedAimIndicatorsAndBeSelectableInLocalBattle()
        {
            const int heroId = 3002;
            var skillIds = new[] { 30020101, 30020201, 30020301, 30020401 };
            var contract = new HeroSkillContract("Jinx", heroId, heroId, skillIds);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(contract, "jinx_hud_catalog_and_aim_contract"))
            {
                Assert.IsTrue(harness.Config.GetAllCharacters().Any(character => character.Id == heroId),
                    "Jinx must be present in the character catalog consumed by the local hero selector.");
                Assert.IsTrue(MobaHeroLoadoutResolver.TryResolveHeroConfig(
                        harness.Config, heroId, out _, out var basicAttackSkillId, out var activeSkills, out var error),
                    "The local HUD selector filters characters through the production hero-loadout resolver. " + error);
                Assert.AreEqual(30020001, basicAttackSkillId);
                CollectionAssert.AreEqual(skillIds, activeSkills,
                    "All four active skills must resolve before Jinx can appear as a selectable loadout.");

                AssertJinxTemplate(harness, 30020101, 1, enableAim: false,
                    SkillAimMode.Direction, SkillAimIndicatorShape.Hidden, SkillUsePointMode.None);
                AssertJinxTemplate(harness, 30020201, 2, enableAim: true,
                    SkillAimMode.Direction, SkillAimIndicatorShape.DirectionLine, SkillUsePointMode.Aim);
                AssertJinxTemplate(harness, 30020301, 3, enableAim: true,
                    SkillAimMode.Point, SkillAimIndicatorShape.TargetCircle, SkillUsePointMode.TargetPoint);
                AssertJinxTemplate(harness, 30020401, 2, enableAim: true,
                    SkillAimMode.Direction, SkillAimIndicatorShape.DirectionLine, SkillUsePointMode.Aim);
            }
        }

        private static void AssertHero(string heroName, int heroId, int skill1, int skill2, int skill3, int[] templates)
        {
            var contract = new HeroSkillContract(heroName, heroId, heroId, new[] { skill1, skill2, skill3 });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(contract, "official_indicator_" + heroName))
            {
                AssertSkillTemplate(harness, skill1, templates[0]);
                AssertSkillTemplate(harness, skill2, templates[1]);
                AssertSkillTemplate(harness, skill3, templates[2]);
            }
        }

        private static void AssertSkillTemplate(MobaSkillConfigTestHarness harness, int skillId, int expectedTemplateId)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill), "Missing skill " + skillId);
            Assert.AreEqual(expectedTemplateId, skill.SkillButtonTemplateId, "Unexpected indicator template for skill " + skillId);
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(expectedTemplateId, out var template), "Missing indicator template " + expectedTemplateId);
            Assert.AreNotEqual(0, template.IndicatorShape, "An active showcase skill must not use Hidden indicator: " + skillId);
            if (template.IndicatorShape == 3)
            {
                Assert.IsTrue(template.SelfRadius > 0f, "Self-area indicator must carry its configured radius: " + skillId);
            }
            if (template.IndicatorShape == 6)
            {
                Assert.Greater(template.DashDistance, 0f, "Dash indicator must carry its configured travel distance: " + skillId);
            }
            if (template.IndicatorShape == 7)
            {
                Assert.Greater(template.LockOnDurationMs, 0, "Lock indicator must carry its configured lock duration: " + skillId);
                Assert.Greater(template.LockProjectileRadius, 0f, "Lock indicator must carry its configured search radius: " + skillId);
            }
        }

        private static void AssertJhinTemplate(
            MobaSkillConfigTestHarness harness,
            int skillId,
            int expectedTemplateId,
            SkillAimMode aimMode,
            SkillAimIndicatorShape shape,
            SkillUsePointMode usePointMode)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill), "Missing Jhin skill " + skillId);
            Assert.AreEqual(expectedTemplateId, skill.SkillButtonTemplateId,
                "Jhin skill must use its dedicated aim template: " + skillId);
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(expectedTemplateId, out var template),
                "Missing Jhin aim template " + expectedTemplateId);
            Assert.IsTrue(template.EnableAim, "Jhin skill must support drag aiming: " + skillId);
            Assert.AreEqual((int)aimMode, template.AimMode, "Wrong aim mode for Jhin skill " + skillId);
            Assert.AreEqual((int)shape, template.IndicatorShape, "Wrong indicator shape for Jhin skill " + skillId);
            Assert.AreEqual((int)usePointMode, template.UsePointMode, "Wrong point mode for Jhin skill " + skillId);

            if (skillId == 30010101)
            {
                Assert.AreEqual(12f, template.SelfRadius, 0.001f,
                    "Jhin Q must show the caster-centered target acquisition radius.");
                Assert.IsFalse(template.EnableAim && template.UsePointMode == (int)SkillUsePointMode.TargetPoint,
                    "Jhin Q must not use a ground target point as its release semantic.");
            }

            if (shape == SkillAimIndicatorShape.Sector)
                Assert.AreEqual(10f, template.SectorAngleDegrees, 0.001f,
                    "Curtain Call's indicator should match its narrow configured firing sector.");
        }

        private static void AssertJinxTemplate(
            MobaSkillConfigTestHarness harness,
            int skillId,
            int expectedTemplateId,
            bool enableAim,
            SkillAimMode aimMode,
            SkillAimIndicatorShape shape,
            SkillUsePointMode usePointMode)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill), "Missing Jinx skill " + skillId);
            Assert.AreEqual(expectedTemplateId, skill.SkillButtonTemplateId,
                "Jinx skill must use its intended shared HUD template: " + skillId);
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(expectedTemplateId, out var template),
                "Missing Jinx aim template " + expectedTemplateId);
            Assert.AreEqual(enableAim, template.EnableAim, "Wrong aim enablement for Jinx skill " + skillId);
            Assert.AreEqual((int)aimMode, template.AimMode, "Wrong aim mode for Jinx skill " + skillId);
            Assert.AreEqual((int)shape, template.IndicatorShape, "Wrong indicator shape for Jinx skill " + skillId);
            Assert.AreEqual((int)usePointMode, template.UsePointMode, "Wrong release-point mode for Jinx skill " + skillId);
            Assert.IsTrue(skill.AllowCastWithoutTarget,
                "Jinx active skills must be releasable from their authored aim snapshot without a locked unit target: " + skillId);
        }
    }
}
