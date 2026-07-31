using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems.Collision;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Map;

public sealed class MobaMapRuntimeServiceTests
{
    [Fact]
    public void Load_registers_map_queries_and_world_colliders()
    {
        var collisionService = new CollisionService();
        var maps = new MobaMapRuntimeService(CreateConfigDatabase(CreateMap()), collisionService);

        maps.Load(1);

        Assert.True(maps.IsLoaded);
        Assert.Equal(1, maps.CurrentMap.Id);
        Assert.Equal("Runtime Test Arena", maps.CurrentMap.Name);

        Assert.True(maps.TryGetSpawnPointById(101, out var spawnPoint));
        Assert.Equal(1, spawnPoint.TeamId);
        Assert.Equal(new Vec3(-8f, 0f, 1f), spawnPoint.Position);
        Assert.True(maps.TryGetTeamSpawnPoint(2, 0, out var teamSpawnPoint));
        Assert.Equal(201, teamSpawnPoint.Id);
        Assert.False(maps.TryGetTeamSpawnPoint(2, 1, out _));

        foreach (var objectId in new[] { 1001, 1002, 1003 })
        {
            Assert.True(maps.TryGetColliderId(objectId, out var colliderId));
            Assert.True(maps.TryGetMapObject(colliderId, out var mapObject));
            Assert.Equal(objectId, mapObject.Id);
            Assert.True(collisionService.World.GetLayer(colliderId, out var layerId));
            Assert.Equal(MobaCollisionLayers.WorldId, layerId);
        }
    }

    [Fact]
    public void Load_same_map_is_idempotent_and_unload_removes_owned_state()
    {
        var collisionService = new CollisionService();
        var maps = new MobaMapRuntimeService(CreateConfigDatabase(CreateMap()), collisionService);

        maps.Load(1);
        Assert.True(maps.TryGetColliderId(1001, out var originalColliderId));

        maps.Load(1);

        Assert.True(maps.TryGetColliderId(1001, out var colliderAfterReload));
        Assert.Equal(originalColliderId, colliderAfterReload);

        maps.Unload();

        Assert.False(maps.IsLoaded);
        Assert.Null(maps.CurrentMap);
        Assert.False(maps.TryGetColliderId(1001, out _));
        Assert.False(maps.TryGetMapObject(originalColliderId, out _));
        Assert.False(maps.TryGetSpawnPointById(101, out _));
        Assert.False(collisionService.World.GetLayer(originalColliderId, out _));

        maps.Unload();
    }

    [Fact]
    public void Console_config_replica_loads_battle_map_into_runtime_model()
    {
        var configs = new MobaConfigDatabase(
            textAssetLoader: new ConsoleTextAssetLoader());

        var result = configs.ReloadFromResources("moba", strict: true);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(configs.TryGetBattleMap(1, out var map));
        Assert.Equal("Prototype Arena", map.Name);
        Assert.Single(map.WalkableAreas);
        Assert.Equal("Main Arena", map.WalkableAreas[0].Name);
        Assert.Equal(new Vec3(35f, 0f, 23f), map.WalkableAreas[0].Size);
        Assert.Equal(2, map.SpawnPoints.Count);
        Assert.Equal(6, map.CollisionObjects.Count);
        Assert.All(
            map.CollisionObjects,
            mapObject => Assert.Equal(MobaCollisionLayers.WorldId, mapObject.CollisionLayer));
        Assert.Equal(
            MapProjectileResponse.Block,
            map.CollisionObjects.Single(mapObject => mapObject.Id == 1101).ProjectileResponse);
        Assert.Equal(
            MapProjectileResponse.Ignore,
            map.CollisionObjects.Single(mapObject => mapObject.Id == 1102).ProjectileResponse);

        var maps = new MobaMapRuntimeService(configs, new CollisionService());
        maps.Load(1);

        Assert.True(maps.IsPositionWalkable(new Vec3(17f, 0f, 11f), 0.5f));
        Assert.False(maps.IsPositionWalkable(new Vec3(17.01f, 0f, 11f), 0.5f));
        Assert.True(maps.TryProjectToWalkable(
            new Vec3(30f, 2f, 20f),
            0.5f,
            out var projected));
        Assert.Equal(new Vec3(17f, 2f, 11f), projected);
    }

    [Fact]
    public void Walkable_queries_apply_actor_clearance_and_choose_nearest_area()
    {
        var maps = new MobaMapRuntimeService(
            CreateConfigDatabase(CreateMap()),
            new CollisionService());
        maps.Load(1);

        Assert.True(maps.IsPositionWalkable(new Vec3(-8f, 0f, 0f), 0.5f));
        Assert.False(maps.IsPositionWalkable(new Vec3(-3.75f, 0f, 0f), 0.5f));
        Assert.False(maps.IsPositionWalkable(new Vec3(0f, 0f, 0f), 0.5f));
        Assert.False(maps.IsPositionWalkable(new Vec3(-8f, 0f, 0f), -0.1f));

        Assert.True(maps.TryProjectToWalkable(new Vec3(1f, 3f, 0f), 0.5f, out var projected));
        Assert.Equal(new Vec3(4.5f, 3f, 0f), projected);
        Assert.True(maps.IsPositionWalkable(projected, 0.5f));
    }

    [Fact]
    public void Load_rejects_spawn_point_outside_walkable_areas()
    {
        var map = CreateMap();
        map.SpawnPoints[0].Position = Vector(0f, 0f, 0f);
        var maps = new MobaMapRuntimeService(
            CreateConfigDatabase(map),
            new CollisionService());

        var error = Assert.Throws<InvalidOperationException>(() => maps.Load(1));

        Assert.Contains("spawn point must be inside a walkable area", error.Message);
        Assert.False(maps.IsLoaded);
    }

