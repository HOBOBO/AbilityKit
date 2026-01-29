using System;

namespace AbilityKit.Ability.World.DI
{
    public static class WorldServicesExtensions
    {
        public static IServiceProvider AsServiceProvider(this IWorldServices services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            return new WorldServiceProviderAdapter(services);
        }
    }
}
