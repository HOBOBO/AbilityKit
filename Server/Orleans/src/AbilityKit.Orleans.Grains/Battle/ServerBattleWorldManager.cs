using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Resources;
using Microsoft.Extensions.Logging;

namespace AbilityKit.Orleans.Grains.Battle;

/// <summary>
/// Orleans battle host 使用的服务器侧玩法世界管理器。
/// 它同时注册 MOBA 与 Shooter 世界蓝图，并让房间到世界的生命周期状态保持玩法无关。
/// </summary>
public sealed class ServerBattleWorldManager : IDisposable
{
    private readonly ILogger _logger;
    private readonly WorldTypeRegistry _worldRegistry;
    private readonly RegistryWorldFactory _worldFactory;
    private readonly WorldManager _worldManager;
    private readonly Dictionary<string, IWorld> _worlds = new();
    private readonly object _lock = new();

    public ServerBattleWorldManager(ILogger logger)
    {
        _logger = logger;
        var baseFactory = new SimpleWorldFactory();
        _worldRegistry = new WorldTypeRegistry();

        var gameplayModules = ServerGameplayModuleCatalog.Default;
        var blueprintRegistry = new WorldBlueprintRegistry();
        foreach (var blueprint in gameplayModules.CreateWorldBlueprints())
        {
            blueprintRegistry.Register(blueprint);
        }

        RegisterWorldTypes(_worldRegistry, baseFactory.Create, blueprintRegistry, gameplayModules.GetWorldTypes(), _logger);

        _worldFactory = new RegistryWorldFactory(_worldRegistry);
        _worldManager = new WorldManager(_worldFactory);

        _logger.LogInformation("[ServerBattleWorldManager] Initialized");
    }

    public IWorld CreateBattleWorld(string roomId, int tickRate)
    {
        return CreateBattleWorld(roomId, tickRate, configureOptions: null);
    }

    public IWorld CreateBattleWorld(
        string roomId,
        int tickRate,
        Action<WorldCreateOptions>? configureOptions)
    {
        lock (_lock)
        {
            if (_worlds.TryGetValue(roomId, out var existingWorld))
            {
                _logger.LogWarning("[ServerBattleWorldManager] World already exists for room: {RoomId}", roomId);
                return existingWorld;
            }

            return CreateBattleWorldCore(roomId, GetDefaultWorldType(), configureOptions);
        }
    }

    public IWorld CreateBattleWorld(string roomId, string worldType, int tickRate)
    {
        return CreateBattleWorld(roomId, worldType, tickRate, configureOptions: null);
    }

    public IWorld CreateBattleWorld(string roomId, string worldType, int tickRate, Action<WorldCreateOptions>? configureOptions)
    {
        lock (_lock)
        {
            return CreateBattleWorldCore(roomId, string.IsNullOrWhiteSpace(worldType) ? GetDefaultWorldType() : worldType, configureOptions);
        }
    }

    private static string GetDefaultWorldType()
    {
        var defaultWorldType = ServerGameplayCatalog.Default.DefaultDescriptor.DefaultWorldType;
        if (string.IsNullOrWhiteSpace(defaultWorldType))
        {
            throw new InvalidOperationException("Default server gameplay world type is not configured.");
        }

        return defaultWorldType;
    }

    private IWorld CreateBattleWorldCore(string roomId, string worldType, Action<WorldCreateOptions>? configureOptions = null)
    {
        if (_worlds.TryGetValue(roomId, out var existingWorld))
        {
            _logger.LogWarning("[ServerBattleWorldManager] World already exists for room: {RoomId}", roomId);
            return existingWorld;
        }

        var options = new WorldCreateOptions
        {
            WorldType = worldType,
            Id = new WorldId(roomId)
        };
        ConfigureMobaResourceLoader(options);
        configureOptions?.Invoke(options);

        _logger.LogInformation(
            "[ServerBattleWorldManager] Creating battle world for room: {RoomId}, WorldType: {WorldType}",
            roomId,
            worldType);
        var createStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var world = _worldManager.Create(options);
        var createElapsed = System.Diagnostics.Stopwatch.GetElapsedTime(createStartedAt);
        _logger.LogInformation(
            "[ServerBattleWorldManager] Battle world factory completed for room: {RoomId}, ElapsedMs: {ElapsedMs}",
            roomId,
            createElapsed.TotalMilliseconds);

        _worlds[roomId] = world;
        _logger.LogInformation(
            "[ServerBattleWorldManager] Created battle world for room: {RoomId}, WorldType: {WorldType}, WorldId: {WorldId}",
            roomId,
            world.WorldType,
            world.Id);

        return world;
    }

