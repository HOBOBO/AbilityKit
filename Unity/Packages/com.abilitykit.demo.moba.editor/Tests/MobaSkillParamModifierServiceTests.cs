using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Modifiers;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillParamModifierServiceTests
    {
        [Test]
        public void SkillParameters_ResolveThroughActorScopedModifierChain()
        {
            using var modifiers = new MobaSkillParamModifierService();
            modifiers.AddFixed(7, MobaSkillParamModifierKeys.Skill.ResourceCost, ModifierOp.Add, 5f, sourceId: 101);
            modifiers.AddFixed(7, MobaSkillParamModifierKeys.Skill.CooldownMs, ModifierOp.PercentAdd, -0.25f, sourceId: 102);
            modifiers.AddFixed(7, MobaSkillParamModifierKeys.Skill.CastRange, ModifierOp.Mul, 1.5f, sourceId: 103);

            Assert.That(modifiers.Skill.ResolveResourceCost(7, 20), Is.EqualTo(25));
            Assert.That(modifiers.Skill.ResolveCooldownMs(7, 1000), Is.EqualTo(750));
            Assert.That(modifiers.Skill.ResolveCastRange(7, 4f), Is.EqualTo(6f).Within(0.001f));

            modifiers.ClearSource(7, 102);
            Assert.That(modifiers.Skill.ResolveCooldownMs(7, 1000), Is.EqualTo(1000));
        }

        [Test]
        public void ProjectileParameters_ResolveOwnerChainInSpecificToGeneralOrder()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var key = MobaSkillParamModifierKeys.Projectile.CountPerShot;
            modifiers.AddFixed(MobaModifierOwnerRef.Projectile(30), key, ModifierOp.Override, 8f, priority: 30);
            modifiers.AddFixed(MobaModifierOwnerRef.Launcher(20), key, ModifierOp.Override, 6f, priority: 20);
            modifiers.AddFixed(MobaModifierOwnerRef.Actor(10), key, ModifierOp.Override, 4f, priority: 10);
            var resolveContext = new MobaModifierResolveContext(
                actorId: 10,
                launcherActorId: 20,
                projectileActorId: 30);

            var resolved = modifiers.Projectile.ResolveCountPerShotFromProjectile(resolveContext, 1);

            Assert.That(resolved, Is.EqualTo(8));
        }

        [Test]
        public void ResolveOwnerChain_SkipsInvalidOwnersAndReturnsBaseWhenNoModifierMatches()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var resolveContext = new MobaModifierResolveContext(actorId: 7, projectileActorId: 31);
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[3];
            var count = resolveContext.WriteProjectileThenLauncherThenActorChain(owners);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(owners[0].Scope, Is.EqualTo(MobaModifierOwnerScope.Projectile));
            Assert.That(owners[1].Scope, Is.EqualTo(MobaModifierOwnerScope.Actor));
            Assert.That(
                modifiers.ResolveFloat(
                    owners.Slice(0, count),
                    MobaSkillParamModifierKeys.Projectile.FanAngleDeg,
                    12.5f),
                Is.EqualTo(12.5f));
        }

        [Test]
        public void RollbackProvider_RestoresStableFullFidelityModifierSnapshot()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var modifier = new ModifierData
            {
                Key = MobaSkillParamModifierKeys.Projectile.FanAngleDeg,
                Op = ModifierOp.Add,
                Priority = 7,
                SourceId = 99190021,
                SourceNameIndex = -1,
                Magnitude = MagnitudeSource.LevelCurve(5f, new[] { 1f, 2f, 3f }, 2f),
                CustomData = CustomModifierData.Bytes(new byte[] { 4, 5, 6 }),
                Metadata = ModifierMetadata.CreateByIndex(-1, 3u, 99190021),
            };
            modifiers.AddModifier(7, in modifier);
            var provider = new MobaSkillParamModifierRollbackProvider(modifiers);
            var frame = new FrameIndex(12);
            var payload = provider.ExportState(frame);

            modifiers.ClearActor(7);
            Assert.That(modifiers.Projectile.ResolveFanAngleDeg(new MobaModifierResolveContext(actorId: 7), 10f), Is.EqualTo(10f));
            provider.ImportState(frame, payload);

            var snapshot = modifiers.CaptureRollbackSnapshot();
            Assert.That(snapshot.Entries, Has.Length.EqualTo(1));
            Assert.That(snapshot.Entries[0].Modifier.Magnitude.ArrayData, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(snapshot.Entries[0].Modifier.CustomData.RawData, Is.EqualTo(new byte[] { 4, 5, 6 }));
            Assert.That(snapshot.Entries[0].Modifier.Metadata.TagsMask, Is.EqualTo(3u));
        }
    }
}
