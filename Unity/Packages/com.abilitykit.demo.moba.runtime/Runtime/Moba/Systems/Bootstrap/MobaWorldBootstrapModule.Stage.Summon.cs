using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterSummonServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaSummonService, MobaSummonService>();
            builder.RegisterService<MobaSummonDeathSubscriber, MobaSummonDeathSubscriber>();
            builder.RegisterService<MobaComponentTemplateService, MobaComponentTemplateService>();
        }
    }
}
