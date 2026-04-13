using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterCombatServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaDamageService, MobaDamageService>();
            builder.RegisterService<DamagePipelineService, DamagePipelineService>();
            builder.RegisterService<MobaUnitDeathSubscriber, MobaUnitDeathSubscriber>();
        }
    }
}
