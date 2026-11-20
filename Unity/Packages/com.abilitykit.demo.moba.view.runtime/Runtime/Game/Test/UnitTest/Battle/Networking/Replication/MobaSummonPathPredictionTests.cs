using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Network.Runtime;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 在 Unity 进程中用正式战斗世界验证存活召唤物的 Path 回滚。
    /// </summary>
    public sealed class MobaSummonPathPredictionTests
    {
        /// <summary>
        /// 使用发布的召唤模板和行为树，在自然重规划边界前后连续纠偏两次并逐帧校验。
        /// </summary>
        [Test]
        public void WardenPathReplansIdenticallyAcrossTwoPredictionCorrections()
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 10010101, 10010201, 10010301 }, worldId: "warden_path_prediction",
                heroId: 1001, attributeTemplateId: 1001);
            harness.EnterGameAndWarmup(10);
            var world = harness.World;
            var services = world.Services;
            var ownerId = harness.AssertPlayerActorBound();
            var owner = harness.AssertActorEntity(ownerId);
            owner.ReplaceTransform(new Transform3(new Vec3(4, 0, -10),
                owner.transform.Value.Rotation, owner.transform.Value.Scale));
            var spawn = new Vec3(-10, 0, -10);
            Assert.That(services.Resolve<MobaSummonService>().TrySummon(
                ownerId, 30030502, in spawn, Vec3.Right, default, out var movingId), Is.True);
            harness.Tick(8);

            var movingActor = harness.AssertActorEntity(movingId);
            Assert.That(services.Resolve<IMobaActorBrainCatalog>().TryGet(
                movingActor.actorBrain.BrainId, out var definition), Is.True);
            Assert.That(definition.DriverKind, Is.EqualTo(MobaBrainDriverKeys.BehaviorTree));
            Assert.That(definition.DecisionName, Is.EqualTo("summon_warden_bt"));
            var path = services.Resolve<MobaPathFollowingRuntimeService>();
            Assert.That(path.TryGet(movingId, out var active), Is.True);
            Assert.That(active.Source.IsActive, Is.True);
            for (var step = 0; step < 25 && active.Data.FramesSinceRepath != 18; step++) harness.Tick(1);
            Assert.That(active.Data.FramesSinceRepath, Is.EqualTo(18));

            var registry = MobaRollbackRegistryBuilder.CreateForPrediction(world);
            Assert.IsTrue(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var lifecycle));
            Assert.IsTrue(registry.TryGet(MobaActorMotionRollbackProvider.DefaultKey, out var motion));
            Assert.IsTrue(registry.TryGet(MobaBrainRollbackProvider.DefaultKey, out var brain));
            using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
            var hostOptions = new HostRuntimeOptions();
            var host = new HostRuntime(harness.Worlds, hostOptions);
            var first = harness.FrameTime.Frame.Value + 1;
            var last = first + 3;
            var injectedMismatch = first + 1;
            var gate = services.Resolve<MobaLogicWorldRunGateService>();
            var actors = services.Resolve<MobaActorRegistry>();
            var calculator = new MobaAuthoritativeStateHashCalculator();
            uint BattleHash() => calculator.Compute(gate.InGame, actors);
            var prediction = new ClientPredictionDriverModule(_ => remote, _ => null,
                resolveIdealFrameLimit: _ => last, minPredictionWindow: 4, enableRollback: true,
                buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
                buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == injectedMismatch ? 1u : 0u)));
            prediction.Install(host, hostOptions);
            try
            {
                hostOptions.WorldCreated.Invoke(world);
                Assert.IsTrue(prediction.TryRebase(world.Id, new FrameIndex(first - 1)));
                var baseline = new Dictionary<int, (byte[] Lifecycle, byte[] Motion, byte[] Brain, uint Hash, Vec3 Position, int Age, long Revision)>();
                for (var frame = first; frame <= last; frame++)
                {
                    Advance(frame, confirmed: false);
                    baseline.Add(frame, Observe(frame));
                }
                Assert.AreNotEqual(baseline[first].Position, baseline[last].Position);
                Assert.IsTrue(baseline[last].Revision > baseline[first].Revision);
                Assert.AreEqual(20, baseline[first + 1].Age);
                Assert.AreEqual(0, baseline[first + 2].Age);
                injectedMismatch = first + 2;
                Correct(first, 1, supplyAuthority: true);
                injectedMismatch = -1;
                Correct(first + 1, 2, supplyAuthority: false);
                TestContext.WriteLine($"summon_warden_bt: checkpoints={first},{first + 1}; " +
                    $"replanFrame={first + 2}; lastFrame={last}; rollbacks={prediction.TotalRollbackCount}; " +
                    $"restoreFailures={prediction.TotalRollbackRestoreFailed}; finalHash={baseline[last].Hash:X8}");

                void Correct(int checkpoint, int count, bool supplyAuthority)
                {
                    prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(checkpoint + 1),
                        new WorldStateHash(baseline[checkpoint + 1].Hash));
                    Assert.AreEqual(count, prediction.TotalRollbackCount);
                    Assert.AreEqual(0, prediction.TotalRollbackRestoreFailed);
                    Assert.AreEqual(checkpoint, prediction.LastRollbackFrame.Value);
                    Assert.IsTrue(prediction.IsReplaying);
                    Compare(checkpoint);
                    for (var frame = checkpoint + 1; frame <= last; frame++)
                    {
                        if (supplyAuthority) remote.Add(frame, Array.Empty<PlayerInputCommand>());
                        Advance(frame, confirmed: true);
                        Compare(frame);
                    }
                    host.Tick(harness.FixedDelta);
                    Assert.IsFalse(prediction.IsReplaying);
                }

                void Compare(int frame)
                {
                    var expected = baseline[frame];
                    var actual = Observe(frame);
                    Assert.That(actual.Lifecycle, Is.EqualTo(expected.Lifecycle), $"Lifecycle frame {frame}");
                    Assert.That(actual.Motion, Is.EqualTo(expected.Motion), $"Motion frame {frame}");
                    Assert.That(actual.Brain, Is.EqualTo(expected.Brain), $"Brain frame {frame}");
                    Assert.That(actual.Hash, Is.EqualTo(expected.Hash), $"Hash frame {frame}");
                    Assert.That(actual.Position, Is.EqualTo(expected.Position), $"Position frame {frame}");
                    Assert.That(actual.Age, Is.EqualTo(expected.Age), $"Age frame {frame}");
                }

                (byte[] Lifecycle, byte[] Motion, byte[] Brain, uint Hash, Vec3 Position, int Age, long Revision) Observe(int frame)
                {
                    var slot = path.CaptureState(movingId);
                    Assert.IsTrue(slot.State.IsPipelineMember);
                    Assert.IsTrue(slot.Source.IsActive);
                    var index = new FrameIndex(frame);
                    return (lifecycle.Export(index), motion.Export(index), brain.Export(index), BattleHash(), movingActor.transform.Value.Position,
                        slot.State.FramesSinceRepath, slot.Revision);
                }

                void Advance(int frame, bool confirmed)
                {
                    host.Tick(harness.FixedDelta);
                    Assert.IsTrue(prediction.TryGetFrames(world.Id, out var confirmedFrame, out var predictedFrame));
                    Assert.AreEqual(frame, predictedFrame.Value);
                    if (confirmed) Assert.AreEqual(frame, confirmedFrame.Value);
                }
            }
            finally
            {
                prediction.Uninstall(host, hostOptions);
            }
        }
    }
}
