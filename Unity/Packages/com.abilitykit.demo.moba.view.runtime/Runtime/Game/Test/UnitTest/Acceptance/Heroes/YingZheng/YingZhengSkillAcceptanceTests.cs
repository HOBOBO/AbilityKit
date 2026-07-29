using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Projectile;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Trace;
using AbilityKit.Triggering.Runtime.Config.Plans;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class YingZhengSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract YingZheng = new HeroSkillContract(
            "YingZheng",
            heroId: 1006,
            attributeTemplateId: 1006,
            skillIds: new[] { 10060101, 10060201, 10060301 });

        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10060101, 10060101, 10060101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10060201, 10060201, 10060201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10060301, 10060301, 10060301);

        [Test]
        public void Passive10060000_FifthBasicAttackShouldDealEnhancedMagicDamage()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(YingZheng, "ying_zheng_passive_fifth_basic_contract_world"))
            {
                Assert.IsTrue(harness.TriggerPlans.TryGetRecordByTriggerId(10060002, out var counter), "Ying Zheng basic-attack counter trigger should exist.");
                Assert.AreEqual(TriggerPlanScope.OwnerBound, counter.Scope, "Ying Zheng basic-attack counter must remain bound to its passive owner.");
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10060002, (int)TriggeringConstants.AdvanceGameplayCounterId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10060003, (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "ying zheng passive fifth basic attack contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                for (var i = 0; i < 5; i++)
                {
                    var damage = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 2f);
                    Assert.IsNotNull(damage, "Ying Zheng passive setup should apply real basic-attack damage.");
                    Assert.Greater(damage.Value, 0f, "Ying Zheng passive setup basic-attack damage should be positive.");
                    harness.Tick(1);
                }

                var enhancedTrace = harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, 10060003, maxTicks: 10, message: "Ying Zheng fifth basic attack should execute its enhanced magic-damage trigger.");
                harness.AssertActionExecutedUnderEffect(enhancedTrace.RootId, (int)TriggeringConstants.GiveDamageId.Value, TriggeringConstants.Actions.GiveDamage);
                Assert.GreaterOrEqual(
                    harness.CountTraceNodesInRoot(enhancedTrace.RootId, MobaTraceKind.DamageApply, 10060003),
                    1,
                    "Ying Zheng fifth basic attack should apply enhanced damage to the original hit target.");
            }
        }

        [Test]
        public void Skill10060101_ShouldSpawnTargetAreaAndDamageOnInterval()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(YingZheng, "ying_zheng_skill_1_interval_area_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetAoe(40060101, out var area), "Ying Zheng skill 1 area config should exist.");
                CollectionAssert.Contains(area.OnDelayTriggerIds, 10060111, "Ying Zheng skill 1 should retain its initial sword-array damage.");
                CollectionAssert.Contains(area.OnIntervalTriggerIds, 10060111, "Ying Zheng skill 1 should damage targets at periodic sword-array intervals.");
                Assert.AreEqual(4f, area.Radius, 0.0001f, "Ying Zheng skill 1 sword array should use its four-meter target area radius.");
                Assert.AreEqual(500, area.IntervalMs, "Ying Zheng skill 1 sword array should tick every 500ms.");

                harness.EnterGameAndWarmup(reason: "ying zheng skill 1 interval area contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                var hpBefore = harness.GetActorHp(targetActorId);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill1.Slot, aimPos: new Vec3(3f, 0f, 0f), aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Ying Zheng skill 1 should cast at the selected target position. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, Skill1.EffectId, maxTicks: 30, message: "Ying Zheng skill 1 should execute its target-area effect.");
                harness.AssertAreaSpawnedUnderEffect(effectTrace.RootId, 40060101);
                harness.TickMilliseconds(850);
                Assert.Less(harness.GetActorHp(targetActorId), hpBefore, "Ying Zheng skill 1 sword array should damage a target inside the area.");
                Assert.GreaterOrEqual(harness.CountTraceNodesInRoot(effectTrace.RootId, MobaTraceKind.DamageApply, 10060101), 2, "Ying Zheng skill 1 should apply both initial and periodic sword-array damage.");
                Assert.IsTrue(harness.HasActorBuff(targetActorId, 10060101), "Ying Zheng skill 1 sword array should apply its slow to targets in the area.");
            }
        }

        [Test]
        public void Skill10060201_ShouldCleanseSlowGuardAndDamageNearbyEnemies()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(YingZheng, "ying_zheng_skill_2_guard_cleanse_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10060201,
                    (int)TriggeringConstants.RemoveBuffId.Value,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.SpawnAreaId.Value,
                    (int)TriggeringConstants.AddShieldId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10060211,
                    (int)TriggeringConstants.PullId.Value,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);

                Assert.IsTrue(harness.Config.TryGetAoe(40060201, out var area), "Ying Zheng skill 2 clearing area config should exist.");
                Assert.AreEqual(4f, area.Radius, 0.0001f, "Ying Zheng skill 2 should clear enemies in its four-meter self area.");

                harness.EnterGameAndWarmup(reason: "ying zheng skill 2 guard cleanse contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 2f);
                var targetPositionBefore = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                var targetHpBefore = harness.GetActorHp(targetActorId);
                var baseMoveSpeed = harness.GetActorMoveSpeed(actorId);
                var buffs = harness.World.Services.Resolve<MobaBuffService>();
                Assert.IsTrue(harness.Config.TryGetBuff(10060101, out var slowBuff), "Ying Zheng skill 1 slow Buff config should exist.");
                Assert.IsTrue(MobaGameplayTagCatalog.TryResolve("Debuff.Slow", out var slowTag), "Debuff.Slow should resolve from the gameplay tag catalog.");
                Assert.IsNotNull(slowBuff.Tags, "Ying Zheng skill 1 slow Buff should retain its configured tags.");
                Assert.IsTrue(slowBuff.Tags.HasTag(slowTag), "Ying Zheng skill 1 slow Buff should carry the Debuff.Slow tag at runtime.");
                Assert.IsTrue(buffs.ApplyBuffImmediate(actorId, 10060101, actorId, durationOverrideMs: 0), "Ying Zheng slow setup buff should apply before guard cleanse.");
                Assert.Less(harness.GetActorMoveSpeed(actorId), baseMoveSpeed, "The tagged slow setup should reduce movement speed before skill 2.");
                Assert.AreEqual(1, buffs.RemoveBuffsWithTagImmediate(actorId, "Debuff.Slow", sourceActorId: 0, removeAll: true, TraceLifecycleReason.Dispelled), "Tag-based buff removal should find the active slow regardless of source.");
                Assert.IsFalse(harness.HasActorBuff(actorId, 10060101), "Direct tag-based removal should clear the active slow.");
                Assert.IsTrue(buffs.ApplyBuffImmediate(actorId, 10060101, actorId, durationOverrideMs: 0), "Ying Zheng slow setup buff should reapply before skill 2 cleanse.");

                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill2, "ying zheng skill 2 guard cleanse contract");
                harness.AssertAreaSpawnedUnderEffect(effectTrace.RootId, 40060201);
                var nearbyEffectTrace = harness.TickUntilTraceNode(
                    MobaTraceKind.EffectExecution,
                    10060211,
                    maxTicks: 15,
                    message: "Ying Zheng skill 2 close-range area should execute its delayed nearby-enemy effect.");
                harness.AssertActionExecutedUnderEffect(nearbyEffectTrace.RootId, (int)TriggeringConstants.PullId.Value, TriggeringConstants.Actions.Pull);
                harness.Tick(12);
                Assert.IsFalse(harness.HasActorBuff(actorId, 10060101), "Ying Zheng skill 2 should remove active Buffs tagged Debuff.Slow.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10060201, 2.5f, "Ying Zheng skill 2 should apply its three-second guard move-speed Buff.");
                Assert.Greater(harness.GetActorMoveSpeed(actorId), baseMoveSpeed, "Ying Zheng skill 2 guard Buff should grant movement speed after cleansing slow.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHpBefore, "Ying Zheng skill 2 should damage enemies inside its close-range clearing area.");
                Assert.IsTrue(harness.HasActorBuff(targetActorId, 10060211), "Ying Zheng skill 2 should slow enemies inside its close-range clearing area.");
                var targetPositionAfter = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                Assert.Greater(targetPositionAfter.X, targetPositionBefore.X + 0.2f, "Ying Zheng skill 2 should knock nearby enemies outward from the caster.");
            }
        }

        [Test]
        public void Skill10060301_ShouldSpawnExactlyElevenFiveSwordWavesAlongLockedDirection()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(YingZheng, "ying_zheng_skill_3_locked_direction_launcher_contract_world"))
            {
                harness.AssertProjectileConfigExists(31060301, 30060301);
                Assert.IsTrue(harness.Config.TryGetProjectileLauncher(31060301, out var launcher), "Ying Zheng ultimate launcher config should exist.");
                Assert.AreEqual(2500, launcher.DurationMs, "Ying Zheng ultimate should sustain sword emission for 2.5 seconds.");
                Assert.AreEqual(250, launcher.IntervalMs, "Ying Zheng ultimate should emit sword waves every 250ms.");
                Assert.AreEqual(5, launcher.CountPerShot, "Ying Zheng ultimate should emit five swords per wave.");
                Assert.AreEqual(0f, launcher.FanAngleDeg, 0.0001f, "Ying Zheng ultimate should emit every sword in its locked cast direction without per-wave fan spread.");
                Assert.IsTrue(harness.Config.TryGetProjectile(30060301, out var projectile), "Ying Zheng ultimate projectile config should exist.");
                Assert.AreEqual("ying_zheng_ultimate_projectile", projectile.StateMachineProfileId);

                var actorId = harness.AssertPlayerActorBound();
                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill3, "ying zheng skill 3 locked direction launcher contract");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31060301, 30060301);
                Assert.AreEqual(55, CountProjectileSpawnsWithinTicks(harness, 30060301, maxTicks: 90), "Ying Zheng ultimate should launch exactly eleven five-sword waves without duplicate scheduler emissions.");
                harness.TickUntilSkillStops(actorId, Skill3.Slot, maxTicks: 180, message: "Ying Zheng ultimate should finish after its locked-direction sword stream expires rather than leave the skill pipeline running.");
            }
        }

        [Test]
        public void Skill10060301_ShouldExposeMovingProjectileActorAndConfiguredVfx()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(YingZheng, "ying_zheng_skill_3_projectile_presentation_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "ying zheng skill 3 projectile presentation contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill3.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Ying Zheng skill 3 should cast toward its selected direction. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, Skill3.EffectId, maxTicks: 30, message: "Ying Zheng skill 3 should execute its locked-direction launcher effect.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31060301, 30060301);
                var spawn = TickUntilProjectileSpawnSnapshot(harness, 30060301, maxTicks: 30);
                Assert.Greater(spawn.ProjectileActorId, 0, "Ying Zheng ultimate spawn snapshot should expose a projectile actor for VFX follow binding.");
                Assert.Greater(spawn.ForwardX, 0.9f, "Ying Zheng ultimate sword should face the cast direction.");

                var projectileActor = harness.AssertActorEntity(spawn.ProjectileActorId);
                Assert.IsTrue(projectileActor.hasTransform, "Ying Zheng ultimate projectile actor should have a transform for client-followed movement.");
                var initialPosition = projectileActor.transform.Value.Position;
                var positions = new List<Vec3>(19) { initialPosition };
                for (var i = 0; i < 18; i++)
                {
                    harness.Tick(1);
                    positions.Add(harness.AssertActorEntity(spawn.ProjectileActorId).transform.Value.Position);
                }

                var rearIndex = 0;
                for (var i = 1; i < positions.Count; i++)
                {
                    if (positions[i].X < positions[rearIndex].X) rearIndex = i;
                }

                var rearPosition = positions[rearIndex];
                Assert.Less(rearPosition.X, initialPosition.X - 0.5f, "Preparing should first move the sword behind the caster along the opposite cast direction.");
                var observedHold = false;
                for (var i = 1; i < positions.Count; i++)
                {
                    if (positions[i].X < initialPosition.X - 0.5f && Math.Abs(positions[i].X - positions[i - 1].X) <= 0.0001f)
                    {
                        observedHold = true;
                        break;
                    }
                }
                Assert.IsTrue(observedHold, "The sword should remain at its rear target for at least one simulation tick before attacking.");

                var moved = positions[positions.Count - 1];
                Assert.Greater(moved.X, rearPosition.X + 0.05f, "After holding, the attack state should move the sword along the locked cast direction.");
                Assert.IsTrue(TryCollectActorTransformSnapshot(harness, spawn.ProjectileActorId, out var transformEntry), "Ying Zheng moving ultimate projectile should be included in actor transform snapshots.");
                Assert.Greater(transformEntry.X, rearPosition.X + 0.05f, "Ying Zheng ultimate transform snapshot should carry its attack-state position for the view layer.");

                var resolver = new BattleProjectileVfxResolver();
                Assert.AreEqual(90006003, resolver.ResolveSnapshotVfxId(spawn.TemplateId, spawn.Kind), "Ying Zheng ultimate should resolve its configured flying-sword VFX.");
            }
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
                        if (entries[entryIndex].Kind == (int)ProjectileEventKind.Spawn && entries[entryIndex].TemplateId == templateId) count++;
                    }
                }

                if (i < maxTicks) harness.Tick(1);
            }

            return count;
        }
    }
}
