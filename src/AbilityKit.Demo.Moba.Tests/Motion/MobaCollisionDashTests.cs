using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Motion;

public sealed class MobaCollisionDashTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enemy_selection_passes_friend_then_stops_exact_source_even_on_final_step(bool grid)
    {
        ICollisionWorld world = grid ? new GridCollisionWorld(2f, 16) : new NaiveCollisionWorld();
        var actors = new MobaActorRegistry();
        var context = new ActorContext();
        AddActor(1, (Team)1, 0);
        AddActor(2, (Team)1, 2);
        var enemy = AddActor(3, (Team)2, 5);
        var source = new MobaCollisionDashMotionSource(new FixedDeltaMotionSource(new Vec3(100, 0, 0), 0.1f,
            10, MotionGroups.Ability, MotionStacking.ExclusiveHighestPriority,
            new MotionCollisionConstraints(true, false, MotionEndOverlapPolicy.AllowInside, 0.5f, 0,
                MobaCollisionLayers.UnitMask, 0, selectionKey: 1), true), default);
        using var pipeline = new MotionPipeline { Solver = new ConfigurableMotionSolver(new MobaMotionCollisionWorldAdapter(world, actors), null) };
        var unrelated = new FixedDeltaMotionSource(Vec3.Zero, 3, 1, MotionGroups.PassiveDisplacement, MotionStacking.Additive);
        pipeline.AddSource(source);
        pipeline.AddSource(unrelated);
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        source.ExportSnapshot(out var snapshot);
        var result = pipeline.Tick(1, ref state, 0.1f, ref output);
        Assert.Equal(enemy.Value, result.Hit.TargetId);
        Assert.Same(source, output.CollisionSource);
        Assert.InRange(state.Position.X, 3.99f, 4.01f);
        Assert.True(source.TryClaimCollision());
        Assert.False(source.TryClaimCollision());
        Assert.True(unrelated.IsActive);
        pipeline.Tick(1, ref state, 0.1f, ref output);
        Assert.InRange(state.Position.X, 3.99f, 4.01f);
        source.ImportSnapshot(snapshot);
        Assert.True(source.IsActive);
        Assert.True(source.TryClaimCollision());

        ColliderId AddActor(int id, Team team, float x)
        {
            var position = new Vec3(x, 0, 0);
            var transform = new Transform3(position, Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateSphere(Vec3.Zero, 0.5f);
            var collider = world.Add(in transform, in shape, MobaCollisionLayers.UnitId);
            var entity = context.CreateEntity();
            entity.AddActorId(id); entity.AddTeam(team); entity.AddEntityMainType(EntityMainType.Unit); entity.AddCollisionId(collider);
            actors.Register(id, entity);
            return collider;
        }
    }
}
