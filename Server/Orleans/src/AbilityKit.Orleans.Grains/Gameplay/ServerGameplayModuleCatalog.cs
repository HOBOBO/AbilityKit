using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
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

public enum ServerBattleSyncMode
{
    StateSync = 0,
    FrameSync = 1
}

public readonly record struct ServerSyncCapabilityDefinition(
    string ProfileName,
    NetworkSyncProfile Profile,
    int MinimumSchemaVersion,
    int MaximumSchemaVersion);

public enum ServerBattleRuntimeMode
{
    BattleWorld = 0,
    FrameRelayOnly = 1,
    BattleWorldWithFrameSync = 2
}

public sealed class ServerBattleSyncTemplate
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

    public bool SupportsStateSyncPush =>
        Mode == ServerBattleSyncMode.StateSync ||
        RuntimeMode == ServerBattleRuntimeMode.BattleWorldWithFrameSync;

    public bool SupportsFrameSync => Mode == ServerBattleSyncMode.FrameSync;

    public bool RequiresBattleRuntime =>
        RuntimeMode == ServerBattleRuntimeMode.BattleWorld ||
        RuntimeMode == ServerBattleRuntimeMode.BattleWorldWithFrameSync;
}

public sealed class ServerBattleSyncProfile
{
    /// <summary>
    /// 唯一的构造入口。玩法不应直接拼 profile——同步模板集合与能力协商由
    /// <see cref="ServerSyncCapabilityDeclaration"/> 一次声明，避免两者漂移。
    /// </summary>
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

/// <summary>
/// 玩法同步的<b>单一声明来源</b>：一次声明同时产出模板集合（<see cref="ServerBattleSyncProfile"/>）
/// 与能力解析器，因此「声明了哪些模板」和「每个模板协商出什么能力」在结构上不可能漂移。
/// 这是 MOBA、Shooter 以及装配外玩法共用的一套声明入口。
/// </summary>
public sealed class ServerSyncCapabilityDeclaration
{
    private readonly Func<BattleSyncStartOptions?, string, ServerSyncCapabilityDefinition> _resolve;

    private ServerSyncCapabilityDeclaration(
        ServerBattleSyncProfile syncProfile,
        Func<BattleSyncStartOptions?, string, ServerSyncCapabilityDefinition> resolve)
    {
        SyncProfile = syncProfile ?? throw new ArgumentNullException(nameof(syncProfile));
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public ServerBattleSyncProfile SyncProfile { get; }

    public string DefaultTemplateId => SyncProfile.DefaultTemplateId;

    /// <summary>静态场景：模板集合与每个模板的能力一次写完。</summary>
    public static ServerSyncCapabilityDeclaration FromTemplates(
        string defaultTemplateId,
        params ServerSyncTemplateDeclaration[] templates)
    {
        if (string.IsNullOrWhiteSpace(defaultTemplateId))
        {
            throw new ArgumentException("Default sync template id is required.", nameof(defaultTemplateId));
        }

        if (templates is null || templates.Length == 0)
        {
            throw new ArgumentException("At least one sync template must be declared.", nameof(templates));
        }

        var defaultIndex = -1;
        var byTemplateId = new Dictionary<string, ServerSyncTemplateDeclaration>(StringComparer.OrdinalIgnoreCase);
        var declared = new ServerBattleSyncTemplate[templates.Length];
        for (var i = 0; i < templates.Length; i++)
        {
            var declaration = templates[i];
            if (string.IsNullOrWhiteSpace(declaration.TemplateId))
            {
                throw new ArgumentException("Sync template id is required.", nameof(templates));
            }

            if (!byTemplateId.TryAdd(declaration.TemplateId, declaration))
            {
                throw new ArgumentException($"Duplicate sync template id '{declaration.TemplateId}'.", nameof(templates));
            }

            declared[i] = declaration.ToTemplate();
            if (string.Equals(declaration.TemplateId, defaultTemplateId, StringComparison.OrdinalIgnoreCase))
            {
                defaultIndex = i;
            }
        }

        if (defaultIndex < 0)
        {
            throw new ArgumentException($"Default sync template '{defaultTemplateId}' is not declared.", nameof(defaultTemplateId));
        }

        var additional = new ServerBattleSyncTemplate[templates.Length - 1];
        var cursor = 0;
        for (var i = 0; i < templates.Length; i++)
        {
            if (i != defaultIndex)
            {
                additional[cursor++] = declared[i];
            }
        }

        var profile = ServerBattleSyncProfile.FromTemplates(declared[defaultIndex], additional);
        return new ServerSyncCapabilityDeclaration(profile, (_, templateId) =>
        {
            var resolved = profile.ResolveTemplate(templateId);
            var declaration = byTemplateId[resolved.TemplateId];
            return new ServerSyncCapabilityDefinition(
                NetworkSyncProfileRegistry.GetName(declaration.Profile.CompatibilityModel),
                declaration.Profile,
                declaration.MinimumSchemaVersion,
                declaration.MaximumSchemaVersion);
        });
    }

    /// <summary>动态场景：模板集合已给定，能力解析需要看启动选项（例如按请求的 SyncModel 覆盖档案）。</summary>
    public static ServerSyncCapabilityDeclaration FromResolver(
        ServerBattleSyncProfile syncProfile,
        Func<BattleSyncStartOptions?, string, ServerSyncCapabilityDefinition> resolve) =>
        new(syncProfile, resolve);

    public ServerSyncCapabilityDefinition Resolve(BattleSyncStartOptions? options, string templateId) =>
        _resolve(options, templateId);
}

/// <summary>单个同步模板的声明：模板形状 + 协商档案 + schema 版本区间。</summary>
public readonly record struct ServerSyncTemplateDeclaration(
    string TemplateId,
    ServerBattleSyncMode Mode,
    ServerBattleRuntimeMode RuntimeMode,
    int SnapshotIntervalFrames,
    int FullSnapshotIntervalFrames,
    NetworkSyncProfile Profile,
    int MinimumSchemaVersion,
    int MaximumSchemaVersion)
{
    public ServerBattleSyncTemplate ToTemplate() =>
        new(TemplateId, Mode, RuntimeMode, SnapshotIntervalFrames, FullSnapshotIntervalFrames);
}

public sealed class ServerGameplayModule
{
    private readonly Func<IRoomGameplayAdapter> _roomAdapterFactory;
    private readonly Func<ServerBattleWorldManager, IBattleRuntimeAdapter> _battleRuntimeAdapterFactory;
    private readonly IReadOnlyList<Func<IWorldBlueprint>> _worldBlueprintFactories;

