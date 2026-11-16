using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using PresentationCueStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;

namespace AbilityKit.Game.Tests
{
    public sealed class JhinRModeGroundVfxTests
    {
        [TestCase("timeout")]
        [TestCase("four_shots")]
        [TestCase("removed")]
        public void R_RealModeOwnsGroundAcrossShortCastsAndClearsAtModeEnd(string ending)
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 30010401 },
                worldId: "jhin_r_ground_" + ending, heroId: 3001, attributeTemplateId: 3001);
            harness.EnterGameAndWarmup();
            var actorId = harness.AssertPlayerActorBound();
            var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
            var world = new EntityWorld();
            var root = world.Create("r-ground-vfx");
            var owner = world.Create("r-ground-owner");
            var pose = new BattleTransformComponent { Position = new Vector3(2, 0, 3), Forward = Vector3.right };
            owner.WithRef(pose);
            var lookup = new BattleEntityLookup();
            lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(actorId), owner);
            var query = new BattleEntityQuery(world, lookup);
            var manager = new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
            var handler = new BattlePresentationCueViewEventHandler(world, query, manager, root);
            GameObject ground = null;
            var started = false;
            var stopped = false;
            var shots = 0;
            var elapsedTicks = 0;
            var nextShot = harness.TickRate * 2;
            var removed = false;
            try
            {
                var cast = skills.TryCastBySlot(actorId, 4, default, Vec3.Right, 0);
                Assert.IsTrue(cast.Success, cast.FailReason);
                for (; elapsedTicks < harness.TickRate * 12 && !stopped; elapsedTicks++)
                {
                    if (ending == "four_shots" && elapsedTicks >= nextShot && shots < 4)
                    {
                        var shot = skills.TryCastBySlot(actorId, 5, default, Vec3.Right, 0);
                        Assert.IsTrue(shot.Success, "R shot " + shots + ": " + shot.FailReason);
                        shots++;
                        nextShot += Mathf.CeilToInt(harness.TickRate * 1.2f);
                    }
                    if (ending == "removed" && elapsedTicks >= harness.TickRate * 3 && !removed)
                    {
                        Assert.IsTrue(harness.World.Services.Resolve<MobaBuffService>().RemoveBuffImmediate(
                            actorId, 32010404, actorId, MobaExecutionEndReason.Cancelled));
                        removed = true;
                    }
                    harness.Tick(1);
                    var snapshots = new List<WorldStateSnapshot>();
                    harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                        .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                    foreach (var snapshot in snapshots)
                    {
                        if (snapshot.OpCode != MobaOpCodes.Snapshot.PresentationCue) continue;
                        foreach (var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload))
                        {
                            if (entry.TemplateId != 90013021) continue;
                            var cue = PresentationCueSnapshotMapper.Map(in entry);
                            Assert.AreEqual(PresentationCueDomain.Buff, cue.Domain);
                            handler.HandleSnapshot(new[] { cue });
                            if (cue.Stage == PresentationCueStage.Started)
                            {
                                Assert.IsFalse(started, "Independent R shots must not recreate the mode visual.");
                                started = true;
                                Assert.AreEqual(1, root.ChildCount);
                                var entity = root.GetChild(0);
                                Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                                ground = view.GameObject;
                                Assert.IsTrue(entity.TryGetRef(out BattlePresentationLifetimeComponent life));
                                Assert.AreEqual("ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Ground", life.Request.ResourceKey);
                                Assert.AreEqual(PresentationCompletionPolicy.Manual, life.Request.Lifecycle.Completion);
                                Assert.AreEqual(actorId, life.Request.Attachment.ActorId);
                                handler.HandleSnapshot(new[] { cue });
                                Assert.AreEqual(1, root.ChildCount, "Replayed cue must not duplicate the ground.");
                            }
                            if (BattlePresentationCueResolver.ShouldStop(cue.Stage))
                            {
                                stopped = true;
                                Assert.IsTrue(ground == null || !ground.activeInHierarchy);
                                Assert.AreEqual(0, root.ChildCount);
                            }
                        }
                    }
                    manager.Tick(root, null, query);
                    if (started && !stopped)
                    {
                        Assert.IsTrue(ground != null && ground.activeInHierarchy,
                            "Entry/shot Cast completion must not remove the persistent R ground.");
                        Assert.AreEqual(1, root.ChildCount);
                        Assert.Less(Vector3.Distance(pose.Position, ground.transform.position), 0.01f);
                        Assert.Greater(Vector3.Dot(pose.Forward, ground.transform.forward), 0.999f);
                        ground.GetComponent<Animator>().Update(1f / harness.TickRate);
                    }
                }
                Assert.IsTrue(started, "The real R mode must publish a ground cue.");
                Assert.IsTrue(stopped, "Mode end must clean up the ground cue.");
                Assert.IsFalse(harness.HasActorBuff(actorId, 32010404));
                if (ending == "four_shots") Assert.AreEqual(4, shots);
                if (ending == "timeout") Assert.Greater(elapsedTicks, harness.TickRate * 9);
            }
            finally
            {
                handler.Clear(); manager.Clear(root);
                if (root.IsValid) root.Destroy();
                if (owner.IsValid) owner.Destroy();
            }
        }
    }
}
