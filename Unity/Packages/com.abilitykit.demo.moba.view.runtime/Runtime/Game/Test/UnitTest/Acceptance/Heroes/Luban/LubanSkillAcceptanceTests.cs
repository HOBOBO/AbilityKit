using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;
using System.Collections.Generic;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class LubanSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract Luban = new HeroSkillContract(
            "Luban", 1008, 1008, new[] { 10080101, 10080201, 10080301 });
        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10080101, 10080101, 10080101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10080201, 10080201, 10080201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10080301, 10080301, 10080301);

        [Test]
        public void Passive10080000_ShouldCountFourBasicAttacksAndLaunchSweep()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_passive_sweep_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetPassiveSkill(10080000, out _));
                Assert.IsTrue(harness.Config.TryGetProjectile(30080001, out var basicProjectile));
                Assert.AreEqual(10080012, basicProjectile.OnHitEffectId,
                    "Luban basic projectile must resolve the damage trigger rather than recursively firing another projectile.");
                Assert.IsTrue(harness.Config.TryGetProjectile(30080301, out var sweepProjectile));
                Assert.AreEqual(10080004, sweepProjectile.OnHitEffectId,
                    "Luban sweep projectile must resolve the sweep damage trigger rather than recursively firing another sweep.");
                Assert.IsTrue(harness.Config.TryGetProjectileLauncher(31080301, out var sweepLauncher));
                Assert.AreEqual(3, sweepLauncher.CountPerShot,
                    "Luban passive sweep should emit three projectiles.");
                Assert.AreEqual(35f, sweepLauncher.FanAngleDeg,
                    "Luban passive sweep should use a spread fan rather than a single straight projectile.");
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080002,
                    (int)TriggeringConstants.AdvanceGameplayCounterId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080003,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                harness.EnterGameAndWarmup(reason: "luban passive fourth basic attack sweep contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: 3f);
                for (var i = 0; i < 4; i++)
                {
                    HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 2f);
                    harness.Tick(1);
                }
                var hpAfterFourthAttack = harness.GetActorHp(targetActorId);
                var trace = harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, 10080003, 10,
                    "Luban fourth basic attack should execute the sweep trigger.");
                harness.AssertActionExecutedUnderEffect(trace.RootId,
                    (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
                harness.AssertProjectileLaunchedUnderEffect(trace.RootId, 31080301, 30080301);
                Assert.AreEqual(3, TickAndCountProjectileSpawns(harness, 30080301, 30),
                    "Luban passive sweep should create all three projectiles in its fan.");
                Assert.Less(harness.GetActorHp(targetActorId), hpAfterFourthAttack,
                    "Luban passive sweep should damage the enemy after the fourth basic attack.");
            }
        }

        [Test]
        public void Skill10080101_ShouldLaunchGrenadeAndSpawnStunArea()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_1_grenade_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetAoe(40080101, out _));
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill1.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080111,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080112,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.EnterGameAndWarmup(reason: "luban grenade area contract");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                var targetHp = harness.GetActorHp(targetActorId);
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill1,
                    "luban grenade area contract", aimDir: Vec3.Right);
                harness.AssertProjectileLaunchedUnderEffect(effect.RootId, 31080101, 30080101);
                harness.TickUntilTraceNode(MobaExecutionKind.AreaSpawn, 40080101, 40,
                    "Luban grenade projectile should create its delayed explosion area.");
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, 10080101, 20,
                    "Luban grenade area should apply damage on entry.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp,
                    "Luban grenade explosion should damage the enemy.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10080101, 0.5f,
                    "Luban grenade explosion should apply the stun Buff.");
            }
        }

        [Test]
        public void Skill10080201_ShouldLaunchSharkCannonAndKnockBackOnHit()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_2_cannon_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill2.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080211,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.PullId.Value);
                harness.EnterGameAndWarmup(reason: "luban shark cannon contract");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: -8f);
                var targetHp = harness.GetActorHp(targetActorId);
                var targetStart = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill2,
                    "luban shark cannon contract", aimDir: new Vec3(0f, 0f, -8f), targetActorId: targetActorId);
                harness.AssertProjectileLaunchedUnderEffect(effect.RootId, 31080201, 30080201);
                var spawn = TickUntilProjectileSnapshot(harness, 30080201, ProjectileEventKind.Spawn, 30);
                Assert.Less(spawn.ForwardZ, -0.9f, $"Luban shark cannon should use the requested clear downward lane. forward=({spawn.ForwardX:F2},{spawn.ForwardZ:F2})");
                CaptureProjectileHitAndExit(harness, spawn.ProjectileId, 40, out var hit, out var exit);
                Assert.Greater(hit.ProjectileId, 0,
                    $"Luban shark cannon should produce a hit event. exitReason={exit.ExitReason}, hitCollider={hit.HitCollider}, spawn=({spawn.X:F2},{spawn.Z:F2}), exit=({exit.X:F2},{exit.Z:F2})");
                Assert.AreEqual((int)ProjectileExitReason.Hit, exit.ExitReason,
                    $"Luban shark cannon should hit before max distance. exitReason={exit.ExitReason}, hitCollider={exit.HitCollider}, hit=({hit.X:F2},{hit.Z:F2}), spawn=({spawn.X:F2},{spawn.Z:F2}), exit=({exit.X:F2},{exit.Z:F2}), target=({targetStart.X:F2},{targetStart.Z:F2})");
                harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, 10080211, 5,
                    "Luban shark cannon hit should execute its OnHit damage trigger.");
                harness.Tick(2);
                AssertDamageTraceExists(harness, effect.RootId, 10080201,
                    "Luban shark cannon should damage its first enemy hit.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp,
                    "Luban shark cannon should damage the enemy.");
                var targetEnd = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                Assert.Less(targetEnd.Z, targetStart.Z - 0.1f,
                    "Luban shark cannon should knock the enemy away from the caster.");
            }
        }

        [Test]
        public void Skill10080301_ShouldSpawnAirshipWithBombSkill()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_3_airship_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSummon(1008, out var summon));
                CollectionAssert.Contains(summon.SkillIds, 10080401);
                Assert.AreEqual(100, summon.BrainId);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill3.EffectId,
                    (int)TriggeringConstants.SpawnSummonId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080401,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                harness.EnterGameAndWarmup(reason: "luban airship summon contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: -8f);
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill3,
                    "luban airship summon contract", aimDir: new Vec3(0f, 0f, -8f), targetActorId: targetActorId);
                harness.TickUntilTraceNode(MobaExecutionKind.SummonSpawn, 1008, 20,
                    "Luban ultimate should spawn the airship summon.");
                harness.AssertActionExecutedUnderEffect(effect.RootId,
                    (int)TriggeringConstants.SpawnSummonId.Value, TriggeringConstants.Actions.SpawnSummon);
                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var summonActorIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(actorId, summonActorIds, 1008));
                Assert.AreEqual(1, summonActorIds.Count, "Luban ultimate should create one airship summon.");
                harness.TickUntilTraceNode(MobaExecutionKind.ProjectileLaunch, 30080401, 120,
                    "The airship Brain should periodically cast its bomb skill.");
                var bombSpawn = TickUntilProjectileSnapshot(harness, 30080401, ProjectileEventKind.Spawn, 30);
                CaptureProjectileHitAndExit(harness, bombSpawn.ProjectileId, 60, out var bombHit, out var bombExit);
                Assert.Greater(bombHit.ProjectileId, 0,
                    $"The airship bomb should produce a hit event. exitReason={bombExit.ExitReason}, hitCollider={bombHit.HitCollider}, spawn=({bombSpawn.X:F2},{bombSpawn.Z:F2}), exit=({bombExit.X:F2},{bombExit.Z:F2})");
                Assert.AreEqual((int)ProjectileExitReason.Hit, bombExit.ExitReason,
                    $"The airship bomb should hit its enemy before max distance. exitReason={bombExit.ExitReason}, hitCollider={bombExit.HitCollider}, hit=({bombHit.X:F2},{bombHit.Z:F2}), spawn=({bombSpawn.X:F2},{bombSpawn.Z:F2}), exit=({bombExit.X:F2},{bombExit.Z:F2})");
                harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, 10080411, 5,
                    "The airship bomb hit should execute its OnHit damage trigger.");
                harness.Tick(2);
                AssertDamageTraceExists(harness, effect.RootId, 10080401,
                    "The airship bomb should damage its enemy target.");
            }
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSnapshot(
            MobaSkillConfigTestHarness harness,
            int id,
            ProjectileEventKind kind,
            int maxTicks,
            bool byProjectileId = false)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].Kind != (int)kind) continue;
                        if (byProjectileId ? entries[e].ProjectileId != id : entries[e].TemplateId != id) continue;
                        return entries[e];
                    }
                }

                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile snapshot missing. id={id}, kind={kind}, maxTicks={maxTicks}");
            return default;
        }

        private static int TickAndCountProjectileSpawns(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var count = 0;
            for (var tick = 0; tick <= maxTicks; tick++)
            {
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].Kind == (int)ProjectileEventKind.Spawn && entries[e].TemplateId == templateId)
                            count++;
                    }
                }

                if (tick < maxTicks) harness.Tick(1);
            }

            return count;
        }

        private static void CaptureProjectileHitAndExit(
            MobaSkillConfigTestHarness harness,
            int projectileId,
            int maxTicks,
            out MobaProjectileEventSnapshotEntry hit,
            out MobaProjectileEventSnapshotEntry exit)
        {
            hit = default;
            exit = default;
            for (var i = 0; i <= maxTicks; i++)
            {
                var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].ProjectileId != projectileId) continue;
                        if (entries[e].Kind == (int)ProjectileEventKind.Hit) hit = entries[e];
                        else if (entries[e].Kind == (int)ProjectileEventKind.Exit) exit = entries[e];
                    }
                }

                if (hit.ProjectileId > 0 && exit.ProjectileId > 0) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile hit/exit snapshots incomplete. projectileId={projectileId}, hit={hit.ProjectileId}, exit={exit.ProjectileId}, maxTicks={maxTicks}");
        }

        private static void AssertDamageTraceExists(
            MobaSkillConfigTestHarness harness,
            long rootId,
            int reasonParam,
            string message)
        {
            var summary = string.Empty;
            var count = 0;
            foreach (var node in harness.Trace.GetNodesByKind((int)MobaExecutionKind.DamageApply))
            {
                if (node.Metadata?.ConfigId != reasonParam) continue;
                count++;
                summary += $"[{node.RootId}:{node.Metadata?.ConfigId}:{node.Metadata?.SourceActorId}->{node.Metadata?.TargetActorId}]";
            }

            Assert.Greater(count, 0, message + $" castRoot={rootId}, damageTrace={summary}");
        }
    }
}
