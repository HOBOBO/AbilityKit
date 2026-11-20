using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.Navigation;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 经正式世界装配验证局部恢复调试投影，并保持正常路径规划时机和冻结参数。
/// </summary>
[Collection("Moba lifecycle preparation")]
public sealed class MobaPathFollowingSystemStateTests
{
    [Fact]
    public void Formal_system_keeps_repath_cadence_threshold_and_frozen_speed_with_optional_local_restore()
    {
        var start = BattleStartConfig.CreateDefault();
        foreach (var player in start.Players) player.EnableBrainOnSpawn = false;
        using var battle = new ConsoleBattleBootstrapper(start);
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();
        var services = battle.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var owner = actors.Entries.First(p => p.Value.hasTeam && p.Value.hasTransform).Value;
        foreach (var pair in actors.Entries)
            if (pair.Value.hasTransform) Place(pair.Value, new Vec3(40f + pair.Key, 0, 40));
        Place(owner, new Vec3(4, 0, -10));
        var spawn = new Vec3(-10, 0, -10);
        Assert.True(services.Resolve<MobaSummonService>().TrySummon(owner.actorId.Value, 30030502,
            in spawn, Vec3.Right, default, out var id));
        for (var i = 0; i < 8; i++) battle.Tick();
        Assert.True(actors.TryGet(id, out var summon));
        var runtime = services.Resolve<MobaPathFollowingRuntimeService>();
        var debug = services.Resolve<NavigationDebugState>();
        Assert.True(runtime.TryGet(id, out var binding));
        var capture = runtime.CaptureState(id);
        var sourceState = capture.State.Source.Value;
        binding.Data.LastTarget = Vec3.Zero;
        binding.Data.FramesSinceRepath = 20;
        binding.Data.Waypoints = null;
        debug.ActivePaths.Clear();
        var position = summon.transform.Value.Position;
        runtime.RestoreState(capture);
        Assert.Equal(position, summon.transform.Value.Position);
        Assert.Equal(capture.State.LastTarget, binding.Data.LastTarget);
        Assert.Equal(capture.State.FramesSinceRepath, binding.Data.FramesSinceRepath);
        var entry = Assert.Single(debug.ActivePaths, path => path.ActorId == id);
        Assert.Equal(sourceState.Points, entry.Waypoints);
        Assert.NotSame(sourceState.Points, entry.Waypoints);
        entry.Waypoints[0] = new Vec3(999, 0, 999);
        Assert.NotEqual(entry.Waypoints[0], binding.Source.CaptureState().Points[0]);
        runtime.RestoreState(capture);

        var revision = binding.Revision;
        new MobaAttrs(summon).SetBase(BattleAttributeType.MOVE_SPEED, sourceState.Speed * 1.2f);
        var frames = binding.Data.FramesSinceRepath;
        Assert.InRange(frames, 0, 19);
        for (var i = frames; i < 20; i++)
        {
            battle.Tick();
            Assert.Equal(i + 1, binding.Data.FramesSinceRepath);
            Assert.Equal(revision, binding.Revision);
            Assert.Equal(sourceState.Speed, binding.Source.CaptureState().Speed);
        }
        battle.Tick();
        Assert.Equal(0, binding.Data.FramesSinceRepath);
        Assert.True(binding.Revision > revision);
        Assert.Equal(new MobaAttrs(summon).MoveSpeed, binding.Source.CaptureState().Speed);

        revision = binding.Revision;
        var target = binding.Data.LastTarget;
        Place(owner, target + new Vec3(.5f, 0, 0));
        battle.Tick();
        Assert.Equal(revision, binding.Revision);
        Assert.Equal(1, binding.Data.FramesSinceRepath);
        Place(owner, target + new Vec3(1.5f, 0, 0));
        battle.Tick();
        Assert.True(binding.Revision > revision);
        Assert.Equal(0, binding.Data.FramesSinceRepath);
        Assert.Equal(target + new Vec3(1.5f, 0, 0), binding.Data.LastTarget);
    }

    private static void Place(ActorEntity actor, Vec3 position) => actor.ReplaceTransform(
        new Transform3(position, actor.transform.Value.Rotation, actor.transform.Value.Scale));
}
