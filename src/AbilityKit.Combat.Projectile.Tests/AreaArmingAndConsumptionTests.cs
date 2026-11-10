using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.FrameSync;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class AreaArmingAndConsumptionTests
{
    [Fact]
    public void Cancel_removes_area_without_emitting_expire_event()
    {
        var collision = new MutableCollisionWorld();
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(7, in center, 2, 10, -1, 0);
        var areaId = world.Spawn(in parameters, 1, spawnEvents);

        Assert.True(world.Cancel(areaId));
        Assert.False(world.Cancel(areaId));
        Assert.Equal(0, world.ActiveCount);
        Assert.Empty(expires);
    }

    [Fact]
    public void DelayedArea_DoesNotEnterBeforeArm_ThenEntersForTargetAlreadyInside()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0, armDelayFrames: 2);
        var areaId = world.Spawn(in parameters, 0, spawnEvents);

        world.Tick(0, enters, stays, exits, expires);
        world.Tick(1, enters, stays, exits, expires);
        Assert.Empty(enters);

        world.Tick(2, enters, stays, exits, expires);
        Assert.Single(enters);
        Assert.Equal(new ColliderId(7), enters[0].Collider);
    }

    [Fact]
    public void OneShotArea_ConsumesOnlyFirstEnter_AndRemainsActiveUntilExplicitDespawn()
    {
        var collision = new MutableCollisionWorld(new ColliderId(9), new ColliderId(3));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0, consumeOnFirstEnter: true);
        var areaId = world.Spawn(in parameters, 0, spawnEvents);

        world.Tick(0, enters, stays, exits, expires);
        Assert.Equal(2, enters.Count);
        var acceptedEffects = 0;
        foreach (var _ in enters)
        {
            if (world.TryConsumeEnter(areaId)) acceptedEffects++;
        }
        Assert.Equal(1, acceptedEffects);
        enters.Clear();
        world.Tick(1, enters, stays, exits, expires);
        Assert.Empty(enters);
        Assert.Equal(1, world.ActiveCount);
    }

    [Fact]
    public void GroupClaim_RejectsRepeatedTargetWithoutConsumingNextArea()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var firstArea = world.Spawn(in parameters, 0, spawnEvents);
        var secondArea = world.Spawn(in parameters, 0, spawnEvents);

        world.Tick(0, enters, stays, exits, expires);
        Assert.Equal(2, enters.Count);
        Assert.True(world.TryAcceptEnter(firstArea, 7));
        Assert.False(world.TryAcceptEnter(secondArea, 7));

        enters.Clear();
        collision.SetOverlaps(new ColliderId(8));
        world.Tick(1, enters, stays, exits, expires);
        Assert.Single(enters);
        Assert.Equal(new ColliderId(8), enters[0].Collider);
        Assert.True(world.TryAcceptEnter(secondArea, 8));
        Assert.Equal(2, world.ActiveCount);
    }

    [Fact]
    public void GroupClaim_AllowsOneAreaToAffectMultipleDistinctTargets()
    {
        var world = new AreaWorld(new MutableCollisionWorld());
        var spawnEvents = new List<AreaSpawnEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var area = world.Spawn(in parameters, 0, spawnEvents);

        Assert.True(world.TryAcceptEnter(area, 7));
        Assert.True(world.TryAcceptEnter(area, 8));
        Assert.False(world.TryAcceptEnter(area, 7));
        Assert.Equal(1, world.ActiveCount);
    }

    [Fact]
    public void GroupClaim_SameFrameCollisionUsesStableAreaOrderAndKeepsOtherAreaAvailable()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var lowerAreaId = world.Spawn(in parameters, 0, spawnEvents);
        var higherAreaId = world.Spawn(in parameters, 0, spawnEvents);

        world.Tick(0, enters, stays, exits, expires);
        enters.Sort((left, right) =>
        {
            var areaOrder = left.Area.Value.CompareTo(right.Area.Value);
            return areaOrder != 0 ? areaOrder : left.Collider.Value.CompareTo(right.Collider.Value);
        });

        Assert.Equal(2, enters.Count);
        Assert.Equal(lowerAreaId, enters[0].Area);
        Assert.Equal(higherAreaId, enters[1].Area);
        Assert.True(world.TryAcceptEnter(enters[0].Area, 7));
        Assert.False(world.TryAcceptEnter(enters[1].Area, 7));
        Assert.True(world.TryAcceptEnter(higherAreaId, 8));
    }

    [Fact]
    public void GroupClaim_RequiresValidRootAndPositiveKey_AndRejectsInvalidTargets()
    {
        var collision = new MutableCollisionWorld();
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var center = Vec3.Zero;
        var missingRoot = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, groupKey: 1, claimTargetsWithinGroup: true);
        var missingKey = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, claimTargetsWithinGroup: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Spawn(in missingRoot, 0, spawnEvents));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Spawn(in missingKey, 0, spawnEvents));

        var valid = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var area = world.Spawn(in valid, 0, spawnEvents);
        Assert.False(world.TryAcceptEnter(area, 0));
        Assert.False(world.TryAcceptEnter(area, -1));
        Assert.True(world.TryAcceptEnter(area, 7));
    }

    [Fact]
    public void GroupClaims_AreIsolatedByKeyAndRestoredByRollback()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var groupOne = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var groupTwo = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, groupKey: 2, claimTargetsWithinGroup: true);
        var firstArea = world.Spawn(in groupOne, 0, spawnEvents);
        var secondArea = world.Spawn(in groupOne, 0, spawnEvents);
        var independentArea = world.Spawn(in groupTwo, 0, spawnEvents);
        world.Tick(0, enters, stays, exits, expires);
        Assert.True(world.TryAcceptEnter(firstArea, 7));

        var snapshot = world.ExportRollback(0);
        var restored = new AreaWorld(collision);
        restored.ImportRollback(snapshot);
        Assert.False(restored.TryAcceptEnter(secondArea, 7));
        Assert.True(restored.TryAcceptEnter(independentArea, 7));
    }

    [Fact]
    public void GroupClaims_AreClearedAfterLastAreaIsRemoved()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0,
            consumeOnFirstEnter: true, rootContextId: 55, groupKey: 1, claimTargetsWithinGroup: true);
        var firstArea = world.Spawn(in parameters, 0, spawnEvents);
        var secondArea = world.Spawn(in parameters, 0, spawnEvents);

        Assert.True(world.TryAcceptEnter(firstArea, 7));
        Assert.True(world.Despawn(firstArea, 1, expires));
        Assert.False(world.TryAcceptEnter(secondArea, 7));
        Assert.True(world.Despawn(secondArea, 1, expires));

        var nextCastArea = world.Spawn(in parameters, 2, spawnEvents);
        Assert.True(world.TryAcceptEnter(nextCastArea, 7));
    }

    [Fact]
    public void DefaultArea_AllowsReenterAfterExit()
    {
        var collision = new MutableCollisionWorld(new ColliderId(4));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0);
        world.Spawn(in parameters, 0, spawnEvents);
        world.Tick(0, enters, stays, exits, expires);
        Assert.Single(enters);

        collision.SetOverlaps();
        world.Tick(1, enters, stays, exits, expires);
        collision.SetOverlaps(new ColliderId(4));
        world.Tick(2, enters, stays, exits, expires);
        Assert.Equal(2, enters.Count);
    }

    [Fact]
    public void Snapshot_RestoresDelayedArmConsumptionOverlapAndIdAllocator()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var world = new AreaWorld(collision);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0, armDelayFrames: 2, consumeOnFirstEnter: true);
        var id = world.Spawn(in parameters, 0, spawnEvents);
        world.Tick(0, enters, stays, exits, expires);
        var snapshot = world.ExportRollback(0);

        var restored = new AreaWorld(collision);
        restored.ImportRollback(snapshot);
        enters.Clear();
        restored.Tick(1, enters, stays, exits, expires);
        Assert.Empty(enters);
        restored.Tick(2, enters, stays, exits, expires);
        Assert.Single(enters);
        Assert.Equal(id, enters[0].Area);
        Assert.True(restored.TryConsumeEnter(id));
        enters.Clear();
        restored.Tick(3, enters, stays, exits, expires);
        Assert.Empty(enters);

        var nextId = restored.Spawn(in parameters, 4, spawnEvents);
        Assert.Equal(id.Value + 1, nextId.Value);
    }

    [Fact]
    public void ProjectileRollbackProvider_RestoresAreaStateThroughServicePayload()
    {
        var collision = new MutableCollisionWorld(new ColliderId(7));
        var service = new ProjectileService(new TestCollisionService(collision));
        var provider = new ProjectileRollbackProvider(service);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0, armDelayFrames: 2, consumeOnFirstEnter: true);
        var areaId = service.SpawnArea(in parameters, 0);
        var frame = new FrameIndex(0);
        var payload = provider.Export(frame);

        service.Tick(1, 1f / 60f);
        service.DrainAreaEnterEvents(enters);
        Assert.Empty(enters);
        provider.Import(frame, payload);

        service.Tick(1, 1f / 60f);
        service.DrainAreaEnterEvents(enters);
        Assert.Empty(enters);
        service.Tick(2, 1f / 60f);
        service.DrainAreaEnterEvents(enters);
        Assert.Single(enters);
        Assert.Equal(areaId, enters[0].Area);
        Assert.True(service.TryConsumeAreaEnter(areaId));

        var nextAreaId = service.SpawnArea(in parameters, 3);
        Assert.Equal(areaId.Value + 1, nextAreaId.Value);
    }

    [Fact]
    public void FollowingArea_TracksActorOffsetAndRestoresAnchorThroughRollback()
    {
        var collision = new MutableCollisionWorld();
        var world = new AreaWorld(collision);
        var positions = new MutablePositionProvider { Position = new Vec3(10, 0, 0) };
        world.SetTrackingTargetProvider(positions);
        var spawnEvents = new List<AreaSpawnEvent>();
        var enters = new List<AreaEnterEvent>();
        var stays = new List<AreaStayEvent>();
        var exits = new List<AreaExitEvent>();
        var expires = new List<AreaExpireEvent>();
        var center = new Vec3(12, 1, 0);
        var parameters = new AreaSpawnParams(1, in center, 3, 10, -1, 0, followOwner: true);
        var id = world.Spawn(in parameters, 0, spawnEvents);

        positions.Position = new Vec3(20, 0, 0);
        world.Tick(1, enters, stays, exits, expires);
        Assert.Equal(new Vec3(22, 1, 0), collision.LastQueriedCenter);

        var snapshot = world.ExportRollback(1);
        var restored = new AreaWorld(collision);
        restored.SetTrackingTargetProvider(positions);
        restored.ImportRollback(snapshot);
        positions.Position = new Vec3(30, 0, 0);
        restored.Tick(2, enters, stays, exits, expires);
        Assert.Equal(new Vec3(32, 1, 0), collision.LastQueriedCenter);
        Assert.Empty(expires);

        positions.Available = false;
        restored.Tick(3, enters, stays, exits, expires);
        Assert.Equal(0, restored.ActiveCount);
        Assert.Single(expires);
        Assert.Equal(id, expires[0].Area);
    }

    private sealed class MutableCollisionWorld : ICollisionWorld
    {
        private readonly List<ColliderId> _overlaps = new();
        public MutableCollisionWorld(params ColliderId[] overlaps) => SetOverlaps(overlaps);
        public Vec3 LastQueriedCenter { get; private set; }
        public void SetOverlaps(params ColliderId[] overlaps)
        {
            _overlaps.Clear();
            _overlaps.AddRange(overlaps);
        }
        public ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerId) => throw new NotSupportedException();
        public bool Remove(ColliderId id) => throw new NotSupportedException();
        public bool UpdateTransform(ColliderId id, in Transform3 transform) => throw new NotSupportedException();
        public bool UpdateShape(ColliderId id, in ColliderShape localShape) => throw new NotSupportedException();
        public bool UpdateLayer(ColliderId id, int layerId) => throw new NotSupportedException();
        public bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape) => throw new NotSupportedException();
        public bool Raycast(in Ray3 ray, float maxDistance, in LayerFilter filter, out RaycastHit hit) { hit = default; return false; }
        public int OverlapSphere(in Sphere sphere, in LayerFilter filter, List<ColliderId> results)
        {
            LastQueriedCenter = sphere.Center;
            results.AddRange(_overlaps);
            return _overlaps.Count;
        }
        public bool ShouldCollide(int layerA, int layerB) => true;
        public bool GetLayer(ColliderId id, out int layerId) { layerId = 0; return true; }
    }

    private sealed class MutablePositionProvider : IProjectileTrackingTargetProvider
    {
        public Vec3 Position;
        public bool Available = true;
        public bool TryGetTrackingTargetPosition(int targetActorId, out Vec3 position)
        {
            position = Position;
            return Available && targetActorId > 0;
        }
        public void Dispose() { }
    }

    private sealed class TestCollisionService : ICollisionService
    {
        public TestCollisionService(ICollisionWorld world) => World = world;
        public ICollisionWorld World { get; }
        public void Dispose() { }
    }
}
