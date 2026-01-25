using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class ProjectileWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegisterType<IProjectileService, ProjectileService>(WorldLifetime.Scoped);
        }
    }

    public sealed class ProjectileSystemsModule : IWorldModule, IEntitasSystemsInstaller
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddModule(new ProjectileWorldModule());
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Ensure ProjectileTickSystem is present even if AutoSystemInstaller is not used.
            systems.Add(new ProjectileTickSystem(contexts, services));
        }
    }
}
