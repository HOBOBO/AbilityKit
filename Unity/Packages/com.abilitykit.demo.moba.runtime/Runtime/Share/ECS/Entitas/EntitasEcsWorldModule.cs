using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasEcsWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<EntitasActorIdLookup>(WorldLifetime.Scoped, s =>
            {
                var contexts = s.Get<global::Entitas.IContexts>() as global::Contexts;
                if (contexts == null) throw new InvalidOperationException("[EntitasEcsWorldModule] Expected Entitas IContexts to be generated Contexts instance.");
                return new EntitasActorIdLookup(contexts.actor);
            });

            builder.TryRegisterType<IUnitResolver, EntitasUnitResolver>(WorldLifetime.Scoped);

            builder.TryRegister<IEcsWorld>(WorldLifetime.Scoped, s =>
            {
                var lookup = s.Get<EntitasActorIdLookup>();
                var units = s.Get<IUnitResolver>();
                return new EntitasEcsWorld(s, lookup, units);
            });
        }
    }
}
