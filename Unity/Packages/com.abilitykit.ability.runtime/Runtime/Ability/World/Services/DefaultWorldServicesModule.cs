using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Effect;
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
                // 尝试从 Unity 集成包获取日志处理器（反射调用，保持通用层无直接依赖）
                var type = Type.GetType("AbilityKit.Ability.Unity.UnityLogSink, AbilityKit.Ability.Unity");
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
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }

                Log.SetSink(NullLogSink.Instance);
                return NullLogSink.Instance;
            });
            builder.TryRegisterType<IWorldClock, WorldClock>(WorldLifetime.Scoped);
            builder.TryRegisterType<IFrameTime, FrameTime>(WorldLifetime.Scoped);
            builder.TryRegisterType<IWorldRandom, DefaultWorldRandom>(WorldLifetime.Scoped);
            builder.TryRegisterType<IEffectTriggeringSwitch, DefaultEffectTriggeringSwitch>(WorldLifetime.Singleton);
            builder.TryRegisterType<IEventBus, EventBus>(WorldLifetime.Singleton);
            builder.TryRegisterType<ITriggerActionRunner, TriggerActionRunner>(WorldLifetime.Scoped);
        }
    }
}
