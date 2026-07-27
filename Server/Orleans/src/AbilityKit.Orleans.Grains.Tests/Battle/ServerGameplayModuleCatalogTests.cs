using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Orleans.Grains.Rooms;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Rooms;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Rooms;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ServerGameplayModuleCatalogTests
{
    [Fact]
    public void DefaultCatalog_WhenCreatingAdapters_RegistersRoomAndBattleModulesAsPairs()
    {
        var moduleCatalog = ServerGameplayModuleCatalog.Default;
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);

        var descriptors = moduleCatalog.GameplayCatalog.Descriptors.ToDictionary(d => d.RoomType);
        var roomAdapters = moduleCatalog.CreateRoomAdapters().ToDictionary(a => a.RoomType);
        var battleAdapters = moduleCatalog.CreateBattleRuntimeAdapters(worldManager).ToDictionary(a => a.RoomType);

        Assert.Contains(GameplayRoomTypes.Moba, descriptors.Keys);
        Assert.Contains(ShooterGameplay.RoomType, descriptors.Keys);
        Assert.Equal(descriptors.Keys.OrderBy(k => k), roomAdapters.Keys.OrderBy(k => k));
        Assert.Equal(descriptors.Keys.OrderBy(k => k), battleAdapters.Keys.OrderBy(k => k));
        Assert.IsType<MobaRoomGameplayAdapter>(roomAdapters[GameplayRoomTypes.Moba]);
        Assert.IsType<ShooterRoomGameplayAdapter>(roomAdapters[ShooterGameplay.RoomType]);
        Assert.IsType<MobaBattleRuntimeAdapter>(battleAdapters[GameplayRoomTypes.Moba]);
        Assert.IsType<ShooterBattleRuntimeAdapter>(battleAdapters[ShooterGameplay.RoomType]);
        Assert.Equal(GameplayRoomTypes.Moba, moduleCatalog.GameplayCatalog.DefaultDescriptor.RoomType);
    }

    [Fact]
    public void DefaultCatalog_WhenResolvingSyncProfiles_RegistersGameplaySyncModes()
    {
        var moduleCatalog = ServerGameplayModuleCatalog.Default;

        var mobaProfile = moduleCatalog.ResolveSyncProfile(GameplayRoomTypes.Moba);
        var shooterProfile = moduleCatalog.ResolveSyncProfile(ShooterGameplay.RoomType);

        Assert.Equal(ServerBattleSyncMode.FrameSync, mobaProfile.DefaultMode);
        Assert.Equal("frame-sync-authority", mobaProfile.DefaultTemplateId);
        Assert.True(mobaProfile.SupportsFrameSync);
        Assert.False(mobaProfile.SupportsStateSyncPush);
        Assert.True(mobaProfile.SupportsTemplate("state-sync-authority"));
        Assert.Equal(ServerBattleSyncMode.FrameSync, mobaProfile.ResolveTemplate(null).Mode);
        Assert.Equal(ServerBattleRuntimeMode.FrameRelayOnly, mobaProfile.ResolveTemplate(null).RuntimeMode);
        Assert.False(mobaProfile.ResolveTemplate(null).RequiresBattleRuntime);
        Assert.Equal(ServerBattleSyncMode.FrameSync, mobaProfile.ResolveTemplate("frame-sync-authority").Mode);
        Assert.Equal(ServerBattleRuntimeMode.FrameRelayOnly, mobaProfile.ResolveTemplate("frame-sync-authority").RuntimeMode);
        Assert.Equal(ServerBattleSyncMode.StateSync, mobaProfile.ResolveTemplate("state-sync-authority").Mode);
        Assert.True(mobaProfile.ResolveTemplate("state-sync-authority").RequiresBattleRuntime);
        Assert.Equal("frame-sync-authority", moduleCatalog.GameplayCatalog.Resolve(GameplayRoomTypes.Moba).DefaultSyncTemplateId);
        Assert.Equal(ServerBattleSyncMode.StateSync, shooterProfile.DefaultMode);
        Assert.Equal(ShooterServerProtocol.PredictRollbackAuthorityTemplate, shooterProfile.DefaultTemplateId);
        Assert.Equal(ShooterServerProtocol.PredictRollbackAuthorityTemplate, moduleCatalog.GameplayCatalog.Resolve(ShooterGameplay.RoomType).DefaultSyncTemplateId);
        Assert.True(shooterProfile.SupportsStateSyncPush);
        Assert.False(shooterProfile.SupportsFrameSync);
        Assert.True(shooterProfile.SupportsTemplate(ShooterServerProtocol.AuthoritativeInterpolationPresentationTemplate));
        Assert.True(shooterProfile.SupportsTemplate(ShooterServerProtocol.BatchStateLowFrequencyTemplate));
        Assert.True(shooterProfile.SupportsTemplate(ShooterServerProtocol.MassBattleLodAoiTemplate));
        Assert.True(shooterProfile.SupportsTemplate(ShooterServerProtocol.HybridHeroPredictionTemplate));
        Assert.True(shooterProfile.SupportsTemplate(ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate));
        Assert.Equal(ServerBattleSyncMode.StateSync, shooterProfile.ResolveTemplate(ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate).Mode);
        Assert.Equal(ServerBattleSyncMode.StateSync, shooterProfile.ResolveTemplate(ShooterServerProtocol.HybridHeroPredictionTemplate).Mode);
    }

    [Fact]
    public void DefaultCatalog_WhenCreatingWorldBlueprints_RegistersGameplayWorldTypes()
    {
        var moduleCatalog = ServerGameplayModuleCatalog.Default;

        var blueprints = moduleCatalog.CreateWorldBlueprints().ToDictionary(b => b.WorldType);
        var worldTypes = moduleCatalog.GetWorldTypes();

        Assert.Contains(MobaBattleWorldBlueprint.Type, blueprints.Keys);
        Assert.Contains(MobaLobbyWorldBlueprint.Type, blueprints.Keys);
        Assert.Contains(ShooterGameplay.WorldType, blueprints.Keys);
        Assert.Contains(MobaBattleWorldBlueprint.Type, worldTypes);
        Assert.Contains(MobaLobbyWorldBlueprint.Type, worldTypes);
        Assert.Contains(ShooterGameplay.WorldType, worldTypes);
        Assert.IsType<MobaBattleWorldBlueprint>(blueprints[MobaBattleWorldBlueprint.Type]);
        Assert.IsType<MobaLobbyWorldBlueprint>(blueprints[MobaLobbyWorldBlueprint.Type]);
        Assert.IsType<ShooterBattleWorldBlueprint>(blueprints[ShooterGameplay.WorldType]);
    }

    [Fact]
    public void RoomFrameSyncRoute_WhenUsingTemplateModes_OnlyStartsFrameSyncTemplates()
    {
        var mobaSummary = CreateSummary(GameplayRoomTypes.Moba);
        var shooterSummary = CreateSummary(ShooterGameplay.RoomType);

        var mobaFrameRoute = RoomFrameSyncRoute.Resolve(mobaSummary, "battle-1", CreateInitParams(syncTemplateId: null));
        var mobaStateRoute = RoomFrameSyncRoute.Resolve(mobaSummary, "battle-1", CreateInitParams("state-sync-authority"));
        var shooterRoute = RoomFrameSyncRoute.Resolve(shooterSummary, "battle-2", CreateInitParams(syncTemplateId: null));
        var mobaFrameStartRoute = RoomFrameSyncRoute.ResolveStartRoute(mobaSummary, "battle-1", CreateInitParams(syncTemplateId: null));
        var mobaStateStartRoute = RoomFrameSyncRoute.ResolveStartRoute(mobaSummary, "battle-1", CreateInitParams("state-sync-authority"));
        var shooterStartRoute = RoomFrameSyncRoute.ResolveStartRoute(shooterSummary, "battle-2", CreateInitParams(syncTemplateId: null));

        Assert.NotNull(mobaFrameRoute);
        Assert.False(mobaFrameStartRoute.RequiresBattleRuntime);
        Assert.False(mobaFrameStartRoute.IsUnsupportedTemplate);
        Assert.Equal("frame-sync-authority", mobaFrameStartRoute.SyncTemplateId);
        Assert.Equal(123UL, mobaFrameRoute!.RoomId);
        Assert.Equal(123UL, mobaFrameRoute.WorldId);
        Assert.Equal(30, mobaFrameRoute.TickRate);
        Assert.Equal("battle-1", mobaFrameRoute.BattleId);
        Assert.Equal("frame-sync-authority", mobaFrameRoute.SyncTemplateId);
        Assert.Null(mobaStateRoute);
        Assert.True(mobaStateStartRoute.RequiresBattleRuntime);
        Assert.False(mobaStateStartRoute.IsUnsupportedTemplate);
        Assert.Equal("state-sync-authority", mobaStateStartRoute.SyncTemplateId);
        Assert.Null(shooterRoute);
        Assert.True(shooterStartRoute.RequiresBattleRuntime);
        Assert.False(shooterStartRoute.IsUnsupportedTemplate);
    }

    [Fact]
    public void ServerGameplayManifest_WhenBuiltFromDefaultCatalog_ExposesPlayableCapabilities()
    {
        var manifest = ServerGameplayManifest.FromCatalog(ServerGameplayModuleCatalog.Default);

        var moba = manifest.Resolve(GameplayRoomTypes.Moba);
        var shooter = manifest.Resolve(ShooterGameplay.RoomType);

        Assert.Equal(GameplayRoomTypes.Moba, moba.RoomType);
        Assert.True(moba.RequiresPlayerLoadout);
        Assert.True(moba.SupportsFrameSync);
        Assert.Contains("state-sync-authority", moba.SupportedSyncTemplateIds);
        Assert.Equal(ShooterGameplay.RoomType, shooter.RoomType);
        Assert.False(shooter.RequiresPlayerLoadout);
        Assert.True(shooter.SupportsStateSyncPush);
        Assert.Contains(ShooterServerProtocol.PredictRollbackAuthorityTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.AuthoritativeInterpolationPresentationTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.BatchStateLowFrequencyTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.MassBattleLodAoiTemplate, shooter.SupportedSyncTemplateIds);
        Assert.Contains(ShooterServerProtocol.HybridHeroPredictionTemplate, shooter.SupportedSyncTemplateIds);
    }

    [Fact]
    public void ServerBattleWorldManager_WhenCreatingWorlds_UsesGameplayModuleWorldBlueprints()
    {
        const string mobaRoomId = "moba-room";
        const int tickRate = 30;
        var initParams = CreateMobaWorldInitParams();
        var launchSpec = DefaultOrleansBattleProtocolMapper.Instance.CreateLaunchSpec(
            mobaRoomId,
            tickRate,
            initParams);
        var initData = launchSpec.ToWorldInitData(MobaWorldBootstrapModule.InitOpCode);

        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var mobaWorld = worldManager.CreateBattleWorld(
            mobaRoomId,
            tickRate,
            options =>
            {
                options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
                options.ServiceBuilder.RegisterInstance(initData);
            });
        var shooterWorld = worldManager.CreateBattleWorld("shooter-room", ShooterGameplay.WorldType, tickRate);

        Assert.Equal(MobaBattleWorldBlueprint.Type, mobaWorld.WorldType);
        Assert.True(mobaWorld.Services.TryResolve<IMobaBattleRuntimePort>(out var mobaRuntimePort));
        Assert.NotNull(mobaRuntimePort);
        Assert.Equal(ShooterGameplay.WorldType, shooterWorld.WorldType);
    }

    [Fact]
    public void MobaBattleRuntimeSession_WhenBootstrapStartsGameplay_StartsSuccessfullyOnce()
    {
        const string battleId = "moba-runtime-start";
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new MobaBattleRuntimeAdapter(
            worldManager,
            DefaultOrleansBattleProtocolMapper.Instance);
        using var session = adapter.CreateSession(battleId);

        var result = session.Start(CreateMobaWorldInitParams());

        Assert.True(result.Succeeded, result.Error);
        Assert.Null(result.Error);
        Assert.True(session.Tick(1, 30, 1f / 30f));
    }

    private static RoomSummary CreateSummary(string roomType)
    {
        return new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "room-a",
            RoomType: roomType,
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 10,
            PlayerCount: 0,
            OwnerAccountId: "account-a",
            CreatedAtUnixMs: 0,
            Tags: null);
    }

    private static BattleInitParams CreateMobaWorldInitParams()
    {
        return new BattleInitParams
        {
            WorldId = 1UL,
            TickRate = 30,
            MapId = 1,
            GameplayId = 1,
            RandomSeed = 12345,
            WorldType = MobaBattleWorldBlueprint.Type,
            Players = new List<PlayerInitInfo>
            {
                new()
                {
                    PlayerId = 1,
                    ActorId = 1,
                    HeroId = 1001,
                    TeamId = 1,
                    Level = 1,
                    AttributeTemplateId = 1001,
                    BasicAttackSkillId = 10010001,
                    SkillIds = new List<int> { 10010101, 10010201, 10010301 }
                }
            }
        };
    }

    private static BattleInitParams CreateInitParams(string? syncTemplateId)
    {
        return new BattleInitParams
        {
            WorldId = 123UL,
            TickRate = 30,
            SyncOptions = syncTemplateId is null
                ? null
                : new BattleSyncStartOptions(syncTemplateId, 0, null, null, true, false, 0)
        };
    }
}
