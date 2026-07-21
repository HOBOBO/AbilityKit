using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
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
            bool registerWorldInitData = true)
        {
            var options = new WorldCreateOptions(worldId, plan.World.WorldType)
            {
                ServiceBuilder = CreateServiceBuilder(plan, authorityFramesSource, registerWorldInitData),
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            return options;
        }

        private static WorldContainerBuilder CreateServiceBuilder(
            BattleStartPlan plan,
            IWorldAuthorityFramesSource authorityFramesSource,
            bool registerWorldInitData)
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
            if (registerWorldInitData)
            {
                var createWorld = plan.CreateWorld;
                builder.RegisterInstance(new WorldInitData(createWorld.OpCode, createWorld.Payload));
            }
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            builder.TryRegister<ICollisionService>(WorldLifetime.Singleton, _ => new CollisionService());

            if (authorityFramesSource != null)
            {
                builder.RegisterInstance(authorityFramesSource);
            }

            return builder;
        }
    }
}
