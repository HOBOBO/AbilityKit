using System;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldServiceProviderAdapter : IServiceProvider
    {
        private readonly IWorldServices _services;

        public WorldServiceProviderAdapter(IWorldServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) return null;
            return _services.TryResolve(serviceType, out var instance) ? instance : null;
        }
    }
}
