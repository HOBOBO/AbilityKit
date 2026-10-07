using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Modifiers;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillParamModifierServiceTests
    {
        [Test]
        public void HitRecord_UsesProjectileTemplateFromTypedExecutionPayload()
        {
            var hit = new ProjectileHitArgs { ProjectileTemplateId = 301 };
            var context = new MobaCombatExecutionContext(hit, default, default, default, default, 10);
            var templateId = GiveDamagePlanActionModule.ResolveProjectileTemplateId(in context);
            var record = new MobaParameterRecord(MobaParameterStage.Hit,
                projectileTemplateId: templateId);

            Assert.That(templateId, Is.EqualTo(301));
            Assert.That(new MobaParameterSelector(projectileTemplateId: 301).Matches(in record), Is.True);
            Assert.That(new MobaParameterSelector(projectileTemplateId: 302).Matches(in record), Is.False);

            var unrelated = new MobaCombatExecutionContext(new object(), default, default,
                default, default, 10);
            Assert.That(GiveDamagePlanActionModule.ResolveProjectileTemplateId(in unrelated), Is.Zero);
        }

        [Test]
        public void SkillParameters_ResolveThroughActorScopedModifierChain()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var record = new MobaParameterRecord(MobaParameterStage.CastPrepare);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Skill.ResourceCost, ModifierOp.Add, 5f, sourceId: 101);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Skill.CooldownMs, ModifierOp.PercentAdd, -0.25f, sourceId: 102);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Skill.CastRange, ModifierOp.Mul, 1.5f, sourceId: 103);

            Assert.That(modifiers.Skill.ResolveResourceCost(7, 20, in record), Is.EqualTo(25));
            Assert.That(modifiers.Skill.ResolveCooldownMs(7, 1000, in record), Is.EqualTo(750));
            Assert.That(modifiers.Skill.ResolveCastRange(7, 4f, in record), Is.EqualTo(6f).Within(0.001f));

            modifiers.ClearSource(7, 102);
            Assert.That(modifiers.Skill.ResolveCooldownMs(7, 1000, in record), Is.EqualTo(1000));
        }

        [Test]
        public void ProjectileSpeed_UsesLauncherTemplateSelectorUnderActorOwner()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var key = MobaSkillParamModifierKeys.Projectile.Speed;
            AddFixed(modifiers, 10, key, ModifierOp.Override, 4f, priority: 10);
            var selected = new ModifierData
            {
                Key = key, Op = ModifierOp.Override, Magnitude = MagnitudeSource.Fixed(6f),
                SourceId = 21, Priority = 20,
            };
            modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in selected,
                new MobaParameterSelector(launcherTemplateId: 20));
            var matching = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
                launcherTemplateId: 20);
            var other = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
                launcherTemplateId: 30);

            Assert.That(modifiers.Projectile.ResolveSpeed(10, 1f, in matching), Is.EqualTo(6f));
            Assert.That(modifiers.Projectile.ResolveSpeed(10, 1f, in other), Is.EqualTo(4f));
        }

        [Test]
        public void PublishedParameterCatalog_ResolvesNewDomainsAndRejectsInvalidOperations()
        {
            Assert.That(MobaSkillParamModifierKeys.TryResolve(MobaSkillParamModifierKeys.Buff.Ids.DurationMs, out var buffKey), Is.True);
            Assert.That(buffKey, Is.EqualTo(MobaSkillParamModifierKeys.Buff.DurationMs));
            Assert.That(MobaSkillParamModifierKeys.TryResolve(MobaSkillParamModifierKeys.Projectile.Ids.Speed, out var speedKey), Is.True);
            Assert.That(speedKey, Is.EqualTo(MobaSkillParamModifierKeys.Projectile.Speed));
            Assert.That(MobaSkillParamModifierKeys.TryResolve(MobaSkillParamModifierKeys.Aoe.Ids.Radius, out var radiusKey), Is.True);
            Assert.That(radiusKey, Is.EqualTo(MobaSkillParamModifierKeys.Aoe.Radius));
            Assert.That(MobaSkillParamModifierKeys.IsTriggerWritable(MobaSkillParamModifierKeys.Emitter.Ids.LauncherId), Is.False);
            Assert.That(MobaSkillParamModifierKeys.SupportsOperation(MobaSkillParamModifierKeys.Emitter.Ids.LauncherId, (int)ModifierOp.Add), Is.False);
            Assert.That(MobaSkillParamModifierKeys.SupportsOperation(MobaSkillParamModifierKeys.Emitter.Ids.LauncherId, (int)ModifierOp.Override), Is.True);
            Assert.That(MobaSkillParamModifierKeys.TryResolve(999, out _), Is.False);
        }

        [Test]
        public void BuffProjectileAndAreaParameters_ResolveAtOwningScopeAndClearBySource()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var buff = new MobaParameterRecord(MobaParameterStage.BuffApply);
            var area = new MobaParameterRecord(MobaParameterStage.AreaSpawn);
            var projectile = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Buff.DurationMs, ModifierOp.Mul, 1.5f, sourceId: 101);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Aoe.Radius, ModifierOp.Add, 2f, sourceId: 102);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Aoe.DurationMs, ModifierOp.Add, 250f, sourceId: 102);
            AddFixed(modifiers, 7, MobaSkillParamModifierKeys.Projectile.Speed,
                ModifierOp.Add, 3f, sourceId: 103);

            Assert.That(modifiers.Buff.ResolveDurationMs(7, 1000, in buff), Is.EqualTo(1500));
            Assert.That(modifiers.Buff.ResolveDurationMs(8, 1000, in buff), Is.EqualTo(1000));
            Assert.That(modifiers.Aoe.ResolveRadius(7, 4f, in area), Is.EqualTo(6f));
            Assert.That(modifiers.Aoe.ResolveDurationMs(7, 1000, in area), Is.EqualTo(1250));
            Assert.That(modifiers.Projectile.ResolveSpeed(7, 10f, in projectile), Is.EqualTo(13f));

            modifiers.ClearSource(7, 101);
            modifiers.ClearSource(7, 102);
            Assert.That(modifiers.Buff.ResolveDurationMs(7, 1000, in buff), Is.EqualTo(1000));
            Assert.That(modifiers.Aoe.ResolveRadius(7, 4f, in area), Is.EqualTo(4f));
            Assert.That(modifiers.Projectile.ResolveSpeed(7, 10f, in projectile), Is.EqualTo(13f));
        }

        [Test]
        public void RecordedResolution_ReturnsBaseWhenOwnerIsInvalid()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var record = new MobaParameterRecord(MobaParameterStage.Emit);
            Assert.That(modifiers.ResolveFloat(MobaModifierOwnerRef.Actor(0),
                MobaSkillParamModifierKeys.Emitter.FanAngleDeg, 12.5f, in record), Is.EqualTo(12.5f));
        }

        [Test]
        public void RollbackProvider_RestoresStableFullFidelityModifierSnapshot()
        {
            using var modifiers = new MobaSkillParamModifierService();
            var modifier = new ModifierData
            {
                Key = MobaSkillParamModifierKeys.Emitter.FanAngleDeg,
                Op = ModifierOp.Add,
                Priority = 7,
                SourceId = 99190021,
                SourceNameIndex = -1,
                Magnitude = MagnitudeSource.LevelCurve(5f, new[] { 1f, 2f, 3f }, 2f),
                CustomData = CustomModifierData.Bytes(new byte[] { 4, 5, 6 }),
                Metadata = ModifierMetadata.CreateByIndex(-1, 3u, 99190021),
            };
            modifiers.AddModifier(7, in modifier);
            var provider = new MobaSkillParamModifierRollbackProvider(modifiers, restoreProjection: true);
            var frame = new FrameIndex(12);
            var payload = provider.ExportState(frame);

            modifiers.ClearActor(7);
            var record = new MobaParameterRecord(MobaParameterStage.Emit);
            Assert.That(modifiers.Projectile.ResolveFanAngleDeg(7, 10f, in record), Is.EqualTo(10f));
            provider.ImportState(frame, payload);

            var snapshot = modifiers.CaptureRollbackSnapshot();
            Assert.That(snapshot.Entries, Has.Length.EqualTo(1));
            Assert.That(snapshot.Entries[0].Modifier.Magnitude.ArrayData, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(snapshot.Entries[0].Modifier.CustomData.RawData, Is.EqualTo(new byte[] { 4, 5, 6 }));
            Assert.That(snapshot.Entries[0].Modifier.Metadata.TagsMask, Is.EqualTo(3u));
        }

        private static void AddFixed(MobaSkillParamModifierService service, int actorId,
            ModifierKey key, ModifierOp op, float value, int sourceId = 0, int priority = 10)
        {
            var modifier = new ModifierData
            {
                Key = key, Op = op, Magnitude = MagnitudeSource.Fixed(value),
                SourceId = sourceId, Priority = priority,
            };
            service.AddModifier(MobaModifierOwnerRef.Actor(actorId), in modifier);
        }
    }
}
