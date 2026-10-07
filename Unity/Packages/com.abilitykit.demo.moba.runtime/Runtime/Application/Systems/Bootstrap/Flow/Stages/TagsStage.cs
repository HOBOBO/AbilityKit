using AbilityKit.Ability.Tags;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.GameplayTags;
using ITagEffectRouter = AbilityKit.GameplayTags.ITagEffectRouter;
using IDurableRegistry = AbilityKit.GameplayTags.IDurableRegistry;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// Tags 阶段。
    /// 注册 GameplayTag 相关服务。
    /// </summary>
    [MobaBootstrapStage]
    public sealed class TagsStage : MobaBootstrapStageBase
    {
        public override string Name => "Tags";

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegister<IMobaContinuousTagTemplateRegistry>(WorldLifetime.Singleton, r => new MobaContinuousTagTemplateRegistry(r.Resolve<MobaConfigDatabase>()));
            builder.TryRegisterType<MobaTagPresentationCueReporter, MobaTagPresentationCueReporter>(WorldLifetime.Scoped);
            builder.TryRegisterType<IDurableRegistry, DurableRegistry>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITagEffectRouter, TagEffectRouter>(WorldLifetime.Scoped);
        }
    }
}
