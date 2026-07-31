using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// 配置阶段。
    /// 注册配置相关的服务。
    /// </summary>
    [MobaBootstrapStage]
    public sealed class ConfigStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.Config;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.CoreState,
        };

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegister<IMobaConfigTableRegistry>(WorldLifetime.Singleton, _ => MobaConfigRegistry.Instance);

            builder.TryRegister<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());
            builder.TryRegister<IMobaConfigDtoProvider>(WorldLifetime.Singleton, _ => EmptyMobaConfigDtoProvider.Instance);
            builder.TryRegister<IMobaConfigLoadProfile>(WorldLifetime.Singleton, _ => ResourcesJsonMobaConfigLoadProfile.Default);
            builder.TryRegister<MobaBrainDecisionDriverRegistry>(
                WorldLifetime.Singleton,
                _ => MobaBrainDecisionDriverRegistry.CreateDefault());
            builder.TryRegister<IMobaActorBrainCatalog>(WorldLifetime.Singleton, _ =>
            {
                var catalog = new MobaActorBrainCatalog();
                MobaActorBrainCatalogJsonLoader.Load(_.Resolve<ITextAssetLoader>(), catalog);
                return catalog;
            });
            builder.TryRegister<IMobaActorStateMachineProfileCatalog>(WorldLifetime.Singleton, _ =>
            {
                var catalog = new MobaActorStateMachineProfileCatalog();
                MobaActorStateMachineProfileJsonLoader.Load(_.Resolve<ITextAssetLoader>(), catalog);
                return catalog;
            });
            builder.TryRegister<IMobaConfigLoadPipeline>(WorldLifetime.Singleton, _ =>
            {
                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                var textAssetLoader = _.Resolve<ITextAssetLoader>();
                return new MobaConfigLoadPipeline(registry ?? MobaConfigRegistry.Instance, textAssetLoader);
            });

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                var textAssetLoader = _.Resolve<ITextAssetLoader>();

                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                _.TryResolve<IMobaConfigDtoDeserializer>(out var deserializer);
                _.TryResolve<IMobaConfigDtoBytesDeserializer>(out var bytesDeserializer);
                _.TryResolve<IMobaConfigLoadProfile>(out var loadProfile);
                _.TryResolve<IMobaConfigLoadPipeline>(out var loadPipeline);
                loadPipeline ??= new MobaConfigLoadPipeline(registry ?? MobaConfigRegistry.Instance, textAssetLoader);
                var db = new MobaConfigDatabase(registry, deserializer, bytesDeserializer, textAssetLoader);

                try
                {
                    loadProfile ??= ResourcesJsonMobaConfigLoadProfile.Default;
                    loadProfile.Load(db, loadPipeline);
                    return db;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[ConfigStage] Failed to load configs");
                    throw;
                }
            });
        }

        protected internal override void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
            MobaBrainConfigurationValidator.Validate(
                services.Resolve<IMobaActorBrainCatalog>(),
                services.Resolve<IMobaActorStateMachineProfileCatalog>(),
                services.Resolve<MobaActorStateMachineRuntimeRegistry>(),
                services.Resolve<MobaBrainDecisionDriverRegistry>());
        }
    }
}
