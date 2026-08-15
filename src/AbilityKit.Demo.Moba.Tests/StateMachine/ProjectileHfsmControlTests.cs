using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using MemoryPack;
using AbilityKit.Demo.Moba.Services.StateMachine;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class ProjectileHfsmControlTests
{
    [Fact]
    public void YingZhengProjectileProfile_LoadsThroughTheSharedHfsmProtocol()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Configs", "moba", "actor_state_machines.json");
        var catalog = new MobaActorStateMachineProfileCatalog();

        Assert.Equal(1, MobaActorStateMachineProfileJsonLoader.LoadJson(File.ReadAllText(path), catalog));
        Assert.True(catalog.TryGet("ying_zheng_ultimate_projectile", out var profile));
        Assert.Equal("prepare", profile.StartState);
        Assert.Equal(3, profile.States.Count);
        Assert.Equal(2, profile.Transitions.Count);
    }

    [Fact]
    public void SuspendedProjectile_IsControlledExternallyAndRollbackKeepsSuspension()
    {
        var world = new ProjectileWorld(new NoHitCollisionWorld());
        var position = Vec3.Zero;
        var direction = Vec3.Right;
        var spawn = new ProjectileSpawnParams(
            ownerId: 1,
            templateId: 30060301,
            launcherActorId: 2,
            rootActorId: 1,
            spawnFrame: 100,
            in position,
            in direction,
            speed: 10f,
            returnAfterFrames: 0,
            returnSpeed: 0f,
            returnStopDistance: 0f,
            lifetimeFrames: 2,
            maxDistance: 20f,
            collisionLayerMask: 0,
            ignoreCollider: default,
            tickIntervalFrames: 1,
            startSuspended: true);
        var id = world.Spawn(in spawn);

        world.Tick(100, 0.1f, null, null, null);
        var suspended = Snapshot(world, 100);
        Assert.Equal(1, suspended.IsSuspended);
        Assert.Equal(0L, suspended.PositionX);
        Assert.Equal(2, suspended.LifetimeFramesLeft);

        var controlledPosition = new Vec3(-2f, 0f, 0f);
        Assert.True(world.TrySetPosition(id, in controlledPosition));
        var rollback = world.ExportRollback(new FrameIndex(100));

        Assert.True(world.ResumeSimulation(id));
        world.Tick(101, 0.1f, null, null, null);
        var resumed = Snapshot(world, 101);
        Assert.Equal(0, resumed.IsSuspended);
        Assert.Equal(-1f, Fixed64.FromRaw(resumed.PositionX).ToSingle(), 4);
        Assert.Equal(1, resumed.LifetimeFramesLeft);

        world.ImportRollback(new FrameIndex(100), rollback);
        world.Tick(101, 0.1f, null, null, null);
        var restored = Snapshot(world, 101);
        Assert.Equal(1, restored.IsSuspended);
        Assert.Equal(-2f, Fixed64.FromRaw(restored.PositionX).ToSingle(), 4);
        Assert.Equal(2, restored.LifetimeFramesLeft);

        world.Clear();
    }

    private static ProjectileWorldSnapshotItem Snapshot(ProjectileWorld world, int frame)
    {
        var payload = world.ExportRollback(new FrameIndex(frame));
        var snapshot = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(payload);
        Assert.Single(snapshot.Items);
        return snapshot.Items[0];
    }

    private sealed class NoHitCollisionWorld : ICollisionWorld
    {
        public ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerId) => new(1);
        public bool Remove(ColliderId id) => true;
        public bool UpdateTransform(ColliderId id, in Transform3 transform) => true;
        public bool UpdateShape(ColliderId id, in ColliderShape localShape) => true;
        public bool UpdateLayer(ColliderId id, int layerId) => true;
        public bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape) => true;
        public bool ShouldCollide(int layerA, int layerB) => false;
        public bool GetLayer(ColliderId id, out int layerId) { layerId = 0; return false; }
        public bool Raycast(in Ray3 ray, float maxDistance, in LayerFilter filter, out RaycastHit hit) { hit = default; return false; }
        public int OverlapSphere(in Sphere sphere, in LayerFilter filter, List<ColliderId> results) => 0;
    }
}
