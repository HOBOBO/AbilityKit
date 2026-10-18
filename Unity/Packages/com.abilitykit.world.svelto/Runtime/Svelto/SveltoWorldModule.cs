using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using Svelto.ECS;
using Svelto.ECS.Schedulers;

namespace AbilityKit.World.Svelto
{
    public sealed class SveltoWorldModule : IWorldModule, IWorldModuleInfo
    {
        public string Id => "abilitykit.world.svelto";
        public int Order => -1000;
        public Type[] DependsOn => Array.Empty<Type>();
        public Type[] ConflictsWith => Array.Empty<Type>();

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<SveltoWorldContext>(WorldLifetime.Singleton, _ => new SveltoWorldContext());
            builder.TryRegisterAlias(typeof(ISveltoWorldContext), typeof(SveltoWorldContext), WorldLifetime.Singleton);
            builder.TryRegisterNonOwning<EnginesRoot>(WorldLifetime.Singleton, r => r.Resolve<SveltoWorldContext>().EnginesRoot);
            builder.TryRegisterNonOwning<EntitiesSubmissionScheduler>(WorldLifetime.Singleton, r => r.Resolve<SveltoWorldContext>().Scheduler);
            builder.TryRegisterNonOwning<EntitiesDB>(WorldLifetime.Singleton, r => r.Resolve<SveltoWorldContext>().EntitiesDB);
            builder.TryRegisterNonOwning<IEntityFactory>(WorldLifetime.Singleton, r => r.Resolve<SveltoWorldContext>().EntityFactory);
            builder.TryRegisterNonOwning<IEntityFunctions>(WorldLifetime.Singleton, r => r.Resolve<SveltoWorldContext>().EntityFunctions);
        }
    }
}
