using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Rooms;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Rooms;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;

namespace AbilityKit.Orleans.Grains.Gameplay;

internal enum ServerBattleSyncMode
{
    StateSync = 0,
    FrameSync = 1
}

internal enum ServerBattleRuntimeMode
{
    BattleWorld = 0,
    FrameRelayOnly = 1,
    BattleWorldWithFrameSync = 2
}

internal sealed class ServerBattleSyncTemplate
{
    public ServerBattleSyncTemplate(string templateId, ServerBattleSyncMode mode)
        : this(templateId, mode, ServerBattleRuntimeMode.BattleWorld, 1, 30)
    {
    }

    public ServerBattleSyncTemplate(string templateId, ServerBattleSyncMode mode, ServerBattleRuntimeMode runtimeMode)
        : this(templateId, mode, runtimeMode, 1, 30)
    {
    }

    public ServerBattleSyncTemplate(
        string templateId,
        ServerBattleSyncMode mode,
        ServerBattleRuntimeMode runtimeMode,
        int snapshotIntervalFrames,
        int fullSnapshotIntervalFrames)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Sync template id is required.", nameof(templateId));
        }

        TemplateId = templateId;
        Mode = mode;
        RuntimeMode = runtimeMode;
        SnapshotIntervalFrames = snapshotIntervalFrames > 0 ? snapshotIntervalFrames : 1;
        FullSnapshotIntervalFrames = fullSnapshotIntervalFrames > 0 ? fullSnapshotIntervalFrames : 30;
    }

    public string TemplateId { get; }

    public ServerBattleSyncMode Mode { get; }

    public ServerBattleRuntimeMode RuntimeMode { get; }

    public int SnapshotIntervalFrames { get; }

    public int FullSnapshotIntervalFrames { get; }

    public bool SupportsStateSyncPush => Mode == ServerBattleSyncMode.StateSync;

    public bool SupportsFrameSync => Mode == ServerBattleSyncMode.FrameSync;

    public bool RequiresBattleRuntime =>
        RuntimeMode == ServerBattleRuntimeMode.BattleWorld ||
        RuntimeMode == ServerBattleRuntimeMode.BattleWorldWithFrameSync;
}

internal sealed class ServerBattleSyncProfile
{
    public static ServerBattleSyncProfile StateSync(string defaultTemplateId, params string[] supportedTemplateIds)
    {
        return new ServerBattleSyncProfile(
            new ServerBattleSyncTemplate(defaultTemplateId, ServerBattleSyncMode.StateSync),
            CreateTemplates(ServerBattleSyncMode.StateSync, supportedTemplateIds));
    }

    public static ServerBattleSyncProfile FrameSync(string defaultTemplateId, params string[] stateSyncTemplateIds)
    {
        return new ServerBattleSyncProfile(
            new ServerBattleSyncTemplate(defaultTemplateId, ServerBattleSyncMode.FrameSync, ServerBattleRuntimeMode.FrameRelayOnly),
            CreateTemplates(ServerBattleSyncMode.StateSync, stateSyncTemplateIds));
    }

    public static ServerBattleSyncProfile FrameSync(string defaultTemplateId, string[] stateSyncTemplateIds, ServerBattleRuntimeMode runtimeMode)
    {
        return new ServerBattleSyncProfile(
            new ServerBattleSyncTemplate(defaultTemplateId, ServerBattleSyncMode.FrameSync, runtimeMode),
            CreateTemplates(ServerBattleSyncMode.StateSync, stateSyncTemplateIds));
    }

    public static ServerBattleSyncProfile FromTemplates(
        ServerBattleSyncTemplate defaultTemplate,
        params ServerBattleSyncTemplate[] additionalTemplates)
    {
        return new ServerBattleSyncProfile(defaultTemplate, additionalTemplates);
    }

