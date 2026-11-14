using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaHeroContentGroupsTests
    {
        [TestCase(3001)]
        [TestCase(3002)]
        [TestCase(3003)]
        [TestCase(3004)]
        [TestCase(3005)]
        [TestCase(3006)]
        [TestCase(3007)]
        public void FormalHeroes_AreEnabledByDefault(int heroId)
        {
            Assert.IsTrue(AbilityKit.Game.Flow.MobaHeroContentGroups.IsFormal(heroId));
            Assert.IsTrue(AbilityKit.Game.Flow.MobaHeroContentGroups.IsEnabled(heroId, false));
        }

        [TestCase(1001)]
        [TestCase(1004)]
        [TestCase(1007)]
        public void PlaceholderHeroes_RequireExplicitEnablement(int heroId)
        {
            Assert.IsFalse(AbilityKit.Game.Flow.MobaHeroContentGroups.IsFormal(heroId));
            Assert.IsFalse(AbilityKit.Game.Flow.MobaHeroContentGroups.IsEnabled(heroId, false));
            Assert.IsTrue(AbilityKit.Game.Flow.MobaHeroContentGroups.IsEnabled(heroId, true));
        }
    }
}
