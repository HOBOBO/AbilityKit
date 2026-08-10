using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.FrameSync;

namespace AbilityKit.Game.Flow
{
    internal static class SessionMobaWorldBootstrapFactory
    {
        public static IWorldManager CreateWorldManager()
        {
            var typeRegistry = new WorldTypeRegistry()
                .RegisterEntitasWorld(AbilityKit.Demo.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Demo.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Demo.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

            var baseFactory = new RegistryWorldFactory(typeRegistry);
            var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
            return new WorldManager(factory);
        }

        public static WorldCreateOptions CreateWorldOptions(
            BattleStartPlan plan,
            WorldId worldId,
            IWorldAuthorityFramesSource authorityFramesSource = null,
            bool registerWorldInitData = true,
            bool replayInputValidated = false)
        {
            var options = new WorldCreateOptions(worldId, plan.World.WorldType)
            {
                ServiceBuilder = CreateServiceBuilder(
                    plan,
                    authorityFramesSource,
                    registerWorldInitData,
                    replayInputValidated),
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            return options;
        }

        private static WorldContainerBuilder CreateServiceBuilder(
            BattleStartPlan plan,
            IWorldAuthorityFramesSource authorityFramesSource,
            bool registerWorldInitData,
            bool replayInputValidated)
        {
            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Demo.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );
            var textAssetLoader = new ResourcesTextAssetLoader();
            builder.RegisterInstance<ITextAssetLoader>(textAssetLoader);
            builder.RegisterInstance<ITextAssetDirectoryLoader>(textAssetLoader);
            builder.AddModule(new MobaConfigWorldModule());
            RegisterLogicWorldDriveProfile(plan, builder, replayInputValidated);
            if (registerWorldInitData)
            {
                var createWorld = plan.CreateWorld;
                builder.RegisterInstance(new WorldInitData(createWorld.OpCode, createWorld.Payload));
            }
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            builder.TryRegister<ICollisionService>(WorldLifetime.Singleton, _ => new CollisionService(new CollisionWorldOptions { BroadphaseType = BroadphaseType.Grid, GridCellSize = 4f }));

            if (authorityFramesSource != null)
            {
                builder.RegisterInstance(authorityFramesSource);
            }

            return builder;
        }

        private static void RegisterLogicWorldDriveProfile(
            BattleStartPlan plan,
            WorldContainerBuilder builder,
            bool replayInputValidated)
        {
            var launchSpec = plan.GetCanonicalLaunchSpec();
            var replayMode = replayInputValidated ||
                launchSpec.LaunchMode == MobaBattleLaunchMode.Replay ||
                launchSpec.SyncMode == MobaBattleLaunchSyncMode.Replay ||
                plan.RunModeOptions.EnableInputReplay;
            var authorityMode = launchSpec.AuthorityMode;
            if (authorityMode == MobaBattleLaunchAuthorityMode.Unspecified)
            {
                authorityMode = plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote
                    ? MobaBattleLaunchAuthorityMode.ServerAuthority
                    : plan.Authority.EnableClientPrediction
                        ? MobaBattleLaunchAuthorityMode.ClientPrediction
                        : MobaBattleLaunchAuthorityMode.LocalAuthority;
            }

            var syncMode = launchSpec.SyncMode;
            if (syncMode == MobaBattleLaunchSyncMode.Unspecified)
            {
                syncMode = ToLaunchSyncMode(plan.Sync.SyncMode);
            }

            var ownsSimulation = OwnsLocalSimulation(plan.HostMode, authorityMode);
            builder.Register<MobaLogicWorldDriveStateService>(
                WorldLifetime.Scoped,
                _ =>
                {
                    var state = new MobaLogicWorldDriveStateService();
                    state.Configure(
                        syncMode,
                        authorityMode,
                        ownsSimulation,
                        replayMode,
                        replayReady: !replayMode || replayInputValidated,
                        reason: replayInputValidated
                            ? "validated replay input"
                            : "view session launch profile");
                    return state;
                });
        }

        private static bool OwnsLocalSimulation(
            BattleStartConfig.BattleHostMode hostMode,
            MobaBattleLaunchAuthorityMode authorityMode)
        {
            return authorityMode == MobaBattleLaunchAuthorityMode.ClientPrediction ||
                hostMode != BattleStartConfig.BattleHostMode.GatewayRemote;
        }

        private static MobaBattleLaunchSyncMode ToLaunchSyncMode(BattleSyncMode syncMode)
        {
            switch (syncMode)
            {
                case BattleSyncMode.Lockstep:
                    return MobaBattleLaunchSyncMode.FrameSync;
                case BattleSyncMode.SnapshotAuthority:
                    return MobaBattleLaunchSyncMode.StateSync;
                case BattleSyncMode.HybridPredictReconcile:
                    return MobaBattleLaunchSyncMode.Hybrid;
                default:
                    return MobaBattleLaunchSyncMode.Unspecified;
            }
        }
    }
}
