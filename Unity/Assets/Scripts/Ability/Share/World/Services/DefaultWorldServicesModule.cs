using System;
using AbilityKit.Ability.Share.Common.Log;
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
            builder.TryRegister<ILogSink>(WorldLifetime.Singleton, _ =>
            {
                var type = Type.GetType("AbilityKit.Ability.Impl.Common.Log.UnityLogSink, AbilityKit.Ability.Unity");
                if (type != null && typeof(ILogSink).IsAssignableFrom(type))
                {
                    try
                    {
                        var created = Activator.CreateInstance(type) as ILogSink;
                        if (created != null)
                        {
                            Log.SetSink(created);
                            return created;
                        }
                    }
                    catch
                    {
                    }
                }

                Log.SetSink(NullLogSink.Instance);
                return NullLogSink.Instance;
            });
            builder.TryRegisterType<IWorldClock, WorldClock>(WorldLifetime.Scoped);
            builder.TryRegisterType<IWorldRandom, DefaultWorldRandom>(WorldLifetime.Scoped);
            builder.TryRegisterType<IEffectTriggeringSwitch, DefaultEffectTriggeringSwitch>(WorldLifetime.Singleton);
            builder.TryRegisterType<IEventBus, EventBus>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITriggerActionRunner, TriggerActionRunner>(WorldLifetime.Scoped);
        }
    }
}
