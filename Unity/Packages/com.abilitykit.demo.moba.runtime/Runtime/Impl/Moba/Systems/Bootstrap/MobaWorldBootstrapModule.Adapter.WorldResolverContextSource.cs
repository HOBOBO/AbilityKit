using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private sealed class WorldResolverContextSource : AbilityKit.Triggering.Runtime.ITriggerContextSource<IWorldResolver>
        {
            private readonly IWorldResolver _services;

            public WorldResolverContextSource(IWorldResolver services)
            {
                _services = services;
            }

            public IWorldResolver GetContext() => _services;
        }
    }
}
