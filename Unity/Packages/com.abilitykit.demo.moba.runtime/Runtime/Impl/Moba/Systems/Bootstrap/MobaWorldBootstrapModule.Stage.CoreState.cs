using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterCoreState(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaLobbyStateService, MobaLobbyStateService>();
            builder.RegisterService<MobaAuthorityFrameService, MobaAuthorityFrameService>();
        }
    }
}
