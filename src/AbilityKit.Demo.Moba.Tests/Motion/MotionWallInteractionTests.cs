using System;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Motion;

public sealed class MotionWallInteractionTests
{
    [Fact]
    public void Pass_through_inside_endpoint_projects_along_direction()
    {
        // 终点 X>4 视为"在墙内"；方向投影返回固定边界点 (4,0,0)。
        var world = new MockProjectionWorld
        {
            IsInside = p => p.X > 4f,
            ProjectedPoint = new Vec3(4f, 0f, 0f),
            ProjectResult = true,
        };
        var solver = new ConfigurableMotionSolver(world, (_, in _, in _, _) => MotionConstraints.Disabled);

        var policy = PassPolicy();
        var result = solver.Resolve(0, Vec3.Zero, new Vec3(10f, 0f, 0f), policy);

        // 落点投影到边界：applied = projected - start = (4,0,0)。
        Assert.InRange(result.AppliedDelta.X, 3.99f, 4.01f);
        Assert.Equal(0f, result.AppliedDelta.Z);
    }

    [Fact]
    public void Pass_through_free_endpoint_keeps_full_delta()
    {
        var world = new MockProjectionWorld { IsInside = _ => false };
        var solver = new ConfigurableMotionSolver(world, (_, in _, in _, _) => MotionConstraints.Disabled);

        var result = solver.Resolve(0, Vec3.Zero, new Vec3(10f, 0f, 0f), PassPolicy());

        // 终点空闲：不投影，全位移保留。
        Assert.InRange(result.AppliedDelta.X, 9.99f, 10.01f);
    }

    [Fact]
    public void Continuous_dash_passes_through_wall_over_multiple_frames()
    {
        var world = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddBox(world, new Vec3(4f, 0f, 0f), new Vec3(0.5f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);
        var pipeline = new MotionPipeline { Solver = solver };
        var source = CreateContinuousPassSource(new Vec3(10f, 0f, 0f), 0.8f);
        pipeline.AddSource(source);

        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        for (var i = 0; i < 8; i++)
        {
            pipeline.Tick(1, ref state, 0.1f, ref output);
        }

        Assert.InRange(state.Position.X, 7.99f, 8.01f);
        Assert.False(adapter.Overlap(1, in state.Position, 0.5f, MobaCollisionLayers.WorldMask, 0));
    }

    [Fact]
    public void Continuous_dash_projects_inside_final_endpoint_to_nearest_wall_edge_only_when_completed()
    {
        var world = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddBox(world, new Vec3(4.5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);
        var pipeline = new MotionPipeline { Solver = solver };
        var source = CreateContinuousPassSource(new Vec3(8f, 0f, 0f), 0.6f);
        pipeline.AddSource(source);

        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        for (var i = 0; i < 5; i++)
        {
            pipeline.Tick(1, ref state, 0.1f, ref output);
        }

        Assert.True(source.IsActive);
        Assert.InRange(state.Position.X, 3.99f, 4.01f);
        Assert.True(adapter.Overlap(1, in state.Position, 0.5f, MobaCollisionLayers.WorldMask, 0));

        pipeline.Tick(1, ref state, 0.1f, ref output);

        Assert.False(source.IsActive);
        Assert.InRange(state.Position.X, 5.99f, 6.01f);
        Assert.False(adapter.Overlap(1, in state.Position, 0.5f, MobaCollisionLayers.WorldMask, 0));
    }

    [Fact]
    public void Adapter_nearest_projection_accounts_for_mover_radius()
    {
        var world = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddBox(world, new Vec3(4.5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var inside = new Vec3(4.8f, 0f, 0f);

        var ok = adapter.TryProjectToFree(
            1,
            in inside,
            0.5f,
            MobaCollisionLayers.WorldMask,
            0,
            out var projected);

        Assert.True(ok);
        Assert.InRange(projected.X, 5.99f, 6.01f);
        Assert.InRange(projected.Z, -0.01f, 0.01f);
        Assert.False(adapter.Overlap(1, in projected, 0.5f, MobaCollisionLayers.WorldMask, 0));
    }

    [Fact]
    public void Adapter_directional_projection_finds_boundary_against_box_wall()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);

        var ok = adapter.TryProjectToFreeDirectional(0, Vec3.Zero, new Vec3(5f, 0f, 0f), 0.5f, MobaCollisionLayers.WorldMask, 0, out var projected);

        Assert.True(ok);
        Assert.InRange(projected.X, 0.5f, 3.9f);                  // 在 from 与墙之间
        Assert.False(adapter.Overlap(0, in projected, 0.5f, MobaCollisionLayers.WorldMask, 0)); // 投影点空闲
    }

    [Fact]
    public void Blink_resolve_pass_wall_lands_outside_wall()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        // 终点 (5,0,0) 落在墙内 → 穿墙策略沿方向投影到墙前边界。
        var result = solver.Resolve(0, Vec3.Zero, new Vec3(5f, 0f, 0f), PassPolicy());
        var landed = new Vec3(result.AppliedDelta.X, 0f, result.AppliedDelta.Z);

        Assert.InRange(landed.X, 0.5f, 3.9f);                     // 投影到墙前，未停在墙内
        Assert.False(adapter.Overlap(0, in landed, 0.5f, MobaCollisionLayers.WorldMask, 0));
    }

    [Fact]
    public void Blink_resolve_block_wall_clamps_before_wall()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        // block：不穿墙，全距 sweep 钳到墙前。
        var blockPolicy = new MotionCollisionConstraints(
            enable: true, allowPassThrough: false,
            endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
            radius: 0.5f, skin: 0f,
            obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
            slideAlongWalls: false, maxSlideIterations: 1);
        var result = solver.Resolve(0, Vec3.Zero, new Vec3(10f, 0f, 0f), blockPolicy);

        Assert.InRange(result.AppliedDelta.X, 3.0f, 4.0f);        // 墙前停止（5 - 1 - 0.5 ≈ 3.5）
    }