    [Fact]
    public void Load_rejects_duplicate_walkable_area_ids()
    {
        var map = CreateMap();
        map.WalkableAreas[1].Id = map.WalkableAreas[0].Id;

        var error = AssertInvalidMap(map);

        Assert.Contains("duplicate walkable area id", error.Message);
    }

    [Fact]
    public void Load_rejects_non_positive_walkable_area_size()
    {
        var map = CreateMap();
        map.WalkableAreas[0].Size.X = 0f;

        var error = AssertInvalidMap(map);

        Assert.Contains("walkable area must have positive XZ size", error.Message);
    }

    [Fact]
    public void Load_rejects_walkable_area_outside_map_bounds()
    {
        var map = CreateMap();
        map.WalkableAreas[0].Center.X = -11f;

        var error = AssertInvalidMap(map);

        Assert.Contains("walkable area must be contained by map bounds", error.Message);
    }

    [Fact]
    public void Collision_sync_only_removes_actor_colliders_it_owns()
    {
        var collisionService = new CollisionService();
        var maps = new MobaMapRuntimeService(CreateConfigDatabase(CreateMap()), collisionService);
        maps.Load(1);
        Assert.True(maps.TryGetColliderId(1001, out var mapColliderId));

        using var container = new WorldContainerBuilder()
            .RegisterInstance<ICollisionService>(collisionService)
            .Build();
        var contexts = new global::Contexts();
        var actor = contexts.actor.CreateEntity();
        actor.AddTransform(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One));
        actor.AddCollider(ColliderShape.CreateSphere(Vec3.Zero, 0.5f));
        actor.AddCollisionLayer(MobaCollisionLayers.UnitMask);
        var sync = new CollisionWorldSyncSystem(contexts, container);

        sync.Execute();

        Assert.True(actor.hasCollisionId);
        var actorColliderId = actor.collisionId.Value;
        Assert.True(collisionService.World.GetLayer(actorColliderId, out var actorLayer));
        Assert.Equal(MobaCollisionLayers.UnitId, actorLayer);
        Assert.True(collisionService.World.GetLayer(mapColliderId, out var mapLayer));
        Assert.Equal(MobaCollisionLayers.WorldId, mapLayer);

        actor.RemoveCollider();
        sync.Execute();

        Assert.False(actor.hasCollisionId);
        Assert.False(collisionService.World.GetLayer(actorColliderId, out _));
        Assert.True(collisionService.World.GetLayer(mapColliderId, out mapLayer));
        Assert.Equal(MobaCollisionLayers.WorldId, mapLayer);
    }

    private static InvalidOperationException AssertInvalidMap(BattleMapDTO map)
    {
        var collisionService = new CollisionService();
        var maps = new MobaMapRuntimeService(
            CreateConfigDatabase(map),
            collisionService);

        var error = Assert.Throws<InvalidOperationException>(() => maps.Load(map.Id));
        Assert.False(maps.IsLoaded);
        return error;
    }

    private static MobaConfigDatabase CreateConfigDatabase(BattleMapDTO map)
    {
        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(BattleMapDTO)] = new[] { map },
            },
            strict: false);

        Assert.True(result.Succeeded, result.Error);
        return configs;
    }

    private static BattleMapDTO CreateMap()
    {
        return new BattleMapDTO
        {
            Id = 1,
            Name = "Runtime Test Arena",
            Bounds = new MapBoundsDTO
            {
                Center = Vector(0f, 0f, 0f),
                Size = Vector(24f, 4f, 16f),
            },
            WalkableAreas = new[]
            {
                new MapWalkableAreaDTO
                {
                    Id = 11,
                    Name = "West Lane",
                    Center = Vector(-8f, 0f, 0f),
                    Size = Vector(8f, 0f, 14f),
                },
                new MapWalkableAreaDTO
                {
                    Id = 12,
                    Name = "East Lane",
                    Center = Vector(8f, 0f, 0f),
                    Size = Vector(8f, 0f, 14f),
                },
            },
            SpawnPoints = new[]
            {
                new MapSpawnPointDTO
                {
                    Id = 101,
                    TeamId = 1,
                    Position = Vector(-8f, 0f, 1f),
                    YawDegrees = 90f,
                },
                new MapSpawnPointDTO
                {
                    Id = 201,
                    TeamId = 2,
                    Position = Vector(8f, 0f, -1f),
                    YawDegrees = -90f,
                },
            },
            CollisionObjects = new[]
            {
                CollisionObject(1001, "Box", Vector(0f, 1f, 5f), Vector(10f, 2f, 1f)),
                CollisionObject(1002, "Sphere", Vector(-3f, 1f, 0f), Vector(0f, 0f, 0f), radius: 1f),
                CollisionObject(1003, "Capsule", Vector(3f, 1.5f, 0f), Vector(0f, 0f, 0f), radius: 0.5f, height: 3f),
            },
        };
    }

    private static MapCollisionObjectDTO CollisionObject(
        int id,
        string shapeType,
        MapVector3DTO position,
        MapVector3DTO size,
        float radius = 0f,
        float height = 0f)
    {
        return new MapCollisionObjectDTO
        {
            Id = id,
            Name = $"Object {id}",
            ObjectType = "StaticObstacle",
            ShapeType = shapeType,
            Position = position,
            RotationEuler = Vector(0f, id == 1001 ? 15f : 0f, 0f),
            Size = size,
            Radius = radius,
            Height = height,
            CollisionLayer = MobaCollisionLayers.WorldId,
            BlocksMovement = true,
            ProjectileResponse = "Default",
            GenerateView = true,
        };
    }

    private static MapVector3DTO Vector(float x, float y, float z)
    {
        return new MapVector3DTO { X = x, Y = y, Z = z };
    }
}
