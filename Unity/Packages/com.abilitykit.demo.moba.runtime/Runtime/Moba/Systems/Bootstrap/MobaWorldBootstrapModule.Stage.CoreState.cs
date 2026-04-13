using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterCoreState(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaGamePhaseService, MobaGamePhaseService>();
            builder.RegisterService<MobaAuthorityFrameService, MobaAuthorityFrameService>();

            // Deterministic + rollbackable RNG (override default world random)
            builder.Register<IWorldRandom>(WorldLifetime.Scoped, _ => new RollbackWorldRandom());
        }
    }
}
