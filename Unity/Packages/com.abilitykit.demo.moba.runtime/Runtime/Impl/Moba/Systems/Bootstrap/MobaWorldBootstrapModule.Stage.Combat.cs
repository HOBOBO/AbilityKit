using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterCombatServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaDamageService, MobaDamageService>();
            builder.RegisterService<DamagePipelineService, DamagePipelineService>();
        }
    }
}
