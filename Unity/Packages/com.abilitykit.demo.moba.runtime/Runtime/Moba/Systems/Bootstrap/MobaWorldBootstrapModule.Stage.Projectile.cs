using AbilityKit.Core.Common.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterProjectileServices(WorldContainerBuilder builder)
        {
            builder.TryRegisterService<IProjectileService, ProjectileService>();
            builder.RegisterService<IProjectileReturnTargetProvider, MobaProjectileReturnTargetProvider>();
            builder.RegisterService<MobaProjectileLinkService, MobaProjectileLinkService>();
            builder.RegisterService<MobaProjectileService, MobaProjectileService>();
            builder.RegisterService<MobaAreaTriggerRegistry, MobaAreaTriggerRegistry>();
        }
    }
}
