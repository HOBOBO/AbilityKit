using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterCoreState(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaLobbyStateService, MobaLobbyStateService>();
            builder.RegisterService<MobaAuthorityFrameService, MobaAuthorityFrameService>();

            // Deterministic + rollbackable RNG (override default world random)
            builder.Register<IWorldRandom>(WorldLifetime.Scoped, _ => new RollbackWorldRandom());
        }
    }
}
