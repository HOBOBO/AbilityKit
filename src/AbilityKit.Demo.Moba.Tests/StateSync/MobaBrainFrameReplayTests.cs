using System.Reflection;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Input;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class MobaBrainFrameReplayTests
{
    [Fact]
    public void Production_btree_replays_each_formal_system_frame_with_same_runtime_and_motion()
    {
        const float delta = 1f / 30f;
        using var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        try
        {
            for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick(delta);
            battle.SetupBattle();
            for (var i = 0; i < 10; i++) battle.Tick(delta);
            var world = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var summons = world.Services.Resolve<MobaSummonService>();
            var brains = world.Services.Resolve<MobaBrainService>();
            var root = actors.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform);
            var start = root.Value.transform.Value.Position + new Vec3(-4, 0, -6);
            var heading = new Vec3(.6f, 0, .8f);
            var source = default(SummonSourceContext);
            Assert.True(summons.TrySummon(root.Key, 1008, in start, in heading, in source, out var id));
            battle.Tick(delta);
            Assert.True(actors.TryGet(id, out var actor));
            var runtime = brains.EnsureBehavior(actor);
            Assert.NotNull(runtime);
            var provider = new MobaBrainRollbackProvider(actors, brains);
            var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(world), new RollbackSnapshotRingBuffer(2));
            var frame = new FrameIndex(battle.Context.LastFrame);
            Assert.True(coordinator.CaptureAndStore(frame));
            var brainState = provider.Export(frame);
            var initialIntent = MobaBrainIntentReader.Read(runtime);
            var initialPosition = actor.transform.Value.Position;
            var expected = new List<(byte[] State, Vec3 Position, MobaActorIntent Intent, byte[] Core, byte[] World)>();
            for (var i = 0; i < 6; i++)
            {
                battle.Tick(delta);
                expected.Add((provider.Export(new FrameIndex(battle.Context.LastFrame)), actor.transform.Value.Position,
                    MobaBrainIntentReader.Read(runtime), runtime.CaptureRecoveryState(),
                    WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(battle.Context.LastFrame)))));
            }
            Assert.True(Vec3.Dot(actor.transform.Value.Position - initialPosition, heading) > .05f);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.True(battle.TryRestoreSimulationFrame(coordinator, frame, out var result), result.ToString());
                Assert.Same(runtime, brains.EnsureBehavior(actor));
                Assert.Equal(brainState, provider.Export(frame));
                Assert.Equal(initialPosition, actor.transform.Value.Position);
                brains.Tick(0f, frame.Value + 100);
                Assert.Equal(initialIntent, MobaBrainIntentReader.Read(runtime));
                Assert.Equal(brainState, provider.Export(frame));
                for (var i = 0; i < expected.Count; i++)
                {
                    battle.Tick(delta);
                    Assert.Equal(frame.Value + i + 1, battle.Context.LastFrame);
                    Assert.Equal(expected[i].State, provider.Export(new FrameIndex(battle.Context.LastFrame)));
                    Assert.Equal(expected[i].Position, actor.transform.Value.Position);
                    Assert.Equal(expected[i].Intent, MobaBrainIntentReader.Read(runtime));
                    Assert.Equal(expected[i].Core, runtime.CaptureRecoveryState());
                    Assert.Equal(expected[i].World,
                        WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(battle.Context.LastFrame))));
                }
            }
        }
        finally { battle.Stop(); }
    }
}
