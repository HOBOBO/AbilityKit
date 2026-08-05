using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class XiaoQiaoSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const string Skill10020101ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020101.expected.json";
        private const string Skill10020101ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020101_scenario.expected.json";
        private const string Skill10020201ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020201.expected.json";
        private const string Skill10020201ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020201_scenario.expected.json";
        private const string Skill10020301ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020301.expected.json";
        private const string Skill10020301ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020301_scenario.expected.json";

        [Test]
        public void XiaoQiaoPresentationResources_ShouldLoadAndMatchUltimateRadius()
        {
            Assert.IsNotNull(Resources.Load<GameObject>("effect/xiaoqiao_skill1_fan"), "Xiao Qiao skill 1 fan projectile prefab should be available through Unity Resources.");
            Assert.IsNotNull(Resources.Load<GameObject>("effect/xiaoqiao_skill3_star"), "Xiao Qiao skill 3 persistent range prefab should be available through Unity Resources.");

            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                Assert.IsTrue(harness.Config.GetTable<PresentationTemplateMO>().TryGet(90002003, out var presentation), "Xiao Qiao skill 3 presentation template 90002003 should exist.");
                Assert.IsTrue(harness.Config.TryGetSearchQueryTemplate(50020301, out var searchQuery), "Xiao Qiao skill 3 search query template 50020301 should exist.");
                Assert.IsNotNull(searchQuery.Rules);
                Assert.Greater(searchQuery.Rules.Length, 0);

                Assert.AreEqual(searchQuery.Rules[0].Radius, presentation.Radius, 0.0001f, "Xiao Qiao skill 3 presentation radius should match the logical self-area query radius.");
                Assert.AreEqual(8f, presentation.Radius, 0.0001f, "Xiao Qiao skill 3 persistent presentation should cover the large ultimate area.");
            }
        }

        [Test]
        public void Skill10020101_RuntimeSnapshot_ShouldSpawnFanProjectileVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                harness.EnterGameAndWarmup(reason: "xiao qiao skill 1 projectile vfx snapshot contract");
                harness.AssertSlotSkill(1, 10020101);
                DrainSnapshots(harness);

                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, slot: 1, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
                Assert.IsTrue(cast.Success, $"Xiao Qiao skill 1 cast should succeed before checking projectile view snapshot. failReason={cast.FailReason}");

                var entry = TickUntilProjectileSpawnSnapshot(harness, templateId: 30020101, maxTicks: 30);
                Assert.AreEqual((int)ProjectileEventKind.Spawn, entry.Kind, "Xiao Qiao skill 1 should emit a projectile spawn snapshot for the fan.");
                Assert.Greater(entry.LauncherActorId, 0, "The fan projectile spawn snapshot should retain a valid launcher actor for follow/position resolution.");
                Assert.Greater(entry.ProjectileActorId, 0, "The fan projectile spawn snapshot should expose the moving projectile actor for VFX follow binding.");
                Assert.AreEqual(1f, entry.ForwardX, 0.0001f, "The fan projectile spawn snapshot should carry launch forward for VFX orientation.");
                Assert.AreEqual(0f, entry.ForwardY, 0.0001f, "The fan projectile spawn snapshot should stay on the XZ plane.");
                Assert.AreEqual(0f, entry.ForwardZ, 0.0001f, "The fan projectile spawn snapshot should follow the skill aim direction.");

                var projectileActor = harness.AssertActorEntity(entry.ProjectileActorId);
                Assert.IsTrue(projectileActor.hasTransform, "The fan projectile actor should have a transform for snapshot-driven movement.");
                var initialPosition = projectileActor.transform.Value.Position;

                var moved = TickUntilActorPositionXGreaterThan(harness, entry.ProjectileActorId, initialPosition.X + 0.05f, maxTicks: 10);
                Assert.Greater(moved.X, initialPosition.X + 0.05f, $"The fan projectile actor should move after projectile ticks. initialX={initialPosition.X:F3}, movedX={moved.X:F3}");
                Assert.IsTrue(TryCollectActorTransformSnapshot(harness, entry.ProjectileActorId, out var transformEntry), "The moving fan projectile actor should be included in actor transform snapshots for the client view.");
                Assert.Greater(transformEntry.X, initialPosition.X + 0.05f, $"The fan projectile transform snapshot should carry the moved projectile position. initialX={initialPosition.X:F3}, snapshotX={transformEntry.X:F3}");

                var resolver = new BattleProjectileVfxResolver();
                Assert.AreEqual(90002001, resolver.ResolveSnapshotVfxId(entry.TemplateId, entry.Kind), "Xiao Qiao skill 1 projectile spawn snapshot should resolve to the configured fan VFX instead of a placeholder.");
            }
        }

        [Test]
        public void Skill10020101_RuntimeSnapshot_ShouldReturnFanProjectileToCaster()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                harness.EnterGameAndWarmup(reason: "xiao qiao skill 1 projectile return contract");
                harness.AssertSlotSkill(1, 10020101);
                DrainSnapshots(harness);

                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, slot: 1, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
                Assert.IsTrue(cast.Success, $"Xiao Qiao skill 1 cast should succeed before checking projectile return. failReason={cast.FailReason}");

                var spawn = TickUntilProjectileSpawnSnapshot(harness, templateId: 30020101, maxTicks: 30);
                Assert.Greater(spawn.ProjectileId, 0, "The fan projectile spawn should expose the runtime projectile id for return validation.");

                var exit = TickUntilProjectileExitSnapshot(harness, spawn.ProjectileId, maxTicks: 90);
                Assert.AreEqual((int)ProjectileExitReason.ReturnArrived, exit.ExitReason, $"Xiao Qiao fan should return to the caster instead of being lost or ending by max distance. exitReason={exit.ExitReason}");
                Assert.AreEqual(actorId, exit.RootActorId, "The returning fan should keep Xiao Qiao as its root actor for caster-position return targeting.");
            }
        }

        [Test]
        public void Skill10020101_ExportsTraceAndMatchesXiaoQiaoProjectileExpectation()
        {
            var summary = RunExpectationFile(Skill10020101ExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.skillCastTraceFound);
            Assert.IsTrue(summary.result.effectExecutionTraceFound);
            Assert.IsTrue(summary.result.allExpectedActionsExecuted);
            Assert.IsTrue(summary.result.projectileLaunched);
            Assert.Greater(summary.result.effectRootId, 0);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);
        }

        [Test]
        public void Skill10020101_ScenarioExportsTraceAndConfirmsProjectileDamage()
        {
            var summary = RunExpectationFile(Skill10020101ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.projectileLaunched);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10020101ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var effectRootId), "Missing effect root trace for projectile damage scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "ProjectileLaunch", 30020101, "the projectile scenario should launch the configured projectile under the Xiao Qiao skill 1 effect root.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, effectRootId, expectation.config.effectId, expectedHitCount: 1, "the projectile scenario should damage exactly one target exactly once under the same effect root.");
        }

        [Test]
        public void Skill10020201_ExportsTraceAndMatchesXiaoQiaoAreaExpectation()
        {
            var summary = RunExpectationFile(Skill10020201ExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.skillCastTraceFound);
            Assert.IsTrue(summary.result.effectExecutionTraceFound);
            Assert.IsTrue(summary.result.allExpectedActionsExecuted);
            Assert.IsTrue(summary.result.areaSpawned);
            Assert.Greater(summary.result.effectRootId, 0);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);
        }

        [Test]
        public void Skill10020201_ScenarioExportsTraceAndConfirmsAreaDamage()
        {
            var summary = RunExpectationFile(Skill10020201ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.areaSpawned);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10020201ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var effectRootId), "Missing effect root trace for area scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "AreaSpawn", 40020201, "area spawn should remain under the Xiao Qiao skill 2 effect root.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, effectRootId, expectation.config.effectId, expectedHitCount: 1, "the area scenario should damage exactly one target exactly once under the same effect root.");
        }

        [Test]
        public void Skill10020301_ExportsTraceAndMatchesXiaoQiaoUltimateExpectation()
        {
            var summary = RunExpectationFile(Skill10020301ExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.skillCastTraceFound);
            Assert.IsTrue(summary.result.effectExecutionTraceFound);
            Assert.IsTrue(summary.result.allExpectedActionsExecuted);
            Assert.IsTrue(summary.result.buffApplied);
            Assert.Greater(summary.result.effectRootId, 0);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);
        }

        [Test]
        public void Skill10020301_ScenarioExportsTraceAndConfirmsIntervalDamage()
        {
            var summary = RunExpectationFile(Skill10020301ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.buffApplied);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10020301ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var effectRootId), "Missing effect root trace for interval damage scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "BuffApply", 10020301, "the ultimate should apply its persistent buff under the same effect root.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "AreaSpawn", 40020301, "the ultimate interval tick should spawn the delayed starfall area under the same effect root.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "DamageApply", 10020301, "the delayed starfall should apply damage under the same effect root.");

            var starfallAreas = MobaAcceptanceTraceAssert.CollectTraceNodesInRoot(records, effectRootId, "AreaSpawn", 40020301);
            var starfallHits = MobaAcceptanceTraceAssert.CollectTraceNodesInRoot(records, effectRootId, "DamageApply", 10020301);
            Assert.GreaterOrEqual(starfallAreas.Count, 2, "the first two ultimate intervals should each spawn one delayed starfall area.");
            Assert.AreEqual(2, starfallHits.Count, "the first two delayed starfalls should apply exactly two damage hits in this scenario.");
        }

        [Test]
        public void Skill10020000_PassiveCastBuff_ShouldIncreaseMoveSpeedTemporarily()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                harness.EnterGameAndWarmup(reason: "xiao qiao passive move speed contract");
                harness.AssertSlotSkill(1, 10020101);

                const int passiveBuffId = 10020001;
                const float baseMoveSpeed = 5f;
                const float buffedMoveSpeed = 7f;
                var playerActorId = harness.AssertPlayerActorBound();

                Assert.AreEqual(baseMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Xiao Qiao base move speed should come from attribute template 1002 before casting.");
                Assert.IsFalse(harness.HasActorBuff(playerActorId, passiveBuffId), "Passive speed buff should not exist before any skill cast.");

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                Assert.IsNotNull(skills, "SkillCastCoordinator should be available in the single-player harness world.");
                var castResult = skills.TryCastBySlot(playerActorId, slot: 1, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
                Assert.IsTrue(castResult.Success, $"Xiao Qiao skill 1 cast should succeed in the passive contract test. failReason={castResult.FailReason}");

                harness.TickUntilTraceNode(MobaTraceKind.SkillCast, 10020101, maxTicks: 10, message: "Xiao Qiao skill 1 should emit SkillCast after direct cast.");
                harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, 10020101, maxTicks: 10, message: "Xiao Qiao skill 1 should execute effect 10020101 after direct cast.");
                harness.TickUntilTraceNode(MobaTraceKind.BuffApply, passiveBuffId, maxTicks: 30, message: "Xiao Qiao passive should apply buff 10020001 after skill cast completes.");

                Assert.IsTrue(harness.HasActorBuff(playerActorId, passiveBuffId), "Passive speed buff should exist immediately after the cast-triggered passive resolves.");
                Assert.AreEqual(buffedMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Passive speed buff should raise move speed by the configured modifier amount.");

                harness.TickSeconds(2.1f);

                Assert.IsFalse(harness.HasActorBuff(playerActorId, passiveBuffId), "Passive speed buff should expire after its configured 2 second duration.");
                Assert.AreEqual(baseMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Move speed should return to base value after passive buff expiration.");
            }
        }

        [Test]
        public void Skill10020000_PassiveCastBuff_RepeatedCastShouldRefreshDurationFromLatestApply()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                harness.EnterGameAndWarmup(reason: "xiao qiao passive move speed refresh contract");
                harness.AssertSlotSkill(1, 10020101);

                const int passiveBuffId = 10020001;
                const float baseMoveSpeed = 5f;
                const float buffedMoveSpeed = 7f;
                var playerActorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                Assert.IsNotNull(skills, "SkillCastCoordinator should be available in the single-player harness world.");

                var firstCast = skills.TryCastBySlot(playerActorId, slot: 1, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
                Assert.IsTrue(firstCast.Success, $"Xiao Qiao first skill 1 cast should succeed. failReason={firstCast.FailReason}");
                TickUntilActorBuff(harness, playerActorId, passiveBuffId, maxTicks: 30, message: "Xiao Qiao passive should apply the speed buff after the first cast completes.");

                Assert.AreEqual(buffedMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "The first passive application should raise move speed.");
                var firstRemaining = harness.AssertActorBuffRemainingSeconds(playerActorId, passiveBuffId, "Passive speed buff should exist after the first cast.");
                Assert.Greater(firstRemaining, 1.8f, $"Fresh passive speed buff should start near its configured 2 second duration, actual={firstRemaining:F3}.");

                harness.TickSeconds(1.0f);
                var beforeRefreshRemaining = harness.AssertActorBuffRemainingSeconds(playerActorId, passiveBuffId, "Passive speed buff should still exist before the refresh cast.");
                Assert.Less(beforeRefreshRemaining, firstRemaining - 0.5f, $"Passive speed buff remaining time should have advanced before refresh. beforeRefresh={beforeRefreshRemaining:F3}, first={firstRemaining:F3}.");

                var actor = harness.AssertActorEntity(playerActorId);
                if (actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills != null && actor.skillLoadout.ActiveSkills.Length > 0 && actor.skillLoadout.ActiveSkills[0] != null)
                {
                    actor.skillLoadout.ActiveSkills[0].CooldownEndTimeMs = 0L;
                    actor.skillLoadout.ActiveSkills[0].CooldownDurationMs = 0;
                }

                var secondCast = skills.TryCastBySlot(playerActorId, slot: 1, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
                Assert.IsTrue(secondCast.Success, $"Xiao Qiao second skill 1 cast should succeed for refresh validation. failReason={secondCast.FailReason}");
                var refreshedRemaining = TickUntilActorBuffRemainingAtLeast(
                    harness,
                    playerActorId,
                    passiveBuffId,
                    beforeRefreshRemaining + 0.5f,
                    maxTicks: 30,
                    message: $"Repeated passive application should refresh remaining time after the second cast completes. beforeRefresh={beforeRefreshRemaining:F3}");

                Assert.Greater(refreshedRemaining, 1.8f, $"Repeated passive application should reset remaining time from the latest application, actual={refreshedRemaining:F3}.");
                Assert.LessOrEqual(refreshedRemaining, 2.05f, $"Repeated passive application should not add duration beyond the configured 2 second window, actual={refreshedRemaining:F3}.");
                Assert.AreEqual(buffedMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Repeated passive application should refresh duration without stacking extra move speed.");

                harness.TickSeconds(1.1f);
                Assert.IsTrue(harness.HasActorBuff(playerActorId, passiveBuffId), "Passive speed buff should still be active after the original first-cast expiration point because the second cast refreshed it.");
                Assert.AreEqual(buffedMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Move speed should stay buffed until the refreshed duration expires.");

                harness.TickSeconds(1.1f);
                Assert.IsFalse(harness.HasActorBuff(playerActorId, passiveBuffId), "Passive speed buff should expire after the refreshed 2 second window.");
                Assert.AreEqual(baseMoveSpeed, harness.GetActorMoveSpeed(playerActorId), 0.01f, "Move speed should return to base after the refreshed passive buff expires.");
            }
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSpawnSnapshot(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSpawnSnapshot(harness, templateId, out var entry))
                {
                    return entry;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile spawn snapshot missing for template {templateId} within {maxTicks} ticks.");
            return default;
        }

        private static bool TryCollectProjectileSpawnSnapshot(MobaSkillConfigTestHarness harness, int templateId, out MobaProjectileEventSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);

            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;

                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].Kind != (int)ProjectileEventKind.Spawn) continue;
                    if (entries[j].TemplateId != templateId) continue;

                    entry = entries[j];
                    return true;
                }
            }

            return false;
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileExitSnapshot(MobaSkillConfigTestHarness harness, int projectileId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileExitSnapshot(harness, projectileId, out var entry))
                {
                    return entry;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile exit snapshot missing for projectile {projectileId} within {maxTicks} ticks.");
            return default;
        }

        private static bool TryCollectProjectileExitSnapshot(MobaSkillConfigTestHarness harness, int projectileId, out MobaProjectileEventSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);

            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;

                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].Kind != (int)ProjectileEventKind.Exit) continue;
                    if (entries[j].ProjectileId != projectileId) continue;

                    entry = entries[j];
                    return true;
                }
            }

            return false;
        }

        private static bool TryCollectActorTransformSnapshot(MobaSkillConfigTestHarness harness, int actorId, out MobaActorTransformSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);

            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ActorTransform) continue;

                var entries = MobaActorTransformSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].ActorId != actorId) continue;

                    entry = entries[j];
                    return true;
                }
            }

            return false;
        }

        private static Vec3 TickUntilActorPositionXGreaterThan(MobaSkillConfigTestHarness harness, int actorId, float minX, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var actor = harness.AssertActorEntity(actorId);
                Assert.IsTrue(actor.hasTransform, "Actor should keep a transform while waiting for projectile movement.");
                var position = actor.transform.Value.Position;
                if (position.X > minX)
                {
                    return position;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            var finalActor = harness.AssertActorEntity(actorId);
            return finalActor.transform.Value.Position;
        }

        private static void DrainSnapshots(MobaSkillConfigTestHarness harness)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
        }

        private static void TickUntilActorBuff(MobaSkillConfigTestHarness harness, int actorId, int buffId, int maxTicks, string message)
        {
            if (harness.TryGetActorBuffRemainingSeconds(actorId, buffId, out _)) return;

            for (var i = 0; i < maxTicks; i++)
            {
                harness.Tick(1);
                if (harness.TryGetActorBuffRemainingSeconds(actorId, buffId, out _)) return;
            }

            Assert.Fail(message);
        }

        private static float TickUntilActorBuffRemainingAtLeast(MobaSkillConfigTestHarness harness, int actorId, int buffId, float minRemainingSeconds, int maxTicks, string message)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (harness.TryGetActorBuffRemainingSeconds(actorId, buffId, out var remainingSeconds) && remainingSeconds >= minRemainingSeconds)
                {
                    return remainingSeconds;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail(message);
            return 0f;
        }

        [Test]
        public void Skill10020301_RuntimeResetAfterRecast_ShouldRestartRepeatHitDecay()
        {
            var expectation = new MobaAcceptanceExpectation
            {
                caseId = "skill_10020301_runtime_reset_after_recast",
                description = "小乔三技能在上一次持续结束后再次释放时，应重新从首段满伤开始计算重复命中减伤",
                worldId = "skill_10020301_runtime_reset_after_recast_world",
                tickRate = 30,
                accelerated = true,
                category = "contract",
                tags = new[] { "xiao-qiao", "skill-3", "buff", "scenario", "runtime-reset" },
                scenario = new MobaAcceptanceScenarioExpectation
                {
                    scenarioId = "skill_10020301_runtime_reset_after_recast",
                    name = "小乔三技能二次释放应重置衰减",
                    description = "第一次释放在同一 runtime 内造成 80+40，等待持续结束并复位目标生命后，第二次释放应再次造成 80+40，而不是继承上次命中次数。",
                    worldId = "skill_10020301_runtime_reset_after_recast_world",
                    tickRate = 30,
                    accelerated = true,
                    category = "contract",
                    tags = new[] { "xiao-qiao", "skill-3", "buff", "scenario", "runtime-reset" },
                    actors = new[]
                    {
                        new MobaAcceptanceActorExpectation
                        {
                            alias = "caster",
                            playerId = "p1",
                            teamId = 1,
                            heroId = 1002,
                            attributeTemplateId = 1002,
                            skillIds = new[] { 10020101, 10020201, 10020301 },
                            hasSpawnPosition = true,
                            spawnPosition = new MobaAcceptanceVector3Expectation { x = 0f, y = 0f, z = 0f },
                            facingDirection = new MobaAcceptanceVector3Expectation { x = 1f, y = 0f, z = 0f }
                        },
                        new MobaAcceptanceActorExpectation
                        {
                            alias = "target",
                            playerId = "p2",
                            teamId = 2,
                            heroId = 1002,
                            attributeTemplateId = 1002,
                            hasSpawnPosition = true,
                            spawnPosition = new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = 0f },
                            facingDirection = new MobaAcceptanceVector3Expectation { x = -1f, y = 0f, z = 0f }
                        }
                    },
                    timeline = new[]
                    {
                        new MobaAcceptanceTimelineStepExpectation { stepId = "first_cast", atMs = 0, action = "press", actorAlias = "caster", slot = 3, note = "Cast the first skill 3 runtime." },
                        new MobaAcceptanceTimelineStepExpectation { stepId = "leave_after_first_three_hits", atMs = 3500, action = "move_to", actorAlias = "target", position = new MobaAcceptanceVector3Expectation { x = 20f, y = 0f, z = 0f }, note = "Leave the search radius after three hits while the first runtime finishes." },
                        new MobaAcceptanceTimelineStepExpectation { stepId = "return_after_cooldown", atMs = 10300, action = "move_to", actorAlias = "target", position = new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = 0f }, note = "Return after the first runtime and the configured cooldown have ended." },
                        new MobaAcceptanceTimelineStepExpectation { stepId = "reset_target_hp", atMs = 10301, action = "set_attr", actorAlias = "target", property = "hp", value = 180f, note = "Reset target HP before the second independent runtime." },
                        new MobaAcceptanceTimelineStepExpectation { stepId = "second_cast", atMs = 10400, action = "press", actorAlias = "caster", slot = 3, note = "Cast skill 3 again; repeat-hit decay must restart." },
                        new MobaAcceptanceTimelineStepExpectation { stepId = "wait_for_second_three_hits", atMs = 10401, action = "wait", durationMs = 3500, note = "Observe the first three hits from the second runtime." }
                    },
                    stateExpectations = new[]
                    {
                        new MobaAcceptanceStateExpectation
                        {
                            alias = "target",
                            property = "hp",
                            comparator = "eq",
                            expectedFloat = 86.67f,
                            tolerance = new MobaAcceptanceVector3Expectation { x = 0.01f, y = 0.01f, z = 0.01f },
                            note = "After reset, the second runtime should independently apply three decayed hits."
                        }
                    }
                },
                mustContain = new[]
                {
                    new MobaAcceptanceTraceExpectation { kind = "SkillCast", configId = 10020301, minCount = 2, maxCount = 2 },
                    new MobaAcceptanceTraceExpectation { kind = "EffectExecution", configId = 10020301, minCount = 2, maxCount = 2 },
                    new MobaAcceptanceTraceExpectation { kind = "BuffApply", configId = 10020301, minCount = 2, maxCount = 2 },
                    new MobaAcceptanceTraceExpectation { kind = "DamageApply", configId = 10020301, minCount = 6, maxCount = 6 }
                },
                relationships = new[]
                {
                    new MobaAcceptanceRelationshipExpectation { parentKind = "SkillCast", parentConfigId = 10020301, childKind = "EffectExecution", childConfigId = 10020301 },
                    new MobaAcceptanceRelationshipExpectation { parentKind = "EffectExecution", parentConfigId = 10020301, childKind = "BuffApply", childConfigId = 10020301 },
                    new MobaAcceptanceRelationshipExpectation { parentKind = "EffectExecution", parentConfigId = 10020301, childKind = "DamageApply", childConfigId = 10020301 }
                }
            };

            var summary = RunExpectation(expectation);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.buffApplied);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var effectRoots = MobaAcceptanceTraceAssert.CollectEffectRootIds(records, 10020301);
            Assert.AreEqual(2, effectRoots.Count, "the recast reset scenario should create exactly two Xiao Qiao skill 3 effect roots.");
            MobaAcceptanceTraceAssert.AssertRepeatHitDamagePattern(records, effectRoots[0], 10020301, expectedHitCount: 3, "the first Xiao Qiao skill 3 runtime should apply exactly three ordered hits before the target leaves range.");
            MobaAcceptanceTraceAssert.AssertRepeatHitDamagePattern(records, effectRoots[1], 10020301, expectedHitCount: 3, "the second Xiao Qiao skill 3 runtime should restart repeat-hit decay with exactly three ordered hits.");
        }
    }

    public sealed class XiaoQiaoProjectileReturnTests
    {
        [Test]
        public void ReturningProjectile_ShouldIgnoreOutwardMaxDistanceAndExitWhenReturnedToLauncher()
        {
            var world = new ProjectileWorld(new NoHitCollisionWorld());
            world.SetReturnTargetProvider(new FixedReturnTargetProvider(new Vec3(0f, 0f, 0f)));

            var exits = new List<ProjectileExitEvent>();
            world.Spawn(new ProjectileSpawnParams(
                ownerId: 1,
                templateId: 30020101,
                launcherActorId: 100,
                rootActorId: 100,
                spawnFrame: 0,
                position: new Vec3(0f, 0f, 0f),
                direction: Vec3.Right,
                speed: 18f,
                returnAfterFrames: 20,
                returnSpeed: 22f,
                returnStopDistance: 1f,
                lifetimeFrames: 90,
                maxDistance: 12f,
                collisionLayerMask: 0,
                ignoreCollider: default,
                hitPolicyKind: ProjectileHitPolicyKind.Pierce,
                hitPolicyParam: 0,
                hitsRemaining: 8));

            for (var frame = 0; frame < 90 && exits.Count == 0; frame++)
            {
                world.Tick(frame, 1f / 30f, hitEvents: null, exitEvents: exits, tickEvents: null);
            }

            Assert.AreEqual(1, exits.Count, "Returning fan projectile should eventually exit exactly once.");
            Assert.AreEqual(ProjectileExitReason.ReturnArrived, exits[0].Reason, "The return leg should not consume the outward MaxDistance budget.");
            Assert.AreEqual(0, world.ActiveCount, "Projectile should be despawned after it returns to the launcher.");
        }

        private sealed class FixedReturnTargetProvider : IProjectileReturnTargetProvider
        {
            private readonly Vec3 _position;

            public FixedReturnTargetProvider(in Vec3 position)
            {
                _position = position;
            }

            public bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position)
            {
                position = _position;
                return true;
            }

            public void Dispose()
            {
            }
        }

        private sealed class NoHitCollisionWorld : ICollisionWorld
        {
            public ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerId) => new ColliderId(1);
            public bool Remove(ColliderId id) => true;
            public bool UpdateTransform(ColliderId id, in Transform3 transform) => true;
            public bool UpdateShape(ColliderId id, in ColliderShape localShape) => true;
            public bool UpdateLayer(ColliderId id, int layerId) => true;
            public bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape) => true;
            public bool ShouldCollide(int layerA, int layerB) => false;

            public bool GetLayer(ColliderId id, out int layerId)
            {
                layerId = 0;
                return false;
            }

            public bool Raycast(in Ray3 ray, float maxDistance, in LayerFilter filter, out AbilityKit.Core.Mathematics.RaycastHit hit)
            {
                hit = default;
                return false;
            }

            public int OverlapSphere(in Sphere sphere, in LayerFilter filter, List<ColliderId> results) => 0;
        }
    }
}
