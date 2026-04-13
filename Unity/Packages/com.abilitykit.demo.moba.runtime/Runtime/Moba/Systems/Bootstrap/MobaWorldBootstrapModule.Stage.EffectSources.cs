using AbilityKit.Demo.Moba.EffectSource;
using AbilityKit.Ability.World.DI;
using EffectSourceRegistry = AbilityKit.Demo.Moba.EffectSource.MobaTraceRegistry;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterEffectSources(WorldContainerBuilder builder)
        {
            builder.TryRegisterService<EffectSourceRegistry, EffectSourceRegistry>();
        }
    }
}
