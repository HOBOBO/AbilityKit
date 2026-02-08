using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Tags;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterTags(WorldContainerBuilder builder)
        {
            builder.TryRegister<ITagTemplateRegistry>(WorldLifetime.Singleton, r => new MobaTagTemplateRegistry(r.Resolve<MobaConfigDatabase>()));
            builder.TryRegisterType<IGameplayTagService, GameplayTagService>(WorldLifetime.Scoped);
            builder.TryRegisterType<IDurableRegistry, DurableRegistry>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITagEffectRouter, TagEffectRouter>(WorldLifetime.Scoped);
        }
    }
}
