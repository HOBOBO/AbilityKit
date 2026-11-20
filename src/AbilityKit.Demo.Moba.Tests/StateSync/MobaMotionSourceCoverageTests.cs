using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData("path", false)]
    [InlineData("path", true)]
    [InlineData("collision-dash", false)]
    [InlineData("collision-dash", true)]
    [InlineData("fixed-delta", false)]
    [InlineData("fixed-delta", true)]
    [InlineData("foreign-locomotion", false)]
    [InlineData("foreign-locomotion", true)]
    public void Unowned_motion_runs_normally_but_capture_and_restore_reject_without_import(
        string kind, bool managedSibling)
    {
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(1, out var actor));
        if (managedSibling) AddMotion(3, 92002, ExactSource(20));
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var localTarget = _preparation.Capture(_frame);
        var pipeline = actor.motion.Pipeline;
        var position = actor.transform.Value.Position;
        IMotionSource source = kind switch
        {
            "path" => new PathFollowerMotionSource(new[] { position, position + new Vec3(100, 0, 0) }, 4),
            "collision-dash" => new MobaCollisionDashMotionSource(ExactSource(20), default),
            "fixed-delta" => ExactSource(20),
            _ => new LocomotionMotionSource(4),
        };
        if (source is LocomotionMotionSource locomotion) locomotion.SetInput(1, 0);
        pipeline.AddSource(source);

        /*
         * 未接入恢复的普通来源仍可经正式 System 推进，不能将回滚准入变成业务禁令。
         */
        var lastFrame = _bootstrapper.Context.LastFrame;
        _bootstrapper.Tick(1f / 30f);
        Assert.Equal(lastFrame + 1, _bootstrapper.Context.LastFrame);
        Assert.NotEqual(position, actor.transform.Value.Position);
        Assert.True(pipeline.ContainsSource(source));
        var frame = new FrameIndex(_bootstrapper.Context.LastFrame);
        var sources = pipeline.CaptureSourceOrder();
        var snapshotSource = Assert.IsAssignableFrom<IMotionSnapshotSource>(source);
        Assert.True(snapshotSource.ExportSnapshot(out var beforeSource));

        /*
         * 临时只拆下本测试来源以捕获其余完整世界；RemoveSource 不取消或推进来源。
         * 验收结束恢复相同来源次序，避免将不可捕获状态拿空载荷冒充。
         */
        Assert.True(pipeline.RemoveSource(source));
        var before = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame));
        pipeline.AddSource(source);
        pipeline.RestoreSourceOrder(sources);
        var beforeMotion = actor.motion;
        var beforePosition = actor.transform.Value.Position;
        var error = Assert.Throws<InvalidOperationException>(() => coordinator.Capture(frame));
        Assert.Contains("actor 1", error.Message);
        Assert.Contains(source.GetType().FullName!, error.Message);
        Assert.Throws<InvalidOperationException>(() => _preparation.Capture(frame));
        Assert.Throws<InvalidOperationException>(() => _preparation.Prepare(_frame, localTarget));

        Assert.False(coordinator.TryRestore(target, out var result));
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(sources, pipeline.CaptureSourceOrder());
        Assert.Same(beforeMotion, actor.motion);
        Assert.Equal(beforePosition, actor.transform.Value.Position);
        Assert.Equal(lastFrame + 1, _bootstrapper.Context.LastFrame);
        Assert.True(snapshotSource.ExportSnapshot(out var afterSource));
        Assert.Equal(beforeSource, afterSource);
        Assert.True(pipeline.RemoveSource(source));
        Assert.Equal(before, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame)));
        pipeline.AddSource(source);
        pipeline.RestoreSourceOrder(sources);
    }

    [Fact]
    public void Duplicate_owned_locomotion_is_rejected_outside_managed_pipeline()
    {
        var owned = OwnedLocomotion(1);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        actor.motion.Pipeline.AddSource(owned);
        var error = Assert.Throws<InvalidOperationException>(() => _preparation.Capture(_frame));
        Assert.Contains("actor 1", error.Message);
        Assert.Contains(nameof(LocomotionMotionSource), error.Message);
        Assert.Equal(2, actor.motion.Pipeline.CaptureSourceOrder().Count(s => ReferenceEquals(s, owned)));
    }
}
