using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using Xunit;
using MemoryPack;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileDestinationTests
{
    [Theory]
    [InlineData(11, 3f)]
    [InlineData(92, 8.5f)]
    public void Destination_preserves_identity_emits_once_and_restores_without_rearrival(int template, float distance)
    {
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        var spawn = Spawn(template, distance, ProjectileMaxDistanceResponse.SuspendAtDestination)
            .WithDirection(Vec3.Right).WithPosition(new Vec3(10, 0, 10)).WithSpeed(10)
            .WithSpawnFrame(1).WithLifetimeFrames(3).WithPatternSlot(0, 1)
            .WithHitPolicyParam(-1).WithTrackingArc(0).WithTrackingArcHeightPerUnit(0);
        var id = world.Spawn(spawn);
        var before = world.ExportRollback(default);
        var arrivals = new List<ProjectileArrivalEvent>();
        var exits = new List<ProjectileExitEvent>();
        world.Tick(2, 1f, null, exits, null, arrivals);
        Assert.Equal(id, Assert.Single(arrivals).Projectile);
        Assert.Empty(exits);
        Assert.True(world.TryGetRuntimeState(id, out var state));
        Assert.True(state.HasArrived);
        Assert.True(state.IsSuspended);
        Assert.InRange(state.Position.X, 10 + distance - .0001f, 10 + distance + .0001f);
        Assert.Equal(2, state.ArrivedFrame);
        Assert.False(world.ResumeSimulation(id));
        var after = world.ExportRollback(default);
        var restored = new ProjectileWorld(new NaiveCollisionWorld());
        restored.ImportRollback(default, after);
        arrivals.Clear();
        for (var frame = 3; frame < 20; frame++) restored.Tick(frame, 1f, null, exits, null, arrivals);
        Assert.Empty(arrivals);
        Assert.Empty(exits);
        Assert.True(restored.TryGetRuntimeState(id, out var resumed));
        Assert.Equal(state.Position, resumed.Position);
        Assert.Equal(after, restored.ExportRollback(default));
        restored.ImportRollback(default, before);
        restored.Tick(2, 1f, null, exits, null, arrivals);
        Assert.Single(arrivals);
        Assert.Equal(after, restored.ExportRollback(default));
        Assert.True(restored.Despawn(id, 20, ProjectileExitReason.Manual, out _));
        Assert.Equal(0, restored.ActiveCount);
    }

    [Fact]
    public void Default_distance_response_still_exits_and_invalid_response_rejects_spawn()
    {
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        world.Spawn(Spawn(4, 3, ProjectileMaxDistanceResponse.Exit));
        var exits = new List<ProjectileExitEvent>();
        var arrivals = new List<ProjectileArrivalEvent>();
        world.Tick(1, 1f, null, exits, null, arrivals);
        Assert.Equal(ProjectileExitReason.MaxDistance, Assert.Single(exits).Reason);
        Assert.Empty(arrivals);
        Assert.Equal(0, world.ActiveCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Spawn(Spawn(5, 3, (ProjectileMaxDistanceResponse)99)));
        Assert.Equal(0, world.ActiveCount);
    }

    [Fact]
    public void Previous_rollback_version_rejects_before_mutating_the_live_projectile_world()
    {
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        world.Spawn(Spawn(7, 3, ProjectileMaxDistanceResponse.SuspendAtDestination));
        var before = world.ExportRollback(default);
        var state = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(before);
        var previous = MemoryPackSerializer.Serialize(new ProjectileWorldSnapshotPayload(
            16, state.Frame, state.NextId, state.Items, state.NextHitFilterKey));
        Assert.Throws<InvalidOperationException>(() => world.ImportRollback(default, previous));
        Assert.Equal(before, world.ExportRollback(default));
    }

    private static ProjectileSpawnParams Spawn(int template, float distance, ProjectileMaxDistanceResponse response) => new(
        1, template, 1, 1, 0, Vec3.Zero, Vec3.Right, 10, 0, 0, 0, 10, distance,
        0, ColliderId.Invalid, hitPolicyKind: ProjectileHitPolicyKind.Pierce,
        hitPolicyParam: -1, hitsRemaining: 0, maxDistanceResponse: response);
}
