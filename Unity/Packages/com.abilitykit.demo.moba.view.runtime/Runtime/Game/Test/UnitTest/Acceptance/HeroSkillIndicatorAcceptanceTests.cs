using AbilityKit.Demo.Moba.Services;
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
    }
}
