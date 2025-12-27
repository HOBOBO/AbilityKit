using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultWorldServicesModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegisterType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);
            builder.TryRegisterType<IWorldClock, WorldClock>(WorldLifetime.Scoped);
            builder.TryRegisterType<IWorldRandom, DefaultWorldRandom>(WorldLifetime.Scoped);
        }
    }
}
