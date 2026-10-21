using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Config;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems.Collision;
using Xunit;
using System.IO;

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
        var loader = new ConsoleTextAssetLoader();
        var configs = new MobaConfigDatabase(textAssetLoader: loader);
        configs.LoadFromGroups(MobaLubanConfigGroups.Create(loader, Array.Empty<string>()));
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
    public void Published_minion_route_matches_map_bounds_walkable_area_and_team_spawns()
    {
        var loader = new ConsoleTextAssetLoader();
        var configs = new MobaConfigDatabase(textAssetLoader: loader);
        configs.LoadFromGroups(MobaLubanConfigGroups.Create(loader, Array.Empty<string>()));
        Assert.True(configs.TryGetBattleMap(1, out var map));

        var root = FindRepositoryRoot();
        var routeSourcePath = Path.Combine(root.FullName,
            "LubanConfig", "Moba", "Production", "Gameplay", "minion_routes.json");
        var runtimeRoutePath = Path.Combine(root.FullName,
            "Unity", "Packages", "com.abilitykit.demo.moba.runtime", "Resources", "moba", "minion_routes.json");
        var consoleRoutePath = Path.Combine(root.FullName,
            "src", "AbilityKit.Demo.Moba.Console", "Configs", "moba", "minion_routes.json");
        var sourceJson = File.ReadAllText(routeSourcePath);
        Assert.Equal(sourceJson, File.ReadAllText(runtimeRoutePath));
        Assert.Equal(sourceJson, File.ReadAllText(consoleRoutePath));
        var resource = MobaMinionRouteResourceLoader.LoadResource(sourceJson);
        Assert.Equal(map.Id, resource.MapId);
        var route = Assert.Single(resource.Routes);

        Assert.Equal(1, route.RouteId);
        MobaMinionRouteMapValidator.Validate(route, map);

        var maps = new MobaMapRuntimeService(configs, new CollisionService());
        maps.Load(map.Id);
        Assert.True(maps.TryGetTeamSpawnPoint(1, 0, out var teamOneSpawn));
        Assert.True(maps.TryGetTeamSpawnPoint(2, 0, out var teamTwoSpawn));
        Assert.Equal(teamOneSpawn.Position, route.Waypoints[0]);
        Assert.Equal(teamTwoSpawn.Position, route.Waypoints[route.Waypoints.Count - 1]);
        foreach (var waypoint in route.Waypoints)
            Assert.True(maps.IsPositionWalkable(waypoint), $"Route waypoint {waypoint} must be walkable.");
    }

    [Fact]
    public void Minion_route_runtime_config_is_prepared_against_the_loaded_map()
    {
        var loader = new ConsoleTextAssetLoader();
        var configs = new MobaConfigDatabase(textAssetLoader: loader);
        configs.LoadFromGroups(MobaLubanConfigGroups.Create(loader, Array.Empty<string>()));
        Assert.True(configs.TryGetBattleMap(1, out var map));
        var routeConfig = new MobaMinionRouteRuntimeConfig(loader);

        routeConfig.Prepare(map);

        Assert.True(routeConfig.IsPrepared);
        Assert.Equal(map.Id, routeConfig.MapId);
        Assert.Equal(1, routeConfig.Resource.DefaultRouteId);
        Assert.Equal(routeConfig.Resource.DefaultRouteId, routeConfig.DefaultRoute.RouteId);
        Assert.Equal(30_000, routeConfig.Resource.Schedule.IntervalMs);
        Assert.Equal(2, routeConfig.Resource.Schedule.Composition.Count);
    }

    [Fact]
    public void Minion_route_runtime_config_rejects_a_map_mismatch_during_prepare()
    {
        var loader = new ConsoleTextAssetLoader();
        var mapDto = CreateMap();
        mapDto.Id = 2;
        var map = new BattleMapMO(mapDto);
        var routeConfig = new MobaMinionRouteRuntimeConfig(loader);

        var error = Assert.Throws<InvalidOperationException>(() => routeConfig.Prepare(map));

        Assert.Contains("mapId=2", error.Message);
        Assert.Contains("resourceMapId=1", error.Message);
        Assert.Contains("path=moba/minion_routes.json", error.Message);
        Assert.False(routeConfig.IsPrepared);
    }

    [Fact]
    public void Minion_route_runtime_config_prepare_fails_when_resource_is_removed()
    {
        var root = FindRepositoryRoot();
        var routePath = Path.Combine(root.FullName,
            "LubanConfig", "Moba", "Production", "Gameplay", "minion_routes.json");
        var loader = new MutableTextAssetLoader(File.ReadAllText(routePath));
        var routeConfig = new MobaMinionRouteRuntimeConfig(loader);
        var configs = new MobaConfigDatabase(textAssetLoader: new ConsoleTextAssetLoader());
        configs.LoadFromGroups(MobaLubanConfigGroups.Create(new ConsoleTextAssetLoader(), Array.Empty<string>()));
        Assert.True(configs.TryGetBattleMap(1, out var map));

        routeConfig.Prepare(map);
        Assert.True(routeConfig.IsPrepared);

        loader.Text = null;
        var error = Assert.Throws<InvalidOperationException>(() => routeConfig.Prepare(map));
        Assert.Contains("resource was not found", error.Message);
        Assert.False(routeConfig.IsPrepared);
        Assert.Equal(0, routeConfig.MapId);
        Assert.Null(routeConfig.Resource);
        Assert.Null(routeConfig.DefaultRoute);
    }

    [Fact]
    public void Minion_route_runtime_config_prepare_rejects_invalid_published_resource()
    {
        var loader = new MutableTextAssetLoader("{ invalid json");
        var routeConfig = new MobaMinionRouteRuntimeConfig(loader);

        var error = Assert.Throws<InvalidOperationException>(
            () => routeConfig.Prepare(new BattleMapMO(CreateMap())));

        Assert.Contains("resource is invalid", error.Message);
        Assert.False(routeConfig.IsPrepared);
    }

    [Fact]
    public void Minion_route_map_validator_rejects_waypoints_outside_walkable_areas()
    {
        var map = new BattleMapMO(CreateMap());
        var route = new MobaMinionRouteDefinition(
            1,
            new[] { new Vec3(-8f, 0f, 1f), new Vec3(0f, 0f, 1f), new Vec3(8f, 0f, -1f) },
            0.5f,
            MobaMinionTerminalPolicy.BaseDamageEvent);

        var error = Assert.Throws<InvalidOperationException>(
            () => MobaMinionRouteMapValidator.Validate(route, map));

        Assert.Contains("outside walkable areas", error.Message);
    }

    [Fact]
    public void Minion_route_resource_validator_rejects_map_identity_mismatch()
    {
        const string json = "{\"Version\":2,\"MapId\":2,\"Routes\":[{\"RouteId\":1,\"AcceptanceRadius\":0.5,\"TerminalPolicy\":\"BaseDamageEvent\",\"Waypoints\":[{\"X\":-8,\"Y\":0,\"Z\":1},{\"X\":8,\"Y\":0,\"Z\":-1}]}]}";
        var resource = MobaMinionRouteResourceLoader.LoadResource(json);
        var map = new BattleMapMO(CreateMap());

        var error = Assert.Throws<InvalidOperationException>(
            () => MobaMinionRouteMapValidator.ValidateResource(resource, map));

        Assert.Contains("resourceMapId=2", error.Message);
        Assert.Contains("mapId=1", error.Message);
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

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Unity", "Packages")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
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

    private sealed class MutableTextAssetLoader : ITextAssetLoader
    {
        public MutableTextAssetLoader(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public bool TryLoadText(string path, out string text)
        {
            text = Text;
            return !string.IsNullOrWhiteSpace(Text);
        }

        public bool TryLoadBytes(string path, out byte[] bytes)
        {
            bytes = null;
            return false;
        }
    }
}
