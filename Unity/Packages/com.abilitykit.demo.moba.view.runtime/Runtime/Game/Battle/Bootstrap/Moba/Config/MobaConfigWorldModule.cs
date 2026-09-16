using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;

namespace AbilityKit.Game.Battle.Moba.Config
{
    /// <summary>
    /// 配置模块 - 复用运行时包的 MobaConfigDatabase 注册
    /// 视图包负责注册 View 层依赖，如 ITextAssetLoader
    /// </summary>
    [WorldService(typeof(ITextAssetLoader), WorldLifetime.Singleton)]
    public sealed class MobaConfigWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<IMobaConfigTableRegistry>(WorldLifetime.Singleton, _ => MobaConfigRegistry.Instance);
            builder.TryRegister<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());
            builder.TryRegister<IMobaConfigDtoProvider>(WorldLifetime.Singleton, _ => EmptyMobaConfigDtoProvider.Instance);
            builder.TryRegister<IMobaConfigLoadProfile>(WorldLifetime.Singleton, _ => ResourcesJsonMobaConfigLoadProfile.Default);
            builder.TryRegister<CharacterPresentationActionCatalog>(WorldLifetime.Singleton, _ =>
                CharacterPresentationActionCatalog.Load(_.Resolve<ITextAssetLoader>()));
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
                    Log.Exception(ex, "[MobaConfigWorldModule] Failed to load configs");
                    throw;
                }
            });
        }
    }
}
