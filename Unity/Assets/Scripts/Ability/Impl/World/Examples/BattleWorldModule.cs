using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Impl.World.Examples
{
    public sealed class BattleWorldModule : IWorldModule, IEntitasSystemsInstaller
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Register<WorldTickCounter>(WorldLifetime.Scoped, _ => new WorldTickCounter());
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldResolver resolver)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var counter = resolver.Resolve<WorldTickCounter>();
            systems.Add(new TickCounterSystem(counter));
        }
    }

    public sealed class WorldTickCounter
    {
        public int TickCount;
    }

    internal sealed class TickCounterSystem : global::Entitas.IExecuteSystem
    {
        private readonly WorldTickCounter _counter;

        public TickCounterSystem(WorldTickCounter counter)
        {
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        public void Execute()
        {
            _counter.TickCount++;
        }
    }
}