    private ServerBattleSyncProfile(ServerBattleSyncTemplate defaultTemplate, IReadOnlyList<ServerBattleSyncTemplate> additionalTemplates)
    {
        DefaultTemplate = defaultTemplate ?? throw new ArgumentNullException(nameof(defaultTemplate));
        Templates = NormalizeTemplates(defaultTemplate, additionalTemplates ?? Array.Empty<ServerBattleSyncTemplate>());
        SupportedTemplateIds = GetTemplateIds(Templates);
    }

    public ServerBattleSyncTemplate DefaultTemplate { get; }

    public ServerBattleSyncMode DefaultMode => DefaultTemplate.Mode;

    public string DefaultTemplateId => DefaultTemplate.TemplateId;

    public bool SupportsStateSyncPush => DefaultTemplate.SupportsStateSyncPush;

    public bool SupportsFrameSync => DefaultTemplate.SupportsFrameSync;

    public IReadOnlyList<ServerBattleSyncTemplate> Templates { get; }

    public IReadOnlyList<string> SupportedTemplateIds { get; }

    public bool SupportsTemplate(string? templateId)
    {
        return TryResolveTemplate(templateId, out _);
    }

    public string ResolveTemplateId(string? requestedTemplateId)
    {
        return ResolveTemplate(requestedTemplateId).TemplateId;
    }

    public ServerBattleSyncTemplate ResolveTemplate(string? requestedTemplateId)
    {
        if (TryResolveTemplate(requestedTemplateId, out var template))
        {
            return template;
        }

        throw new InvalidOperationException($"Unsupported sync template. TemplateId={requestedTemplateId}");
    }

    public bool TryResolveTemplate(string? requestedTemplateId, out ServerBattleSyncTemplate template)
    {
        if (string.IsNullOrWhiteSpace(requestedTemplateId))
        {
            template = DefaultTemplate;
            return true;
        }

        for (var i = 0; i < Templates.Count; i++)
        {
            if (string.Equals(Templates[i].TemplateId, requestedTemplateId, StringComparison.OrdinalIgnoreCase))
            {
                template = Templates[i];
                return true;
            }
        }

        template = DefaultTemplate;
        return false;
    }

    private static IReadOnlyList<ServerBattleSyncTemplate> CreateTemplates(ServerBattleSyncMode mode, IReadOnlyList<string> templateIds)
    {
        var templates = new List<ServerBattleSyncTemplate>();
        for (var i = 0; i < templateIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(templateIds[i]))
            {
                templates.Add(new ServerBattleSyncTemplate(templateIds[i], mode));
            }
        }

