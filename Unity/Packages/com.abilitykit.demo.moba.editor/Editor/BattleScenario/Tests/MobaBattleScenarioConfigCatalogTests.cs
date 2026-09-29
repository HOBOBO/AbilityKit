using System.Linq;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario.Tests
{
    public sealed class MobaBattleScenarioConfigCatalogTests
    {
        [SetUp]
        public void RefreshCatalog()
        {
            MobaBattleScenarioConfigCatalog.Refresh();
        }

        [Test]
        public void Catalog_LoadsHeroSkillAndEffectSources()
        {
            var hero = MobaBattleScenarioConfigCatalog.FindHero(1002);
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.AttributeTemplateId, Is.EqualTo(1002));

            var skills = MobaBattleScenarioConfigCatalog.SkillChoices(1002, 1002);
            Assert.That(skills.Any(entry => entry.Id == 10020101 && entry.Slot == 1), Is.True);
            Assert.That(MobaBattleScenarioConfigCatalog.FindEffect(10020101), Is.Not.Null);
        }

        [Test]
        public void Catalog_LoadsTriggerReferenceDomains()
        {
            Assert.That(MobaBattleScenarioConfigCatalog.FindBuff(1), Is.Not.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindProjectileLauncher(1), Is.Not.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindProjectile(1), Is.Not.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindAoe(1), Is.Not.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindSummon(1), Is.Not.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindSearchQuery(1), Is.Not.Null);

            Assert.That(MobaBattleScenarioConfigCatalog.ProjectileLaunchers, Is.Not.Empty);
            Assert.That(MobaBattleScenarioConfigCatalog.Projectiles, Is.Not.Empty);
            Assert.That(MobaBattleScenarioConfigCatalog.Aoes, Is.Not.Empty);
            Assert.That(MobaBattleScenarioConfigCatalog.Summons, Is.Not.Empty);
            Assert.That(MobaBattleScenarioConfigCatalog.SearchQueries, Is.Not.Empty);
        }

        [Test]
        public void TraceKind_SelectsTheMatchingConfigDomain()
        {
            var damage = MobaBattleScenarioConfigCatalog.TraceConfigChoices("DamageApply");
            Assert.That(damage, Is.SameAs(MobaBattleScenarioConfigCatalog.Effects));

            var cast = MobaBattleScenarioConfigCatalog.TraceConfigChoices("SkillCast");
            Assert.That(cast, Is.SameAs(MobaBattleScenarioConfigCatalog.Skills));

            var buff = MobaBattleScenarioConfigCatalog.TraceConfigChoices("BuffAdd");
            Assert.That(buff, Is.SameAs(MobaBattleScenarioConfigCatalog.Buffs));
        }

        [Test]
        public void SkillFlow_CascadesReachableEffectsThroughCompositePhases()
        {
            var effects = MobaBattleScenarioConfigCatalog.SkillEffects(10010301);
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010301));
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010311));
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010321));
            Assert.That(MobaBattleScenarioConfigCatalog.SkillFlowIds(10010301), Does.Contain(10010301));
        }

        [Test]
        public void SkillAwareTraceChoices_FilterAndValidateConfigIds()
        {
            var damageChoices = MobaBattleScenarioConfigCatalog.TraceConfigChoices("DamageApply", 10020101);
            Assert.That(damageChoices.Select(entry => entry.Id), Is.EquivalentTo(new[] { 10020101 }));
            Assert.That(MobaBattleScenarioConfigCatalog.PreferredTraceConfigId("DamageApply", 10020101),
                Is.EqualTo(10020101));
            Assert.That(MobaBattleScenarioConfigCatalog.IsTraceConfigCompatible(
                "DamageApply", 10020101, 10020101), Is.True);
            Assert.That(MobaBattleScenarioConfigCatalog.IsTraceConfigCompatible(
                "DamageApply", 10020101, 10010301), Is.False);

            var castChoices = MobaBattleScenarioConfigCatalog.TraceConfigChoices("SkillCast", 10020101);
            Assert.That(castChoices.Select(entry => entry.Id), Is.EqualTo(new[] { 10020101 }));
        }

        [Test]
        public void UnknownIds_RemainAbsentInsteadOfBeingCoerced()
        {
            Assert.That(MobaBattleScenarioConfigCatalog.FindHero(987654321), Is.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindEffect(987654321), Is.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindProjectile(987654321), Is.Null);
            Assert.That(MobaBattleScenarioConfigCatalog.FindSearchQuery(987654321), Is.Null);
        }
    }
}
