using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class DajiSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract Daji = new HeroSkillContract(
            "Daji",
            heroId: 1005,
            attributeTemplateId: 1005,
            skillIds: new[] { 10050101, 10050201, 10050301 });

        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10050101, 10050101, 10050101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10050201, 10050201, 10050201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10050301, 10050301, 10050301);

        [Test]
        public void Skill10050101_ShouldLaunchRectangularWaveAndDamageOffsetTarget()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "daji_skill_1_rectangular_wave_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050101,
                    (int)TriggeringConstants.ShootProjectileId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050111,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.AssertProjectileConfigExists(31050101, 30050101);
                Assert.IsTrue(harness.Config.TryGetProjectile(30050101, out var projectile), "Daji skill 1 projectile config should exist.");
                Assert.AreEqual(2f, projectile.CollisionWidth, 0.001f, "Daji skill 1 should use the configured rectangular width.");
                Assert.AreEqual(1.5f, projectile.CollisionHeight, 0.001f, "Daji skill 1 should use the configured rectangular height.");
                Assert.AreEqual(0.8f, projectile.CollisionLength, 0.001f, "Daji skill 1 should use the configured rectangular length.");

                harness.EnterGameAndWarmup(reason: "daji skill 1 rectangular wave contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f, z: 0.8f);
                var hpBefore = harness.GetActorHp(targetActorId);
                var magicDefenseBefore = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAGIC_DEFENSE);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill1.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Daji skill 1 should cast along the selected direction. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaTraceKind.EffectExecution,
                    Skill1.EffectId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(Skill1.SkillId, Skill1.EffectId, safetyFrames: 5) + 30,
                    message: "Daji skill 1 should execute its configured effect.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31050101, 30050101);
                var spawn = TickUntilProjectileSpawnSnapshot(harness, 30050101, maxTicks: 30);
                AssertDajiProjectileMovesAndResolvesVfx(harness, in spawn, expectedVfxId: 90005001, skillLabel: "skill 1 soul wave");
                TickUntilActorHpLessThan(
                    harness,
                    targetActorId,
                    hpBefore,
                    maxTicks: 60,
                    message: "Daji skill 1 rectangular wave should hit a target offset from the center ray but inside its configured width.");
                AssertHeartbreakStacks(harness, targetActorId, expectedStacks: 1, magicDefenseBefore);
            }
        }

        [Test]
        public void Passive10050000_ShouldCapAtThreeLayersAndIncreaseFinalMagicDamage()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "daji_passive_stack_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "daji passive stack contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                Assert.IsTrue(harness.Config.TryGetBuff(10050000, out var heartbreak), "Daji Heartbreak passive Buff config should exist.");
                Assert.AreEqual(BuffStackingPolicy.AddStack, heartbreak.StackingPolicy, "Daji Heartbreak should add one stack for every skill hit.");
                Assert.AreEqual(BuffRefreshPolicy.ResetRemaining, heartbreak.RefreshPolicy, "Every Daji skill hit should refresh Heartbreak's duration.");
                Assert.AreEqual(3, heartbreak.MaxStacks, "Daji Heartbreak should cap at three stacks.");
                Assert.AreEqual(1, heartbreak.Modifiers.Count, "Daji Heartbreak should contain one magic-defense modifier.");
                Assert.AreEqual((int)BattleAttributeType.MAGIC_DEFENSE, heartbreak.Modifiers[0].TargetId, "Daji Heartbreak should modify magic defense.");
                Assert.AreEqual(-30f, heartbreak.Modifiers[0].Value, 0.01f, "Each Heartbreak stack should reduce magic defense by 30.");

                var magicDefenseBefore = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAGIC_DEFENSE);
                var damageBeforeStacks = HeroSkillHeadlessContract.ExecuteDamage(
                    harness,
                    actorId,
                    targetActorId,
                    baseDamage: 100f,
                    reasonKind: DamageReasonKind.Skill,
                    reasonParam: 10050101,
                    damageType: DamageType.Magic);
                Assert.IsNotNull(damageBeforeStacks, "Baseline magic damage should resolve before Heartbreak is applied.");

                for (var i = 0; i < 4; i++)
                {
                    harness.AddScenarioBuff(targetActorId, 10050000, actorId, durationOverrideMs: 3000);
                }

                AssertHeartbreakStacks(harness, targetActorId, expectedStacks: 3, magicDefenseBefore);
                HeroSkillHeadlessContract.AssertFreshBuff(
                    harness,
                    targetActorId,
                    10050000,
                    minRemainingSeconds: 2.8f,
                    message: "Reapplying Daji passive should refresh the three-second stack duration.");

                var damageAfterThreeStacks = HeroSkillHeadlessContract.ExecuteDamage(
                    harness,
                    actorId,
                    targetActorId,
                    baseDamage: 100f,
                    reasonKind: DamageReasonKind.Skill,
                    reasonParam: 10050101,
                    damageType: DamageType.Magic);
                Assert.IsNotNull(damageAfterThreeStacks, "Magic damage should resolve after Heartbreak reaches three stacks.");
                var effectiveDefenseBefore = magicDefenseBefore > 0f ? magicDefenseBefore : 0f;
                var expectedBeforeStacks = 100f * 100f / (100f + effectiveDefenseBefore);
                var magicDefenseAfter = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAGIC_DEFENSE);
                var effectiveDefenseAfter = magicDefenseAfter > 0f ? magicDefenseAfter : 0f;
                var expectedAfterThreeStacks = 100f * 100f / (100f + effectiveDefenseAfter);
                Assert.AreEqual(expectedBeforeStacks, damageBeforeStacks.Value, 0.01f, "Baseline final magic damage should use the target's original magic defense.");
                Assert.AreEqual(expectedAfterThreeStacks, damageAfterThreeStacks.Value, 0.01f, "Final magic damage should use the reduced magic defense at three Heartbreak stacks.");
                Assert.Greater(damageAfterThreeStacks.Value, damageBeforeStacks.Value, "Three Heartbreak stacks should increase subsequent final magic damage.");
            }
        }

        [Test]
        public void Skill10050201_ShouldRequireEnemyWithinConfiguredCastRangeWithoutStartingCooldown()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "daji_skill_2_required_target_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "daji skill 2 required target contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                var noTargetCast = skills.TryCastBySlot(actorId, Skill2.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsFalse(noTargetCast.Success, "Daji skill 2 should be rejected when no enemy is in its cast range.");
                AssertSkillCooldownClear(harness, actorId, Skill2.SkillId, "A rejected Daji skill 2 cast without a target must not start cooldown.");

                var outOfRangeTargetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 10.5f);
                var outOfRangeCast = skills.TryCastBySlot(actorId, Skill2.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: outOfRangeTargetActorId);
                Assert.IsFalse(outOfRangeCast.Success, "Daji skill 2 should reject an explicit enemy outside its ten-meter cast range.");
                AssertSkillCooldownClear(harness, actorId, Skill2.SkillId, "An out-of-range Daji skill 2 cast must not start cooldown.");
            }
        }

        [Test]
        public void Skill10050201_ShouldAutoLockEnemyLaunchHomingCharmAndApplyControlOnHit()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "daji_skill_2_homing_charm_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050201,
                    (int)TriggeringConstants.ShootProjectileId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050211,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.AssertProjectileConfigExists(31050201, 30050201);

                harness.EnterGameAndWarmup(reason: "daji skill 2 homing charm contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 6f);
                var magicDefenseBefore = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAGIC_DEFENSE);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill2.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Daji skill 2 should automatically lock an enemy inside its cast range. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaTraceKind.EffectExecution,
                    Skill2.EffectId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(Skill2.SkillId, Skill2.EffectId, safetyFrames: 5) + 30,
                    message: "Daji skill 2 should execute its configured effect.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31050201, 30050201);
                var spawn = TickUntilProjectileSpawnSnapshot(harness, 30050201, maxTicks: 30);
                AssertDajiProjectileMovesAndResolvesVfx(harness, in spawn, expectedVfxId: 90005002, skillLabel: "skill 2 homing charm");

                harness.MoveScenarioActor(targetActorId, new MobaAcceptanceVector3Expectation { x = 6f, y = 0f, z = 3f });
                var projectileActor = harness.AssertActorEntity(spawn.ProjectileActorId);
                var zBeforeTracking = projectileActor.transform.Value.Position.Z;
                TickUntilActorPositionZGreaterThan(harness, spawn.ProjectileActorId, zBeforeTracking + 0.05f, maxTicks: 10);

                TickUntilActorBuff(
                    harness,
                    targetActorId,
                    10050201,
                    maxTicks: 60,
                    message: "Daji charm projectile should track the moved locked target and apply control within its configured lifetime.");
                HeroSkillHeadlessContract.AssertFreshBuff(
                    harness,
                    targetActorId,
                    10050201,
                    minRemainingSeconds: 1.0f,
                    message: "Daji charm projectile should apply the configured control buff after hitting its locked target.");
                AssertHeartbreakStacks(harness, targetActorId, expectedStacks: 1, magicDefenseBefore);
            }
        }

        [Test]
        public void Skill10050301_ShouldLaunchExactlyFiveFoxfiresFromOneCast()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Daji, "daji_skill_3_foxfire_decay_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050301,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.ShootProjectileId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050314,
                    (int)TriggeringConstants.GetActionId(TriggeringConstants.Actions.AdjustDamageNumber).Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10050313,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.AssertProjectileConfigExists(31050301, 30050301);

                harness.EnterGameAndWarmup(reason: "daji skill 3 five foxfires contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                var targetHpBefore = harness.GetActorHp(targetActorId);
                var magicDefenseBefore = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAGIC_DEFENSE);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill3.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Daji ultimate should cast at the selected enemy. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaTraceKind.EffectExecution,
                    Skill3.EffectId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(Skill3.SkillId, Skill3.EffectId, safetyFrames: 5) + 30,
                    message: "Daji ultimate should execute its configured effect.");
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.AddBuffId.Value, TriggeringConstants.Actions.AddBuff);
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31050301, 30050301);
                HeroSkillHeadlessContract.AssertFreshBuff(
                    harness,
                    actorId,
                    10050301,
                    minRemainingSeconds: 1.2f,
                    message: "Daji ultimate should enter the configured 1.6 second foxfire state.");

                var firstFoxfireSpawn = TickUntilProjectileSpawnSnapshot(harness, 30050301, maxTicks: 30);
                AssertDajiProjectileMovesAndResolvesVfx(harness, in firstFoxfireSpawn, expectedVfxId: 90005004, skillLabel: "skill 3 first foxfire");
                var laterFoxfireSpawnCount = CountProjectileSpawnsWithinTicks(harness, 30050301, maxTicks: 80);
                Assert.AreEqual(4, laterFoxfireSpawnCount, "One Daji ultimate cast should launch exactly five foxfires, including the already-verified first foxfire, without a second periodic scheduling path.");
                TickUntilActorHpLessThan(
                    harness,
                    targetActorId,
                    targetHpBefore,
                    maxTicks: 30,
                    message: "Daji foxfires should damage their selected target.");
                TickUntilHeartbreakStacks(harness, targetActorId, expectedStacks: 3, maxTicks: 60);
                AssertHeartbreakStacks(harness, targetActorId, expectedStacks: 3, magicDefenseBefore);
            }
        }

        private static void AssertHeartbreakStacks(MobaSkillConfigTestHarness harness, int actorId, int expectedStacks, float magicDefenseBefore)
        {
            Assert.IsTrue(
                harness.TryGetActorBuffStackCount(actorId, 10050000, out var stackCount),
                "Daji skill damage should apply Heartbreak to its target.");
            Assert.AreEqual(expectedStacks, stackCount, "Daji skill hits should add Heartbreak up to its configured three-stack cap.");
            Assert.AreEqual(
                magicDefenseBefore - expectedStacks * 30f,
                harness.GetActorAttribute(actorId, BattleAttributeType.MAGIC_DEFENSE),
                0.01f,
                "Each Heartbreak stack should reduce the target's magic defense by 30.");
        }

        private static void TickUntilHeartbreakStacks(MobaSkillConfigTestHarness harness, int actorId, int expectedStacks, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (harness.TryGetActorBuffStackCount(actorId, 10050000, out var stackCount) && stackCount >= expectedStacks) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Daji skill hits should stack Heartbreak to " + expectedStacks + " layers within the expected time.");
        }

        private static void TickUntilActorHpLessThan(MobaSkillConfigTestHarness harness, int actorId, float hp, int maxTicks, string message)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (harness.GetActorHp(actorId) < hp) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail(message);
        }

        private static void TickUntilActorBuff(MobaSkillConfigTestHarness harness, int actorId, int buffId, int maxTicks, string message)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (harness.HasActorBuff(actorId, buffId)) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail(message);
        }

        private static int CountProjectileSpawnsWithinTicks(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            var count = 0;
            for (var i = 0; i <= maxTicks; i++)
            {
                snapshots.Clear();
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                {
                    if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload);
                    for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                    {
                        if (entries[entryIndex].Kind == (int)ProjectileEventKind.Spawn
                            && entries[entryIndex].TemplateId == templateId)
                        {
                            count++;
                        }
                    }
                }

                if (i < maxTicks) harness.Tick(1);
            }

            return count;
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSpawnSnapshot(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSpawnSnapshot(harness, templateId, out var entry)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Projectile spawn snapshot missing for template " + templateId + " within " + maxTicks + " ticks.");
            return default;
        }

        private static bool TryCollectProjectileSpawnSnapshot(MobaSkillConfigTestHarness harness, int templateId, out MobaProjectileEventSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            {
                if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload);
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    if (entries[entryIndex].Kind != (int)ProjectileEventKind.Spawn || entries[entryIndex].TemplateId != templateId) continue;
                    entry = entries[entryIndex];
                    return true;
                }
            }

            return false;
        }

        private static void AssertDajiProjectileMovesAndResolvesVfx(MobaSkillConfigTestHarness harness, in MobaProjectileEventSnapshotEntry spawn, int expectedVfxId, string skillLabel)
        {
            Assert.Greater(spawn.ProjectileActorId, 0, "Daji " + skillLabel + " spawn snapshot should expose a projectile actor for VFX follow binding.");
            Assert.Greater(spawn.ForwardX, 0.9f, "Daji " + skillLabel + " spawn snapshot should face its selected aim direction.");
            Assert.AreEqual(0f, spawn.ForwardY, 0.0001f, "Daji " + skillLabel + " should stay on the XZ plane.");

            var projectileActor = harness.AssertActorEntity(spawn.ProjectileActorId);
            Assert.IsTrue(projectileActor.hasTransform, "Daji " + skillLabel + " projectile actor should have a transform for client-followed movement.");
            var initialPosition = projectileActor.transform.Value.Position;
            var moved = TickUntilActorPositionXGreaterThan(harness, spawn.ProjectileActorId, initialPosition.X + 0.05f, maxTicks: 10);
            Assert.Greater(moved.X, initialPosition.X + 0.05f, "Daji " + skillLabel + " projectile actor should leave the caster and move toward its target.");
            Assert.IsTrue(TryCollectActorTransformSnapshot(harness, spawn.ProjectileActorId, out var transformEntry), "Daji " + skillLabel + " moving projectile should be included in actor transform snapshots.");
            Assert.Greater(transformEntry.X, initialPosition.X + 0.05f, "Daji " + skillLabel + " transform snapshot should carry its moved position for the view layer.");

            var resolver = new BattleProjectileVfxResolver();
            Assert.AreEqual(expectedVfxId, resolver.ResolveSnapshotVfxId(spawn.TemplateId, spawn.Kind), "Daji " + skillLabel + " should resolve its configured moving projectile VFX.");
        }

        private static Vec3 TickUntilActorPositionXGreaterThan(MobaSkillConfigTestHarness harness, int actorId, float minX, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var actor = harness.AssertActorEntity(actorId);
                if (actor.hasTransform && actor.transform.Value.Position.X > minX) return actor.transform.Value.Position;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Projectile actor " + actorId + " did not advance beyond X=" + minX.ToString("F3") + " within " + maxTicks + " ticks.");
            return default;
        }

        private static void TickUntilActorPositionZGreaterThan(MobaSkillConfigTestHarness harness, int actorId, float minZ, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var actor = harness.AssertActorEntity(actorId);
                if (actor.hasTransform && actor.transform.Value.Position.Z > minZ) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Projectile actor " + actorId + " did not turn toward the moved target within " + maxTicks + " ticks.");
        }

        private static void AssertSkillCooldownClear(MobaSkillConfigTestHarness harness, int actorId, int skillId, string message)
        {
            var actor = harness.AssertActorEntity(actorId);
            Assert.IsTrue(actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills != null, message);
            for (var i = 0; i < actor.skillLoadout.ActiveSkills.Length; i++)
            {
                var runtime = actor.skillLoadout.ActiveSkills[i];
                if (runtime == null || runtime.SkillId != skillId) continue;
                Assert.LessOrEqual(runtime.CooldownEndTimeMs, 0L, message);
                Assert.LessOrEqual(runtime.CooldownDurationMs, 0, message);
                return;
            }

            Assert.Fail("Active skill runtime missing for skill " + skillId + ". " + message);
        }

        private static bool TryCollectActorTransformSnapshot(MobaSkillConfigTestHarness harness, int actorId, out MobaActorTransformSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            {
                if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ActorTransform) continue;
                var entries = MobaActorTransformSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload);
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    if (entries[entryIndex].ActorId != actorId) continue;
                    entry = entries[entryIndex];
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class DajiRectangularProjectileTests
    {
        private const int CollisionLayerId = 0;
        private const int CollisionLayerMask = 1 << CollisionLayerId;

        [Test]
        public void RectangularSweep_ShouldHitOffsetTargetThatPointRayMisses()
        {
            var pointHits = TickSingleProjectile(collisionHalfExtents: Vec3.Zero, out _, out _);
            var boxHits = TickSingleProjectile(collisionHalfExtents: new Vec3(0.5f, 0.5f, 0.2f), out _, out _);

            Assert.AreEqual(0, pointHits.Count, "A point projectile should miss a target outside its center ray.");
            Assert.AreEqual(1, boxHits.Count, "A rectangular projectile should hit a target inside its width.");
        }

        [Test]
        public void RectangularSweep_ShouldSkipIgnoredColliderAndHitTargetBehindIt()
        {
            var collision = new NaiveCollisionWorld();
            var ignored = AddSphere(collision, new Vec3(2f, 0f, 0.4f), 0.1f);
            var target = AddSphere(collision, new Vec3(4f, 0f, 0.4f), 0.1f);
            var filter = new FixedCollisionResponseFilter(ProjectileCollisionResponse.Hit);
            filter.Set(ignored, ProjectileCollisionResponse.Ignore);
            var world = new ProjectileWorld(collision);
            SpawnTestProjectile(world, filter, new Vec3(0.5f, 0.5f, 0.2f));
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();

            world.Tick(1, 1f, hits, exits, tickEvents: null);

            Assert.AreEqual(1, hits.Count, "Ignoring an overlapping friendly collider should not exhaust rectangular sweep attempts.");
            Assert.AreEqual(target, hits[0].HitCollider, "The rectangular projectile should continue to the valid target behind the ignored collider.");
            Assert.AreEqual(0, exits.Count, "A piercing projectile should remain active after the valid hit.");
        }

        [Test]
        public void RectangularSweep_BlockerShouldExitPiercingProjectileWithoutHitEvent()
        {
            var collision = new NaiveCollisionWorld();
            var blocker = AddSphere(collision, new Vec3(2f, 0f, 0f), 0.2f);
            AddSphere(collision, new Vec3(4f, 0f, 0f), 0.2f);
            var filter = new FixedCollisionResponseFilter(ProjectileCollisionResponse.Hit);
            filter.Set(blocker, ProjectileCollisionResponse.Block);
            var world = new ProjectileWorld(collision);
            SpawnTestProjectile(world, filter, new Vec3(0.5f, 0.5f, 0.2f));
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();

            world.Tick(1, 1f, hits, exits, tickEvents: null);

            Assert.AreEqual(0, hits.Count, "A hard blocker should not emit a unit hit event.");
            Assert.AreEqual(1, exits.Count, "A hard blocker should immediately terminate the projectile.");
            Assert.AreEqual(ProjectileExitReason.Hit, exits[0].Reason);
            Assert.AreEqual(0, world.ActiveCount);
        }

        [Test]
        public void ManualDespawn_ShouldQueueExitWithoutHitEvent()
        {
            var service = new ProjectileService(new CollisionService());
            var projectileId = service.Spawn(new ProjectileSpawnParams(
                ownerId: 1,
                templateId: 30050101,
                launcherActorId: 2,
                rootActorId: 1,
                spawnFrame: 7,
                position: Vec3.Zero,
                direction: new Vec3(1f, 0f, 0f),
                speed: 10f,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: 10,
                maxDistance: 20f,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: default,
                hitFilter: null));
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();

            Assert.IsTrue(service.Despawn(projectileId, 42, ProjectileExitReason.Manual));
            service.DrainHitEvents(hits);
            service.DrainExitEvents(exits);

            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(0, hits.Count, "Area-driven projectile clearing must not emit a projectile hit event.");
            Assert.AreEqual(1, exits.Count);
            Assert.AreEqual(projectileId, exits[0].Projectile);
            Assert.AreEqual(ProjectileExitReason.Manual, exits[0].Reason);
            Assert.AreEqual(42, exits[0].Frame);
        }

        [Test]
        public void Rollback_ShouldRetainRectangularCollisionHalfExtents()
        {
            var collision = new NaiveCollisionWorld();
            var target = AddSphere(collision, new Vec3(4f, 0f, 0.4f), 0.1f);
            var world = new ProjectileWorld(collision);
            SpawnTestProjectile(world, hitFilter: null, collisionHalfExtents: new Vec3(0.5f, 0.5f, 0.2f));
            var payload = world.ExportRollback(new FrameIndex(0));
            world.ImportRollback(new FrameIndex(0), payload);
            var hits = new List<ProjectileHitEvent>();

            world.Tick(1, 1f, hits, exitEvents: null, tickEvents: null);

            Assert.AreEqual(1, hits.Count, "Rollback restore should preserve rectangular collision geometry.");
            Assert.AreEqual(target, hits[0].HitCollider);
        }

        private static List<ProjectileHitEvent> TickSingleProjectile(in Vec3 collisionHalfExtents, out List<ProjectileExitEvent> exits, out ProjectileWorld world)
        {
            var collision = new NaiveCollisionWorld();
            AddSphere(collision, new Vec3(4f, 0f, 0.4f), 0.1f);
            world = new ProjectileWorld(collision);
            SpawnTestProjectile(world, hitFilter: null, collisionHalfExtents: collisionHalfExtents);
            var hits = new List<ProjectileHitEvent>();
            exits = new List<ProjectileExitEvent>();
            world.Tick(1, 1f, hits, exits, tickEvents: null);
            return hits;
        }

        private static void SpawnTestProjectile(ProjectileWorld world, IProjectileHitFilter hitFilter, in Vec3 collisionHalfExtents)
        {
            world.Spawn(new ProjectileSpawnParams(
                ownerId: 1,
                templateId: 30050101,
                launcherActorId: 1,
                rootActorId: 1,
                spawnFrame: 0,
                position: Vec3.Zero,
                direction: Vec3.Right,
                speed: 10f,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: 30,
                maxDistance: 20f,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: default,
                hitsRemaining: 6,
                hitPolicyKind: ProjectileHitPolicyKind.Pierce,
                hitPolicyParam: 6,
                hitFilter: hitFilter,
                collisionHalfExtents: collisionHalfExtents));
        }

        private static ColliderId AddSphere(NaiveCollisionWorld collision, in Vec3 position, float radius)
        {
            var transform = new Transform3(position, Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, radius));
            return collision.Add(transform, shape, CollisionLayerId);
        }

        private sealed class FixedCollisionResponseFilter : IProjectileHitFilter, IProjectileCollisionResponseResolver
        {
            private readonly Dictionary<int, ProjectileCollisionResponse> _responses = new Dictionary<int, ProjectileCollisionResponse>();
            private readonly ProjectileCollisionResponse _defaultResponse;

            public FixedCollisionResponseFilter(ProjectileCollisionResponse defaultResponse)
            {
                _defaultResponse = defaultResponse;
            }

            public void Set(ColliderId collider, ProjectileCollisionResponse response)
            {
                _responses[collider.Value] = response;
            }

            public bool ShouldHit(int ownerId, ColliderId collider, int frame)
            {
                return ResolveCollision(ownerId, collider, frame) == ProjectileCollisionResponse.Hit;
            }

            public ProjectileCollisionResponse ResolveCollision(int ownerId, ColliderId collider, int frame)
            {
                return _responses.TryGetValue(collider.Value, out var response) ? response : _defaultResponse;
            }
        }
    }
}
