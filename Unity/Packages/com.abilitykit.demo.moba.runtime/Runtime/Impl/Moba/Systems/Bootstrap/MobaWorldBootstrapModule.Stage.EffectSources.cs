using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterEffectSources(WorldContainerBuilder builder)
        {
            builder.TryRegisterService<EffectSourceRegistry, EffectSourceRegistry>();
        }
    }
}