        return templates;
    }

    private static IReadOnlyList<ServerBattleSyncTemplate> NormalizeTemplates(ServerBattleSyncTemplate defaultTemplate, IReadOnlyList<ServerBattleSyncTemplate> additionalTemplates)
    {
        var templates = new List<ServerBattleSyncTemplate> { defaultTemplate };
        for (var i = 0; i < additionalTemplates.Count; i++)
        {
            var template = additionalTemplates[i];
            if (!ContainsTemplate(templates, template.TemplateId))
            {
                templates.Add(template);
            }
        }

        return templates;
    }

    private static IReadOnlyList<string> GetTemplateIds(IReadOnlyList<ServerBattleSyncTemplate> templates)
    {
        var templateIds = new string[templates.Count];
        for (var i = 0; i < templates.Count; i++)
        {
            templateIds[i] = templates[i].TemplateId;
        }

        return templateIds;
    }

    private static bool ContainsTemplate(IReadOnlyList<ServerBattleSyncTemplate> templates, string templateId)
    {
        for (var i = 0; i < templates.Count; i++)
        {
            if (string.Equals(templates[i].TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class ServerGameplayModule
{
    private readonly Func<IRoomGameplayAdapter> _roomAdapterFactory;
    private readonly Func<ServerBattleWorldManager, IBattleRuntimeAdapter> _battleRuntimeAdapterFactory;
    private readonly IReadOnlyList<Func<IWorldBlueprint>> _worldBlueprintFactories;

    public ServerGameplayModule(
        GameplayRoomDescriptor descriptor,
        ServerBattleSyncProfile syncProfile,
        Func<IRoomGameplayAdapter> roomAdapterFactory,
        Func<ServerBattleWorldManager, IBattleRuntimeAdapter> battleRuntimeAdapterFactory,
        IReadOnlyList<Func<IWorldBlueprint>> worldBlueprintFactories)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        SyncProfile = syncProfile ?? throw new ArgumentNullException(nameof(syncProfile));
        _roomAdapterFactory = roomAdapterFactory ?? throw new ArgumentNullException(nameof(roomAdapterFactory));
        _battleRuntimeAdapterFactory = battleRuntimeAdapterFactory ?? throw new ArgumentNullException(nameof(battleRuntimeAdapterFactory));
        _worldBlueprintFactories = worldBlueprintFactories ?? throw new ArgumentNullException(nameof(worldBlueprintFactories));
        if (_worldBlueprintFactories.Count == 0)
        {
            throw new ArgumentException("At least one world blueprint must be registered for a server gameplay module.", nameof(worldBlueprintFactories));
        }
    }

    public GameplayRoomDescriptor Descriptor { get; }

    public ServerBattleSyncProfile SyncProfile { get; }

    public string RoomType => Descriptor.RoomType;

    public IRoomGameplayAdapter CreateRoomAdapter()
    {
        var adapter = _roomAdapterFactory();
        if (!string.Equals(adapter.RoomType, RoomType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Room gameplay adapter type mismatch. Descriptor={RoomType}, Adapter={adapter.RoomType}");
        }

        return adapter;
    }

    public IBattleRuntimeAdapter CreateBattleRuntimeAdapter(ServerBattleWorldManager worldManager)
    {
        var adapter = _battleRuntimeAdapterFactory(worldManager ?? throw new ArgumentNullException(nameof(worldManager)));
        if (!string.Equals(adapter.RoomType, RoomType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Battle runtime adapter type mismatch. Descriptor={RoomType}, Adapter={adapter.RoomType}");
        }

        return adapter;
    }

    public IReadOnlyList<IWorldBlueprint> CreateWorldBlueprints()
    {
        var blueprints = new IWorldBlueprint[_worldBlueprintFactories.Count];
        var hasDefaultWorldType = false;
        for (var i = 0; i < _worldBlueprintFactories.Count; i++)
        {
            var blueprint = _worldBlueprintFactories[i]();
            if (blueprint is null)
            {
                throw new InvalidOperationException($"World blueprint factory returned null. RoomType={RoomType}");
            }

            if (string.Equals(blueprint.WorldType, Descriptor.DefaultWorldType, StringComparison.OrdinalIgnoreCase))
            {
                hasDefaultWorldType = true;
            }

            blueprints[i] = blueprint;
        }

        if (!hasDefaultWorldType)
        {
            throw new InvalidOperationException($"Default world blueprint is not registered. RoomType={RoomType}, WorldType={Descriptor.DefaultWorldType}");
        }

        return blueprints;
    }
}

internal sealed class ServerGameplayModuleCatalog
{
    private readonly IReadOnlyList<ServerGameplayModule> _modules;

    public static ServerGameplayModuleCatalog Default { get; } = new(new[]
    {
        new ServerGameplayModule(
            ServerGameplayDescriptors.Moba,
            ServerBattleSyncProfile.FrameSync("frame-sync-authority", ["state-sync-authority"], ServerBattleRuntimeMode.BattleWorldWithFrameSync),
            static () => new MobaRoomGameplayAdapter(),
            static worldManager => new MobaBattleRuntimeAdapter(worldManager, DefaultOrleansBattleProtocolMapper.Instance),
            new Func<IWorldBlueprint>[]
            {
                static () => new MobaLobbyWorldBlueprint(),
                static () => new MobaBattleWorldBlueprint()
            }),
        new ServerGameplayModule(
            ServerGameplayDescriptors.Shooter,
            ShooterServerSyncTemplateCatalog.CreateSyncProfile(),
            static () => new ShooterRoomGameplayAdapter(),
            static worldManager => new ShooterBattleRuntimeAdapter(worldManager),
            new Func<IWorldBlueprint>[]
            {
                static () => new ShooterBattleWorldBlueprint()
            })
    });

    public ServerGameplayModuleCatalog(IReadOnlyList<ServerGameplayModule> modules)
    {
        if (modules is null)
        {
            throw new ArgumentNullException(nameof(modules));
        }

        if (modules.Count == 0)
        {
            throw new ArgumentException("At least one server gameplay module must be registered.", nameof(modules));
        }

        _modules = modules;
        GameplayCatalog = new ServerGameplayCatalog(GetDescriptors(modules));
    }

    public ServerGameplayCatalog GameplayCatalog { get; }

    public IReadOnlyList<ServerGameplayModule> Modules => _modules;

    public ServerGameplayModule ResolveModule(string? roomType)
    {
        if (!string.IsNullOrWhiteSpace(roomType))
        {
            for (var i = 0; i < _modules.Count; i++)
            {
                if (string.Equals(_modules[i].RoomType, roomType, StringComparison.OrdinalIgnoreCase))
                {
                    return _modules[i];
                }
            }
        }

        var defaultRoomType = GameplayCatalog.DefaultDescriptor.RoomType;
        for (var i = 0; i < _modules.Count; i++)
        {
            if (string.Equals(_modules[i].RoomType, defaultRoomType, StringComparison.OrdinalIgnoreCase))
            {
                return _modules[i];
            }
        }

        throw new InvalidOperationException($"Default server gameplay module is not registered. RoomType={defaultRoomType}");
    }

    public ServerBattleSyncProfile ResolveSyncProfile(string? roomType)
    {
        return ResolveModule(roomType).SyncProfile;
    }

    public IReadOnlyList<IRoomGameplayAdapter> CreateRoomAdapters()
    {
        var adapters = new IRoomGameplayAdapter[_modules.Count];
        for (var i = 0; i < _modules.Count; i++)
        {
            adapters[i] = _modules[i].CreateRoomAdapter();
        }

        return adapters;
    }

    public IReadOnlyList<IBattleRuntimeAdapter> CreateBattleRuntimeAdapters(ServerBattleWorldManager worldManager)
    {
        var adapters = new IBattleRuntimeAdapter[_modules.Count];
        for (var i = 0; i < _modules.Count; i++)
        {
            adapters[i] = _modules[i].CreateBattleRuntimeAdapter(worldManager);
        }

        return adapters;
    }

    public IReadOnlyList<IWorldBlueprint> CreateWorldBlueprints()
    {
        var blueprints = new List<IWorldBlueprint>();
        for (var i = 0; i < _modules.Count; i++)
        {
            blueprints.AddRange(_modules[i].CreateWorldBlueprints());
        }

        return blueprints;
    }

    public IReadOnlyList<string> GetWorldTypes()
    {
        var worldTypes = new List<string>();
        foreach (var descriptor in GameplayCatalog.Descriptors)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.DefaultWorldType))
            {
                worldTypes.Add(descriptor.DefaultWorldType);
            }
        }

        foreach (var blueprint in CreateWorldBlueprints())
        {
            if (!ContainsWorldType(worldTypes, blueprint.WorldType))
            {
                worldTypes.Add(blueprint.WorldType);
            }
        }

        return worldTypes;
    }

    private static bool ContainsWorldType(IReadOnlyList<string> worldTypes, string worldType)
    {
        for (var i = 0; i < worldTypes.Count; i++)
        {
            if (string.Equals(worldTypes[i], worldType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<GameplayRoomDescriptor> GetDescriptors(IReadOnlyList<ServerGameplayModule> modules)
    {
        var descriptors = new GameplayRoomDescriptor[modules.Count];
        for (var i = 0; i < modules.Count; i++)
        {
            descriptors[i] = modules[i].Descriptor;
        }

        return descriptors;
    }
}
