using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Motion;

public sealed class ConfigurableMotionSolverSlideTests
{
    [Fact]
    public void Slide_off_clamps_to_wall_without_tangential_progress()
    {
        var solver = MakeSolver(slide: false);
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput { DesiredDelta = new Vec3(10f, 0f, 2f) };

        var result = solver.Solve(0, in state, in output, 0.016f);

        Assert.InRange(result.AppliedDelta.X, 3.9f, 4.1f);
        // 未开滑动：整个对角位移按接触比例缩放，Z 分量被压缩。
        Assert.InRange(result.AppliedDelta.Z, 0.7f, 0.9f);
    }

    [Fact]
    public void Slide_on_keeps_tangential_progress_along_wall()
    {
        var solver = MakeSolver(slide: true);
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput { DesiredDelta = new Vec3(10f, 0f, 2f) };

        var result = solver.Solve(0, in state, in output, 0.016f);

        Assert.InRange(result.AppliedDelta.X, 3.9f, 4.1f);
        // 默认恢复率为 0：保持原有投影语义，沿墙的 Z 分量几乎完整保留。
        Assert.InRange(result.AppliedDelta.Z, 1.9f, 2.1f);
    }

    [Fact]
    public void Full_speed_recovery_redirects_remaining_horizontal_distance_along_wall()
    {
        var solver = MakeSolver(slide: true, wallSlideSpeedRecovery: 1f);
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput { DesiredDelta = new Vec3(5f, 0f, 5f) };

        var result = solver.Solve(0, in state, in output, 0.016f);

        Assert.InRange(result.AppliedDelta.X, 3.9f, 4.1f);
        // 碰撞前进 (4,4)，剩余 (1,1) 的长度 sqrt(2) 被重定向到墙切线。
        Assert.InRange(result.AppliedDelta.Z, 5.40f, 5.43f);
    }

    [Fact]
    public void Pass_through_ignores_wall_slide_speed_recovery_and_keeps_full_delta()
    {
        var solver = MakeSolver(
            slide: true,
            wallSlideSpeedRecovery: 1f,
            allowPassThrough: true);
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput { DesiredDelta = new Vec3(10f, 0f, 2f) };

        var result = solver.Solve(0, in state, in output, 0.016f);

        Assert.Equal(10f, result.AppliedDelta.X);
        Assert.Equal(2f, result.AppliedDelta.Z);
        Assert.False(result.Hit.Hit);
    }

    private static ConfigurableMotionSolver MakeSolver(
        bool slide,
        float wallSlideSpeedRecovery = 0f,
        bool allowPassThrough = false)
    {
        var world = new MockSlideMotionWorld();
        return new ConfigurableMotionSolver(
            world,
            (moverId, in state, in input, dt) => new MotionConstraints(
                new MotionCollisionConstraints(
                    enable: true,
                    allowPassThrough: allowPassThrough,
                    endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
                    radius: 0.5f,
                    skin: 0f,
                    obstacleMask: 1,
                    ignoreMask: 0,
                    slideAlongWalls: slide,
                    maxSlideIterations: 2,
                    wallSlideSpeedRecovery: wallSlideSpeedRecovery),
                MotionLeashConstraints.Disabled));
    }

    /// <summary>
    /// 可控碰撞世界：x=WallX 处有一面沿 Z 方向无限延伸的墙，法向 (-1,0,0)。
    /// 仅阻塞 +X 方向跨越，Z 方向自由（用于验证切向滑动）。
    /// </summary>
    private sealed class MockSlideMotionWorld : IMotionCollisionWorld
    {
        private const float WallX = 4f;

        public bool Sweep(
            int moverId,
            in Vec3 start,
            in Vec3 desiredDelta,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out MotionHit hit,
            out Vec3 appliedDelta)
        {
            appliedDelta = desiredDelta;
            hit = MotionHit.None;

            if (desiredDelta.X > 0f && start.X + desiredDelta.X > WallX)
            {
                var fraction = (WallX - start.X) / desiredDelta.X;
                if (fraction < 0f) fraction = 0f;
                if (fraction > 1f) fraction = 1f;
                appliedDelta = new Vec3(desiredDelta.X * fraction, desiredDelta.Y * fraction, desiredDelta.Z * fraction);
                hit = new MotionHit(true, 1, new Vec3(-1f, 0f, 0f), fraction);
                return true;
            }

            return false;
        }

        public bool Overlap(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask) => false;

        public bool TryProjectToFree(
            int moverId,
            in Vec3 position,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out Vec3 projectedPosition)
        {
            projectedPosition = position;
            return false;
        }

        public bool TryProjectToFreeDirectional(
            int moverId,
            in Vec3 from,
            in Vec3 to,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out Vec3 projectedPosition)
        {
            projectedPosition = to;
            return false;
        }
    }
}