    public ServerGameplayModule(
        GameplayRoomDescriptor descriptor,
        ServerSyncCapabilityDeclaration syncDeclaration,
        Func<IRoomGameplayAdapter> roomAdapterFactory,
        Func<ServerBattleWorldManager, IBattleRuntimeAdapter> battleRuntimeAdapterFactory,
        IReadOnlyList<Func<IWorldBlueprint>> worldBlueprintFactories)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        SyncDeclaration = syncDeclaration ?? throw new ArgumentNullException(nameof(syncDeclaration));
        _roomAdapterFactory = roomAdapterFactory ?? throw new ArgumentNullException(nameof(roomAdapterFactory));
        _battleRuntimeAdapterFactory = battleRuntimeAdapterFactory ?? throw new ArgumentNullException(nameof(battleRuntimeAdapterFactory));
        _worldBlueprintFactories = worldBlueprintFactories ?? throw new ArgumentNullException(nameof(worldBlueprintFactories));
        if (_worldBlueprintFactories.Count == 0)
        {
            throw new ArgumentException("At least one world blueprint must be registered for a server gameplay module.", nameof(worldBlueprintFactories));
        }
    }

    public GameplayRoomDescriptor Descriptor { get; }

    public ServerSyncCapabilityDeclaration SyncDeclaration { get; }

    public ServerBattleSyncProfile SyncProfile => SyncDeclaration.SyncProfile;

    public string RoomType => Descriptor.RoomType;

    public ServerSyncCapabilityDefinition ResolveSyncCapabilities(BattleSyncStartOptions? options, string templateId)
    {
        if (!SyncProfile.SupportsTemplate(templateId))
        {
            throw new InvalidOperationException($"Unsupported sync template. RoomType={RoomType}, TemplateId={templateId}");
        }

        return SyncDeclaration.Resolve(options, templateId);
    }

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

public sealed class ServerGameplayModuleCatalog
{
    private readonly IReadOnlyList<ServerGameplayModule> _modules;

    // 模板 id 直接取自 MOBA 的 room descriptor，避免"默认模板"在两处各写一份字面量。
    private static readonly ServerSyncCapabilityDeclaration MobaSyncDeclaration =
        ServerSyncCapabilityDeclaration.FromTemplates(
            ServerGameplayDescriptors.Moba.DefaultSyncTemplateId,
            new ServerSyncTemplateDeclaration(
                ServerGameplayDescriptors.Moba.DefaultSyncTemplateId,
                ServerBattleSyncMode.FrameSync,
                ServerBattleRuntimeMode.BattleWorldWithFrameSync,
                1,
                30,
                NetworkSyncProfiles.Lockstep,
                0,
                1));

    public static ServerGameplayModuleCatalog Default { get; } = new(new[]
    {
        new ServerGameplayModule(
            ServerGameplayDescriptors.Moba,
            MobaSyncDeclaration,
            static () => new MobaRoomGameplayAdapter(),
            static worldManager => new MobaBattleRuntimeAdapter(worldManager, DefaultOrleansBattleProtocolMapper.Instance),
            new Func<IWorldBlueprint>[]
            {
                static () => new MobaLobbyWorldBlueprint(),
                static () => new MobaBattleWorldBlueprint()
            }),
        new ServerGameplayModule(
            ServerGameplayDescriptors.Shooter,
            ServerSyncCapabilityDeclaration.FromResolver(
                ShooterServerSyncTemplateCatalog.CreateSyncProfile(),
                ServerGameplaySyncCapabilityProfiles.ForShooter),
            static () => new ShooterRoomGameplayAdapter(),
            static worldManager => new ShooterBattleRuntimeAdapter(worldManager),
            new Func<IWorldBlueprint>[]
            {
                static () => new ShooterBattleWorldBlueprint()
            })
    });

    public ServerGameplayModuleCatalog WithModule(ServerGameplayModule module)
    {
        if (module is null) throw new ArgumentNullException(nameof(module));
        var modules = new List<ServerGameplayModule>(_modules) { module };
        return new ServerGameplayModuleCatalog(modules);
    }

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

    public ServerSyncCapabilityDefinition ResolveSyncCapabilities(
        string? roomType,
        BattleSyncStartOptions? options,
        string templateId)
    {
        return ResolveModule(roomType).ResolveSyncCapabilities(options, templateId);
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
