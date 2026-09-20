using System.Linq;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow.Tests
{
    public sealed class MobaBattleFlowConfigCatalogTests
    {
        [SetUp]
        public void RefreshCatalog()
        {
            MobaBattleFlowConfigCatalog.Refresh();
        }

        [Test]
        public void Catalog_LoadsHeroSkillAndEffectSources()
        {
            var hero = MobaBattleFlowConfigCatalog.FindHero(1002);
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.AttributeTemplateId, Is.EqualTo(1002));

            var skills = MobaBattleFlowConfigCatalog.SkillChoices(1002, 1002);
            Assert.That(skills.Any(entry => entry.Id == 10020101 && entry.Slot == 1), Is.True);
            Assert.That(MobaBattleFlowConfigCatalog.FindEffect(10020101), Is.Not.Null);
        }

        [Test]
        public void Catalog_LoadsTriggerReferenceDomains()
        {
            Assert.That(MobaBattleFlowConfigCatalog.FindBuff(1), Is.Not.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindProjectileLauncher(1), Is.Not.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindProjectile(1), Is.Not.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindAoe(1), Is.Not.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindSummon(1), Is.Not.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindSearchQuery(1), Is.Not.Null);

            Assert.That(MobaBattleFlowConfigCatalog.ProjectileLaunchers, Is.Not.Empty);
            Assert.That(MobaBattleFlowConfigCatalog.Projectiles, Is.Not.Empty);
            Assert.That(MobaBattleFlowConfigCatalog.Aoes, Is.Not.Empty);
            Assert.That(MobaBattleFlowConfigCatalog.Summons, Is.Not.Empty);
            Assert.That(MobaBattleFlowConfigCatalog.SearchQueries, Is.Not.Empty);
        }

        [Test]
        public void TraceKind_SelectsTheMatchingConfigDomain()
        {
            var damage = MobaBattleFlowConfigCatalog.TraceConfigChoices("DamageApply");
            Assert.That(damage, Is.SameAs(MobaBattleFlowConfigCatalog.Effects));

            var cast = MobaBattleFlowConfigCatalog.TraceConfigChoices("SkillCast");
            Assert.That(cast, Is.SameAs(MobaBattleFlowConfigCatalog.Skills));

            var buff = MobaBattleFlowConfigCatalog.TraceConfigChoices("BuffAdd");
            Assert.That(buff, Is.SameAs(MobaBattleFlowConfigCatalog.Buffs));
        }

        [Test]
        public void SkillFlow_CascadesReachableEffectsThroughCompositePhases()
        {
            var effects = MobaBattleFlowConfigCatalog.SkillEffects(10010301);
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010301));
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010311));
            Assert.That(effects.Select(entry => entry.Id), Does.Contain(10010321));
            Assert.That(MobaBattleFlowConfigCatalog.SkillFlowIds(10010301), Does.Contain(10010301));
        }

        [Test]
        public void SkillAwareTraceChoices_FilterAndValidateConfigIds()
        {
            var damageChoices = MobaBattleFlowConfigCatalog.TraceConfigChoices("DamageApply", 10020101);
            Assert.That(damageChoices.Select(entry => entry.Id), Is.EquivalentTo(new[] { 10020101 }));
            Assert.That(MobaBattleFlowConfigCatalog.PreferredTraceConfigId("DamageApply", 10020101),
                Is.EqualTo(10020101));
            Assert.That(MobaBattleFlowConfigCatalog.IsTraceConfigCompatible(
                "DamageApply", 10020101, 10020101), Is.True);
            Assert.That(MobaBattleFlowConfigCatalog.IsTraceConfigCompatible(
                "DamageApply", 10020101, 10010301), Is.False);

            var castChoices = MobaBattleFlowConfigCatalog.TraceConfigChoices("SkillCast", 10020101);
            Assert.That(castChoices.Select(entry => entry.Id), Is.EqualTo(new[] { 10020101 }));
        }

        [Test]
        public void UnknownIds_RemainAbsentInsteadOfBeingCoerced()
        {
            Assert.That(MobaBattleFlowConfigCatalog.FindHero(987654321), Is.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindEffect(987654321), Is.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindProjectile(987654321), Is.Null);
            Assert.That(MobaBattleFlowConfigCatalog.FindSearchQuery(987654321), Is.Null);
        }
    }
}
