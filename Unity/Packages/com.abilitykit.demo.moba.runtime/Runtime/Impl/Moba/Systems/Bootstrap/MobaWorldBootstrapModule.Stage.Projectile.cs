using AbilityKit.Core.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
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