    private static void ConfigureMobaResourceLoader(WorldCreateOptions options)
    {
        if (!string.Equals(options.WorldType, MobaBattleWorldBlueprint.Type, StringComparison.Ordinal))
        {
            return;
        }

        options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
        var loader = new ServerMobaTextAssetLoader();
        options.ServiceBuilder.TryRegister<ITextAssetLoader>(
            WorldLifetime.Singleton,
            _ => loader);
        options.ServiceBuilder.TryRegister<ITextAssetDirectoryLoader>(
            WorldLifetime.Singleton,
            _ => loader);
    }

    private static void RegisterWorldTypes(
        WorldTypeRegistry registry,
        Func<WorldCreateOptions, IWorld> baseFactory,
        WorldBlueprintRegistry blueprintRegistry,
        IReadOnlyList<string> worldTypes,
        ILogger logger)
    {
        if (worldTypes.Count == 0)
        {
            throw new InvalidOperationException("At least one server battle world type must be registered.");
        }

        for (var i = 0; i < worldTypes.Count; i++)
        {
            var worldType = worldTypes[i];
            if (string.IsNullOrWhiteSpace(worldType))
            {
                continue;
            }

            registry.Register(worldType, options => CreateWorldFromBlueprint(baseFactory, blueprintRegistry, options, logger));
        }
    }

    private static IWorld CreateWorldFromBlueprint(
        Func<WorldCreateOptions, IWorld> baseFactory,
        WorldBlueprintRegistry blueprintRegistry,
        WorldCreateOptions options,
        ILogger logger)
    {
        logger.LogInformation(
            "[ServerBattleWorldManager] Configuring world blueprint for WorldId: {WorldId}, WorldType: {WorldType}",
            options.Id,
            options.WorldType);
        blueprintRegistry.Configure(options);
        logger.LogInformation(
            "[ServerBattleWorldManager] World blueprint configured for WorldId: {WorldId}, Modules: {ModuleCount}",
            options.Id,
            options.Modules.Count);
        logger.LogInformation(
            "[ServerBattleWorldManager] Invoking base world factory for WorldId: {WorldId}",
            options.Id);
        var world = baseFactory(options);
        logger.LogInformation(
            "[ServerBattleWorldManager] Base world factory completed for WorldId: {WorldId}",
            options.Id);
        return world;
    }

    public bool TryGetBattleWorld(string roomId, out IWorld? world)
    {
        lock (_lock)
        {
            return _worlds.TryGetValue(roomId, out world);
        }
    }

    public IWorldStateSnapshotProvider? GetSnapshotProvider(string roomId)
    {
        lock (_lock)
        {
            if (!_worlds.TryGetValue(roomId, out var world))
            {
                return null;
            }

            return world.Services.Resolve<IWorldStateSnapshotProvider>();
        }
    }

    public void TickWorld(string roomId, float deltaTime)
    {
        lock (_lock)
        {
            if (_worlds.TryGetValue(roomId, out var world))
            {
                world.Tick(deltaTime);
            }
        }
    }

    public bool DestroyBattleWorld(string roomId)
    {
        lock (_lock)
        {
            if (_worlds.ContainsKey(roomId))
            {
                _worlds.Remove(roomId);
                _worldManager.Destroy(new WorldId(roomId));
                _logger.LogInformation("[ServerBattleWorldManager] Destroyed battle world for room: {RoomId}", roomId);
                return true;
            }

            return false;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _worlds.Clear();
            _worldManager.DisposeAll();
        }

        _logger.LogInformation("[ServerBattleWorldManager] Disposed all worlds");
    }
}