    [Fact]
    public void Sweep_from_resting_contact_does_not_block_when_moving_away()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);

        // 贴边：移动体（半径 0.5）停在墙前 X=3.5（墙 X∈[4,6]），盒心恰好落在膨胀盒边界上。
        var resting = new Vec3(3.5f, 0f, 0f);

        // 朝远离墙体的方向（-X）扫掠：不应被阻挡，完整位移保留——修复前这里会被误判为碰撞。
        var away = adapter.Sweep(0, in resting, new Vec3(-5f, 0f, 0f), 0.5f,
            MobaCollisionLayers.WorldMask, 0, out var awayHit, out var awayApplied);

        Assert.False(away);
        Assert.False(awayHit.Hit);
        Assert.Equal(new Vec3(-5f, 0f, 0f), awayApplied);

        // 朝墙体内（+X）扫掠：仍应被阻挡在贴边（applied≈0），且命中法向指向移动体一侧（-X）。
        var into = adapter.Sweep(0, in resting, new Vec3(5f, 0f, 0f), 0.5f,
            MobaCollisionLayers.WorldMask, 0, out var intoHit, out var intoApplied);

        Assert.True(into);
        Assert.True(intoHit.Hit);
        Assert.InRange(intoApplied.X, -0.01f, 0.01f);
        Assert.InRange(intoHit.Normal.X, -1.01f, -0.99f);
    }

    [Fact]
    public void Solver_lets_mover_escape_wall_after_resting_contact()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(1f, 2f, 2f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        // 移动体被钳到墙边后停在 X=3.5（墙 X∈[4,6]，半径 0.5）。
        var resting = new Vec3(3.5f, 0f, 0f);

        // 反向（远离墙）：应能完整脱离，不再被当作碰撞卡住。
        var away = solver.Resolve(0, resting, new Vec3(-2f, 0f, 0f), SlideConstraints());
        Assert.InRange(away.AppliedDelta.X, -2.01f, -1.99f);

        // 正向（撞墙）：仍被正确挡住，几乎不动。
        var into = solver.Resolve(0, resting, new Vec3(2f, 0f, 0f), SlideConstraints());
        Assert.InRange(into.AppliedDelta.X, -0.01f, 0.01f);
    }

    [Fact]
    public void Solver_slides_along_aabb_wall_when_approaching_at_an_angle()
    {
        var world = new NaiveCollisionWorld();
        AddBox(world, new Vec3(5f, 0f, 0f), new Vec3(0.5f, 2f, 5f), MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        // 斜向撞墙：位移同时含 +X(撞墙) 与 +Z(沿墙)。
        var start = new Vec3(0f, 0f, -3f);
        var desired = new Vec3(10f, 0f, 6f);

        var noSlide = solver.Resolve(0, start, desired, NoSlideConstraints());
        var slide = solver.Resolve(0, start, desired, SlideConstraints());

        // 滑行应带来更多位移（保留沿墙分量），而不是停在墙前。
        Assert.True(slide.AppliedDelta.SqrMagnitude > noSlide.AppliedDelta.SqrMagnitude + 1f);
        Assert.InRange(slide.AppliedDelta.Z, 5f, 6.01f);   // 沿墙分量基本保留
        Assert.InRange(slide.AppliedDelta.X, 3.5f, 4.5f);  // 撞墙分量被挡（墙前 4）
    }

    [Fact]
    public void Solver_slides_along_rotated_obb_wall_when_approaching_at_an_angle()
    {
        var world = new NaiveCollisionWorld();
        var rot = Quat.FromAxisAngle(Vec3.Up, 45f * (float)Math.PI / 180f);
        var tr = new Transform3(new Vec3(8f, 0f, 0f), rot, Vec3.One);
        var obbShape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, new Vec3(0.5f, 2f, 8f));
        world.Add(in tr, in obbShape, MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        // 斜向撞旋转墙：+X(撞墙) + +Z(沿墙方向之一)。
        var start = new Vec3(0f, 0f, -4f);
        var desired = new Vec3(14f, 0f, 8f);

        var noSlide = solver.Resolve(0, start, desired, NoSlideConstraints());
        var slide = solver.Resolve(0, start, desired, SlideConstraints());

        Assert.True(slide.Hit.Hit);
        // 滑行位移明显大于不滑行（沿墙保留了切向分量）。
        Assert.True(slide.AppliedDelta.SqrMagnitude > noSlide.AppliedDelta.SqrMagnitude + 1f);
    }

    [Fact]
    public void Mover_slides_along_rotated_blocker_over_many_frames()
    {
        // 仿真实运行：GridCollisionWorld + 旋转 OBB 挡板 + 默认滑行约束，持续 +X 输入多帧。
        // 应沿挡板滑行（持续位移、越过挡板），而不是贴着停住。
        var world = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        var rot = Quat.FromAxisAngle(Vec3.Up, 30f * (float)Math.PI / 180f);
        var tr = new Transform3(new Vec3(5f, 0f, 2f), rot, Vec3.One);
        var obbShape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, new Vec3(1.5f, 1f, 3f));
        world.Add(in tr, in obbShape, MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => MotionConstraints.Disabled);

        var pos = new Vec3(0f, 0f, 2f);
        var dir = new Vec3(1f, 0f, 0f);
        var posAt50 = pos;
        for (int i = 0; i < 60; i++)
        {
            var result = solver.Resolve(0, pos, dir * 0.5f, SlideConstraints());
            pos = pos + result.AppliedDelta;
            if (i == 49) posAt50 = pos;
        }

        // 滑行：越过挡板(X>10)且最后 10 帧仍在移动(未卡死)。
        Assert.True(pos.X > 10f, $"mover did not slide past blocker, pos={pos}");
        Assert.True((pos - posAt50).SqrMagnitude > 1f, $"mover got stuck, pos={pos}");
    }

    [Fact]
    public void Pipeline_with_locomotion_source_slides_along_rotated_obb()
    {
        // 真实游戏路径：MotionPipeline + LocomotionMotionSource + 默认滑行约束 + 运行时 cellSize=4。
        // 持续 +X 输入撞旋转 OBB，应沿其滑行越过，而非停住。
        var world = new GridCollisionWorld(cellSize: 4f, initialCapacity: 16);
        var rot = Quat.FromAxisAngle(Vec3.Up, 30f * (float)Math.PI / 180f);
        var tr = new Transform3(new Vec3(8f, 0f, 2f), rot, Vec3.One);
        var obbShape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, new Vec3(1.5f, 1f, 3f));
        world.Add(in tr, in obbShape, MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => new MotionConstraints(SlideConstraints(), MotionLeashConstraints.Disabled));

        var pipeline = new MotionPipeline { Solver = solver };
        var loco = new LocomotionMotionSource(speed: 5f, space: MotionInputSpace.World, priority: 0);
        pipeline.AddSource(loco);
        loco.SetInput(1f, 0f);

        var state = new MotionState(new Vec3(0f, 0f, 2f));
        var output = new MotionOutput();
        var posAt49 = state.Position;
        for (int i = 0; i < 60; i++)
        {
            pipeline.Tick(1, ref state, 0.1f, ref output);
            if (i == 49) posAt49 = state.Position;
        }

        Assert.True(state.Position.X > 12f, $"did not slide past blocker: {state.Position}");
        Assert.True((state.Position - posAt49).SqrMagnitude > 1f, $"got stuck: {state.Position}");
    }

    [Fact]
    public void Pipeline_axis_boundary_wall_angled_input_slides()
    {
        // 用户场景：键盘/摇杆(Locomotion) 斜向撞 轴对齐边界墙(北墙)——应沿墙滑行(X 增大)，而非停住。
        var world = new GridCollisionWorld(cellSize: 4f, initialCapacity: 16);
        var wallTr = new Transform3(new Vec3(0f, 1f, 12f), Quat.Identity, Vec3.One);
        var wallShape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, new Vec3(18f, 1f, 0.5f));
        world.Add(in wallTr, in wallShape, MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var solver = new ConfigurableMotionSolver(adapter, (_, in _, in _, _) => new MotionConstraints(SlideConstraints(), MotionLeashConstraints.Disabled));

        var pipeline = new MotionPipeline { Solver = solver };
        var loco = new LocomotionMotionSource(speed: 5f, space: MotionInputSpace.World, priority: 0);
        pipeline.AddSource(loco);
        loco.SetInput(1f, 1f);   // 斜向(+X,+Z) 朝北墙

        var state = new MotionState(new Vec3(0f, 0f, 5f));
        var output = new MotionOutput();
        for (int i = 0; i < 40; i++)
            pipeline.Tick(1, ref state, 0.1f, ref output);

        // 沿墙滑行：X 明显增大，Z 被挡在墙前(~11)。
        Assert.True(state.Position.X > 10f, $"did not slide along wall: {state.Position}");
        Assert.InRange(state.Position.Z, 10.5f, 11.5f);
    }

    private static MotionCollisionConstraints NoSlideConstraints() => new MotionCollisionConstraints(
        enable: true, allowPassThrough: false,
        endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
        radius: 0.5f, skin: 0f,
        obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
        slideAlongWalls: false, maxSlideIterations: 1);

    private static MotionCollisionConstraints SlideConstraints() => new MotionCollisionConstraints(
        enable: true, allowPassThrough: false,
        endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
        radius: 0.5f, skin: 0f,
        obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
        slideAlongWalls: true, maxSlideIterations: 2);

    private static MotionCollisionConstraints PassPolicy() => new MotionCollisionConstraints(
        enable: true, allowPassThrough: true,
        endOverlapPolicy: MotionEndOverlapPolicy.ProjectAlongDirection,
        radius: 0.5f, skin: 0f,
        obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
        slideAlongWalls: false, maxSlideIterations: 1);

    private static MotionCollisionConstraints ContinuousPassPolicy() => new MotionCollisionConstraints(
        enable: true, allowPassThrough: true,
        endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
        radius: 0.5f, skin: 0f,
        obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
        slideAlongWalls: false, maxSlideIterations: 1);

    private static MotionCollisionConstraints ContinuousPassCompletionPolicy() => new MotionCollisionConstraints(
        enable: true, allowPassThrough: true,
        endOverlapPolicy: MotionEndOverlapPolicy.ProjectToNearestFree,
        radius: 0.5f, skin: 0f,
        obstacleMask: MobaCollisionLayers.WorldMask, ignoreMask: 0,
        slideAlongWalls: false, maxSlideIterations: 1);

    private static FixedDeltaMotionSource CreateContinuousPassSource(in Vec3 velocity, float duration) =>
        new FixedDeltaMotionSource(
            velocity,
            duration,
            priority: 20,
            groupId: MotionGroups.Ability,
            stacking: MotionStacking.ExclusiveHighestPriority,
            collisionPolicy: ContinuousPassPolicy(),
            hasCollisionPolicy: true,
            completionCollisionPolicy: ContinuousPassCompletionPolicy(),
            hasCompletionCollisionPolicy: true);

    private static ColliderId AddBox(ICollisionWorld world, in Vec3 center, in Vec3 halfExtents, int layerId)
    {
        var transform = new Transform3(center, Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateAabb(-halfExtents, halfExtents);
        return world.Add(in transform, in shape, layerId);
    }

    private sealed class MockProjectionWorld : IMotionCollisionWorld
    {
        public Func<Vec3, bool> IsInside = _ => false;
        public Vec3 ProjectedPoint = Vec3.Zero;
        public bool ProjectResult;

        public bool Sweep(int moverId, in Vec3 start, in Vec3 desiredDelta, float radius, int obstacleMask, int ignoreMask, out MotionHit hit, out Vec3 appliedDelta)
        {
            hit = MotionHit.None;
            appliedDelta = desiredDelta;
            return false;
        }

        public bool Overlap(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask) => IsInside(position);

        public bool TryProjectToFree(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
        {
            projectedPosition = position;
            return false;
        }

        public bool TryProjectToFreeDirectional(int moverId, in Vec3 from, in Vec3 to, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
        {
            projectedPosition = ProjectedPoint;
            return ProjectResult;
        }
    }
}
