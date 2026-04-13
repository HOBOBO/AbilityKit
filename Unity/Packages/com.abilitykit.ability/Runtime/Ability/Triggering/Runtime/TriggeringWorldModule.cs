using System;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Triggering.Runtime.Builtins;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggeringWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<ITriggerContextFactory>(WorldLifetime.Scoped, services =>
            {
                var sp = new WorldServiceProviderAdapter(services);
                return new WorldTriggerContextFactory(sp);
            });

            builder.TryRegister<TriggerRegistry>(WorldLifetime.Scoped, _ =>
            {
                var registry = new TriggerRegistry();
                registry.AutoRegisterFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
                return registry;
            });

            builder.TryRegister<TriggerRunner>(WorldLifetime.Scoped, services =>
            {
                var bus = services.Resolve<IEventBus>();
                var registry = services.Resolve<TriggerRegistry>();
                var ctxFactory = services.Resolve<ITriggerContextFactory>();
                return new TriggerRunner(bus, registry, ctxFactory);
            });
        }

    }
}
