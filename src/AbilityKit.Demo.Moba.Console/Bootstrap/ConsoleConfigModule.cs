using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Demo.Moba.Triggering;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    public sealed class ConsoleConfigModule : IWorldModule
    {
        private static readonly MobaTriggerPlanLoadProfile TriggerPlanOverlays = new MobaTriggerPlanLoadProfile(
            new[] { TriggerPlanLoadEntry.Directory("Ability rules", "luban/ability/rules", "**/*.json") },
            failFastOnDirectoryLoad: false);

        private readonly string _resourcesDir;
        private readonly string _lubanResourcesDir;
        private readonly string _triggerPlansPath;

        public ConsoleConfigModule(string resourcesDir = "luban/moba", string lubanResourcesDir = "luban/moba", string triggerPlansPath = "luban/ability/ability_trigger_plans.json")
        {
            _resourcesDir = resourcesDir;
            _lubanResourcesDir = lubanResourcesDir;
            _triggerPlansPath = triggerPlansPath;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Register<ITextAssetLoader>(WorldLifetime.Singleton, _ => new ConsoleTextAssetLoader());
            builder.Register<IMobaConfigTableRegistry>(WorldLifetime.Singleton, _ => MobaConfigRegistry.Instance);
            builder.Register<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.Register<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            builder.Register<DefaultMobaConfigLoader>(WorldLifetime.Singleton, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                var registry = container.Resolve<IMobaConfigTableRegistry>();
                return new DefaultMobaConfigLoader(registry, textAssetLoader);
            });

            builder.Register<LubanConfigGroup>(WorldLifetime.Singleton, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                return LubanConfigGroup.Create(textAssetLoader, _lubanResourcesDir);
            });

            builder.Register<MobaConfigDatabase>(WorldLifetime.Singleton, container =>
            {
                var registry = container.Resolve<IMobaConfigTableRegistry>();
                var deserializer = container.Resolve<IMobaConfigDtoDeserializer>();
                var textAssetLoader = container.Resolve<ITextAssetLoader>();

                try
                {
                    return MobaConfigDatabaseFactory.Create(
                        textAssetLoader, registry, deserializer, bytesDeserializer: null,
                        profile: new LubanGroupsMobaConfigLoadProfile(textAssetLoader));
                }
                catch (Exception ex)
                {
                    Platform.Log.Error($"[ConsoleConfigModule] Failed to load MobaConfigDatabase from resources '{_resourcesDir}': {ex.Message}");
                    throw;
                }
            });

            builder.Register<TriggerPlanJsonDatabase>(WorldLifetime.Scoped, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                var cueFactory = new MobaPresentationCueFactory(container.Resolve<MobaPresentationCueSnapshotService>());
                try
                {
                    var database = MobaTriggerPlanLoader.LoadWithAggregate(
                        new ConsoleTriggerPlanTextSource(textAssetLoader), _triggerPlansPath, TriggerPlanOverlays, cueFactory);
                    Platform.Log.System($"[ConsoleConfigModule] Loaded {database.Records?.Count ?? 0} trigger plans from editor aggregate and rules");
                    return database;
                }
                catch (Exception ex)
                {
                    Platform.Log.Error($"[ConsoleConfigModule] Failed to load TriggerPlanJsonDatabase: {ex.Message}");
                    throw;
                }
            });
        }

    }
}
