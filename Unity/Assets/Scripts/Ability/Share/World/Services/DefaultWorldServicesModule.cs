using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultWorldServicesModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegisterType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);
            builder.TryRegisterType<IWorldClock, WorldClock>(WorldLifetime.Scoped);
            builder.TryRegisterType<IWorldRandom, DefaultWorldRandom>(WorldLifetime.Scoped);
            builder.TryRegisterType<IEffectTriggeringSwitch, DefaultEffectTriggeringSwitch>(WorldLifetime.Singleton);
            builder.TryRegisterType<IEventBus, EventBus>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITriggerActionRunner, TriggerActionRunner>(WorldLifetime.Scoped);
        }
    }
}
