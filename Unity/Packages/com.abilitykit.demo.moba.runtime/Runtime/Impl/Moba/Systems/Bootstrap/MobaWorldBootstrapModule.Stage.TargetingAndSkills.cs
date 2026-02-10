using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Systems;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterTargetingAndSkillServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<SearchTargetService, SearchTargetService>();
            builder.RegisterService<MobaSkillLoadoutService, MobaSkillLoadoutService>();

            builder.TryRegister<MobaEventSubscriptionRegistry>(WorldLifetime.Singleton, _ =>
            {
                var reg = new MobaEventSubscriptionRegistry();
                reg.RegisterPrefix<SkillCastContext>("skill.");
                reg.RegisterPrefix<BuffEventArgs>("buff.");
                return reg;
            });

            builder.TryRegister<MobaTriggerIndexService>(WorldLifetime.Singleton, _ =>
            {
                var loader = _.Resolve<ITextLoader>();
                var s = new MobaTriggerIndexService(loader);
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources begin");
                s.LoadFromResources();
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources end");
                return s;
            });
            builder.RegisterService<MobaEffectExecutionService, MobaEffectExecutionService>();

            builder.RegisterService<MobaOngoingEffectService, MobaOngoingEffectService>();
            builder.RegisterService<MobaEffectExecuteSubscriber, MobaEffectExecuteSubscriber>();
            builder.RegisterService<MobaEffectExecuteDemoSubscriber, MobaEffectExecuteDemoSubscriber>();
            builder.RegisterService<MobaEffectApplyDemoSubscriber, MobaEffectApplyDemoSubscriber>();
        }
    }
}
