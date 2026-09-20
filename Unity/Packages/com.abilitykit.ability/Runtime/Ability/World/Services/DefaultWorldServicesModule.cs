using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.DI;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultWorldServicesModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegisterType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);
            builder.TryRegister<ILogSink>(WorldLifetime.Singleton, _ =>
            {
                Log.SetSink(NullLogSink.Instance);
                return NullLogSink.Instance;
            });
            builder.TryRegisterType<IWorldClock, WorldClock>(WorldLifetime.Scoped);
            builder.TryRegisterType<IFrameTime, FrameTime>(WorldLifetime.Scoped);
            builder.TryRegisterType<IWorldRandom, DefaultWorldRandom>(WorldLifetime.Scoped);
            builder.TryRegisterType<IEffectTriggeringSwitch, DefaultEffectTriggeringSwitch>(WorldLifetime.Singleton);
            builder.TryRegisterType<IEventBus, EventBus>(WorldLifetime.Singleton);
        }
    }
}
