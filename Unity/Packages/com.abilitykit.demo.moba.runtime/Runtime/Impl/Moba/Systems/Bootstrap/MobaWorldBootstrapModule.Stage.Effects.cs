using AbilityKit.Ability.Impl.Moba.Effects.Config;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;
using AbilityKit.Effects.Core;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterEffects(WorldContainerBuilder builder)
        {
            builder.TryRegister<EffectRegistry>(WorldLifetime.Singleton, _ => new EffectRegistry());
            builder.TryRegisterService<MobaEffectRegistryBootstrapService, MobaEffectRegistryBootstrapService>(WorldLifetime.Singleton);

            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());
        }
    }
}
