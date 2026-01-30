using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas.Systems
{
    public sealed class TickCounterWorldModule : IWorldModule, IEntitasSystemsInstaller
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Register<WorldTickCounter>(WorldLifetime.Scoped, _ => new WorldTickCounter());
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var counter = services.Resolve<WorldTickCounter>();
            var ctx = services.Resolve<IWorldContext>();
            systems.Add(new TickCounterSystem(ctx, counter));
        }
    }
}
