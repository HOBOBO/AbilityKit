using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Motion.Tests;

/// <summary>
/// combat.motion 包的 MotionPipeline + LocomotionMotionSource + ConfigurableMotionSolver
/// 全链路冒烟测试。验证基本移动产生 + 碰撞停止合约。
/// 使用纯内存 AABB 世界的假墙，无需外部 ICollisionWorld 依赖。
/// </summary>
public sealed class MotionPipelineSmokeTests
{
    [Fact]
    public void Pipeline_with_locomotion_produces_movement_without_wall()
    {
        var pipeline = new MotionPipeline();
        var loco = new LocomotionMotionSource(speed: 5f, space: MotionInputSpace.World, priority: 0);
        pipeline.AddSource(loco);
        loco.SetInput(1f, 0f);  // +X

        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();

        pipeline.Tick(1, ref state, 0.1f, ref output);  // dt=0.1, 期望位移 0.5

        // 无障碍时应到达 desired delta ≈ 0.5（+X）
        Assert.Equal(0.5f, state.Position.X, 4);
        Assert.Equal(0f, state.Position.Y);
        Assert.Equal(0f, state.Position.Z);
    }

    [Fact]
    public void Pipeline_stops_at_wall_via_solver()
    {
        var world = new FakeWallWorld(wallX: 0.4f, normal: new Vec3(-1f, 0f, 0f));
        var solver = new ConfigurableMotionSolver(world,
            (_, in _, in _, _) => new MotionConstraints(
                new MotionCollisionConstraints(enable: true, allowPassThrough: false,
                    endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
                    radius: 0.5f, skin: 0f, obstacleMask: 0, ignoreMask: 0,
                    slideAlongWalls: false, maxSlideIterations: 1),
                MotionLeashConstraints.Disabled));

        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput { DesiredDelta = new Vec3(2f, 0f, 0f) };
        solver.Solve(1, state, output, 0.1f);

        // 应在墙前停下（appliedDelta.X 被钳制）。
        Assert.InRange(output.AppliedDelta.X, 0f, 1.5f);
    }

    /// <summary>
    /// 假 1D 世界：sweep 在 wallX 处命中原点左侧墙（normal 向左），overlap 在超界位置为真。
    /// </summary>
    private sealed class FakeWallWorld : IMotionCollisionWorld
    {
        private readonly float _wallX;
        private readonly Vec3 _normal;

        public FakeWallWorld(float wallX, Vec3 normal) { _wallX = wallX; _normal = normal; }

        public bool Sweep(int moverId, in Vec3 start, in Vec3 desiredDelta, float radius, int obstacleMask, int ignoreMask, out MotionHit hit, out Vec3 appliedDelta)
        {
            var end = start + desiredDelta;
            var dir = desiredDelta.Magnitude > 0f ? desiredDelta / desiredDelta.Magnitude : Vec3.Zero;
            if (start.X > _wallX && end.X <= _wallX)
            {
                var t = (start.X - _wallX) / (start.X - end.X);  // 时间比例
                appliedDelta = desiredDelta * t;
                hit = new MotionHit(true, 1, _normal, t);
                return true;
            }
            hit = MotionHit.None;
            appliedDelta = Vec3.Zero;
            return false;
        }

        public bool Overlap(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask) => position.X <= _wallX;

        public bool TryProjectToFree(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask, out Vec3 projected)
        { projected = position; return false; }

        public bool TryProjectToFreeDirectional(int moverId, in Vec3 from, in Vec3 to, float radius, int obstacleMask, int ignoreMask, out Vec3 projected)
        { projected = to; return false; }
    }
}
