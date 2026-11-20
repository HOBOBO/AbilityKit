using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
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
        Assert.True(mobaProfile.SupportsStateSyncPush);
        Assert.True(mobaProfile.SupportsTemplate("moba-state-sync-authority"));
        Assert.Equal(ServerBattleSyncMode.FrameSync, mobaProfile.ResolveTemplate(null).Mode);
        Assert.Equal(ServerBattleRuntimeMode.BattleWorldWithFrameSync, mobaProfile.ResolveTemplate(null).RuntimeMode);
        Assert.True(mobaProfile.ResolveTemplate(null).RequiresBattleRuntime);
        Assert.Equal(ServerBattleSyncMode.FrameSync, mobaProfile.ResolveTemplate("frame-sync-authority").Mode);
        Assert.Equal(ServerBattleRuntimeMode.BattleWorldWithFrameSync, mobaProfile.ResolveTemplate("frame-sync-authority").RuntimeMode);
        Assert.Equal(ServerBattleSyncMode.StateSync, mobaProfile.ResolveTemplate("moba-state-sync-authority").Mode);
        Assert.Equal(ServerBattleRuntimeMode.BattleWorld, mobaProfile.ResolveTemplate("moba-state-sync-authority").RuntimeMode);
        Assert.Equal("frame-sync-authority", moduleCatalog.GameplayCatalog.Resolve(GameplayRoomTypes.Moba).DefaultSyncTemplateId);
        Assert.Equal(ServerBattleSyncMode.StateSync, shooterProfile.DefaultMode);
        Assert.Equal(ShooterServerProtocol.StateSyncAuthorityTemplate, shooterProfile.DefaultTemplateId);
        Assert.Equal(ShooterServerProtocol.StateSyncAuthorityTemplate, moduleCatalog.GameplayCatalog.Resolve(ShooterGameplay.RoomType).DefaultSyncTemplateId);
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
        var mobaStateRoute = RoomFrameSyncRoute.Resolve(mobaSummary, "battle-1", CreateInitParams("moba-state-sync-authority"));
        var shooterRoute = RoomFrameSyncRoute.Resolve(shooterSummary, "battle-2", CreateInitParams(syncTemplateId: null));
        var mobaFrameStartRoute = RoomFrameSyncRoute.ResolveStartRoute(mobaSummary, "battle-1", CreateInitParams(syncTemplateId: null));
        var mobaStateStartRoute = RoomFrameSyncRoute.ResolveStartRoute(mobaSummary, "battle-1", CreateInitParams("moba-state-sync-authority"));
        var shooterStartRoute = RoomFrameSyncRoute.ResolveStartRoute(shooterSummary, "battle-2", CreateInitParams(syncTemplateId: null));

        Assert.NotNull(mobaFrameRoute);
        Assert.True(mobaFrameStartRoute.RequiresBattleRuntime);
        Assert.False(mobaFrameStartRoute.IsUnsupportedTemplate);
        Assert.Equal("frame-sync-authority", mobaFrameStartRoute.SyncTemplateId);
        Assert.Equal(123UL, mobaFrameRoute!.RoomId);
        Assert.Equal(123UL, mobaFrameRoute.WorldId);
        Assert.Equal(30, mobaFrameRoute.TickRate);
        Assert.Equal("battle-1", mobaFrameRoute.BattleId);
        Assert.Equal("frame-sync-authority", mobaFrameRoute.SyncTemplateId);
        Assert.True(mobaFrameRoute.EnableRecording);
        Assert.Null(mobaStateRoute);
        Assert.True(mobaStateStartRoute.RequiresBattleRuntime);
        Assert.False(mobaStateStartRoute.IsUnsupportedTemplate);
        Assert.Equal("moba-state-sync-authority", mobaStateStartRoute.SyncTemplateId);
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
        Assert.True(moba.SupportsStateSyncPush);
        Assert.Contains("frame-sync-authority", moba.SupportedSyncTemplateIds);
        Assert.Contains("moba-state-sync-authority", moba.SupportedSyncTemplateIds);
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
        var initialState = session.CreateStateSyncPush(1UL, frame: 0, isFullSnapshot: true);
        var actor = Assert.Single(initialState.Actors);
        Assert.Equal(-12f, actor.X, 3);
        Assert.Equal(0f, actor.Z, 3);
        Assert.True(session.Tick(1, 30, 1f / 30f));
    }

    [Fact]
    public void MobaBattleRuntimeSession_RequiresFreshGuardCompletionAndTheMatchingWorldFrame()
    {
        const string battleId = "moba-runtime-settlement-guard";
        const float fixedDelta = 1f / 30f;
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new MobaBattleRuntimeAdapter(worldManager, DefaultOrleansBattleProtocolMapper.Instance);
        using var session = adapter.CreateSession(battleId);
        var result = session.Start(CreateMobaWorldInitParams());
        Assert.True(result.Succeeded, result.Error);
        var completion = Assert.IsAssignableFrom<IBattleRuntimeFrameCompletion>(session);
        var world = worldManager.CreateBattleWorld(battleId, 30);
        Assert.True(world.Services.TryResolve<IFrameTime>(out var worldTime));
        var time = Assert.IsType<FrameTime>(worldTime);
        Assert.True(world.Services.TryResolve<MobaCombatFrameGuard>(out var guard));

        var ticks = guard!.CompletedTickCount;
        Assert.False(completion.CanFinalizeFrame(0));
        Assert.True(session.Tick(0, 30, fixedDelta));
        Assert.Equal(ticks + 1, guard.CompletedTickCount);
        Assert.Equal(0, time.Frame.Value);
        Assert.Equal(0f, time.DeltaTime);
        Assert.False(completion.CanFinalizeFrame(0));

        time.StepTo(new FrameIndex(1), fixedDelta);
        Assert.True(session.Tick(1, 30, fixedDelta));
        Assert.True(completion.CanFinalizeFrame(1));
        time.StepTo(new FrameIndex(2), fixedDelta);
        Assert.True(session.Tick(2, 30, fixedDelta));
        Assert.False(completion.CanFinalizeFrame(1));
        Assert.True(completion.CanFinalizeFrame(2));

        Assert.True(session.Tick(3, 30, fixedDelta));
        Assert.False(completion.CanFinalizeFrame(2));
        Assert.False(completion.CanFinalizeFrame(3));

        time.StepTo(new FrameIndex(4), fixedDelta);
        guard.FailFrame(new InvalidOperationException("Test guard fault."));
        Assert.Throws<InvalidOperationException>(() => session.Tick(4, 30, fixedDelta));
        Assert.False(completion.CanFinalizeFrame(4));
    }

    [Fact]
    public void MobaExternalFrameClock_DrivesPublishedSummonExpiryAtTheExpectedFrame()
    {
        const string battleId = "moba-runtime-external-frame-clock";
        const float fixedDelta = 1f / 30f;
        const int pressFrame = 3;
        const int summonConfig = 30030502;
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new MobaBattleRuntimeAdapter(worldManager, DefaultOrleansBattleProtocolMapper.Instance);
        using var session = adapter.CreateSession(battleId);
        var init = CreateMobaWorldInitParams();
        var player = Assert.Single(init.Players!);
        player.HeroId = 3003;
        player.AttributeTemplateId = 3003;
        player.BasicAttackSkillId = 30030101;
        player.SkillIds = new List<int> { 30030201, 30030301, 30030401, 30030501 };
        var start = session.Start(init);
        Assert.True(start.Succeeded, start.Error);

        var externalClock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(session);
        var completion = Assert.IsAssignableFrom<IBattleRuntimeFrameCompletion>(session);
        var world = worldManager.CreateBattleWorld(battleId, 30);
        Assert.True(world.Services.TryResolve<IFrameTime>(out var worldTime));
        var time = Assert.IsType<FrameTime>(worldTime);
        Assert.True(world.Services.TryResolve<MobaCombatFrameGuard>(out var guard));
        Assert.True(world.Services.TryResolve<MobaSummonService>(out var summons));
        Assert.True(world.Services.TryResolve<MobaActorLookupService>(out var actors));
        Assert.True(world.Services.TryResolve<MobaActorRegistry>(out var actorRegistry));
        var caster = Assert.Single(session.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);
        var expectedReplayClock = new FrameTime();
        var summonedActors = new List<int>();
        var spawnFrame = -1;
        var expiryFrame = -1;
        long deadlineMs = 0;
        Assert.True(externalClock.HasInitialFrameCheckpoint);
        Assert.False(externalClock.TryPrepareExternalFrame(0, fixedDelta));
        Assert.False(externalClock.TryPrepareExternalFrame(2, fixedDelta));
        Assert.Equal(0, time.Frame.Value);
        Assert.Equal(0f, time.Time);

        for (var frame = 1; frame <= 1200; frame++)
        {
            Assert.True(externalClock.TryPrepareExternalFrame(frame, fixedDelta));
            Assert.True(expectedReplayClock.AlignTo(new FrameIndex(frame), fixedDelta));
            Assert.Equal(frame, time.Frame.Value);
            Assert.Equal(expectedReplayClock.TimeMilliseconds, time.TimeMilliseconds);
            Assert.Equal(expectedReplayClock.Time, time.Time);

            if (frame is pressFrame or pressFrame + 1)
            {
                var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
                var phase = frame == pressFrame ? SkillInputPhase.Press : SkillInputPhase.Release;
                var input = new SkillInputEvent(4, phase, aimPos: in aim, targetFrame: frame,
                    inputSequence: frame, predictionKey: frame, entityVersion: actorRegistry!.GetEntityVersion(1));
                Assert.Equal(1, session.SubmitInputs(frame, new[]
                {
                    new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in input) }
                }));
            }

            Assert.True(session.Tick(frame, 30, fixedDelta));
            Assert.True(completion.CanFinalizeFrame(frame));
            if (spawnFrame < 0 && summons!.TryGetSummons(1, summonedActors, summonConfig))
            {
                Assert.True(actors!.TryGetActorEntity(Assert.Single(summonedActors), out var summon));
                Assert.True(summon.hasLifetime);
                deadlineMs = summon.lifetime.EndTimeMs;
                spawnFrame = frame;
                Assert.Equal(pressFrame, spawnFrame);
                Assert.True(deadlineMs > MathF.Round(time.Time * 1000f));
            }

            if (spawnFrame >= 0)
            {
                var expired = MathF.Round(time.Time * 1000f) >= deadlineMs;
                Assert.Equal(expired ? 0 : 1, summons!.GetAliveCount(1, summonConfig));
                if (expired)
                {
                    expiryFrame = frame;
                    break;
                }
            }
        }

        Assert.True(spawnFrame == pressFrame && expiryFrame > spawnFrame,
            $"Missing press-frame spawn or natural expiry (spawn={spawnFrame}, expiry={expiryFrame}).");
        Assert.False(externalClock.TryPrepareExternalFrame(expiryFrame, fixedDelta));
        Assert.False(externalClock.TryPrepareExternalFrame(expiryFrame + 2, fixedDelta));
        guard!.FailFrame(new InvalidOperationException("Test guard fault."));
        Assert.False(externalClock.TryPrepareExternalFrame(expiryFrame + 1, fixedDelta));
        Assert.Equal(expiryFrame, time.Frame.Value);
    }

    [Fact]
    public void MobaBattleRuntimeSession_WhenMoveInputIsSubmitted_AdvancesAuthoritativeProjection()
    {
        const string battleId = "moba-runtime-authoritative-move";
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new MobaBattleRuntimeAdapter(
            worldManager,
            DefaultOrleansBattleProtocolMapper.Instance);
        using var session = adapter.CreateSession(battleId);

        var start = session.Start(CreateMobaWorldInitParams());
        Assert.True(start.Succeeded, start.Error);

        var initialState = session.CreateStateSyncPush(1UL, frame: 0, isFullSnapshot: true);
        var initialActor = Assert.Single(initialState.Actors);
        var submitted = session.SubmitInputs(
            0,
            new[]
            {
                new BattleInputItem
                {
                    PlayerId = 1,
                    OpCode = MobaOpCodes.Input.Move,
                    Payload = MobaMoveCodec.Serialize(1f, 0f)
                }
            });

        Assert.Equal(1, submitted);
        Assert.True(session.Tick(1, 30, 1f / 30f));

        var movedState = session.CreateStateSyncPush(1UL, frame: 1, isFullSnapshot: true);
        var movedActor = Assert.Single(movedState.Actors);
        Assert.True(
            movedActor.X > initialActor.X,
            $"Authoritative actor did not move. initial=({initialActor.X},{initialActor.Y},{initialActor.Z}), moved=({movedActor.X},{movedActor.Y},{movedActor.Z})");
    }

    [Fact]
    public void MobaBattleRuntimeSession_WhenInputIsRejected_ExposesRuntimeDiagnostic()
    {
        const string battleId = "moba-runtime-input-diagnostic";
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new MobaBattleRuntimeAdapter(
            worldManager,
            DefaultOrleansBattleProtocolMapper.Instance);
        using var session = adapter.CreateSession(battleId);

        var start = session.Start(CreateMobaWorldInitParams());
        Assert.True(start.Succeeded, start.Error);

        var submitted = session.SubmitInputs(
            0,
            new[]
            {
                new BattleInputItem
                {
                    PlayerId = 1,
                    OpCode = int.MaxValue,
                    Payload = Array.Empty<byte>()
                }
            });

        Assert.Equal(0, submitted);
        var diagnostics = Assert.IsAssignableFrom<IBattleRuntimeInputDiagnostics>(session);
        Assert.Contains("NoCommandHandled", diagnostics.LastInputSubmitDiagnostic);
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
