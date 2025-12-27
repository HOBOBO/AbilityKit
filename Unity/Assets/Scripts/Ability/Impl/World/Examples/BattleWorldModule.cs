using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.World.Examples
{
    public sealed class BattleWorldModule : IWorldModule, IEntitasSystemsInstaller
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Register<WorldTickCounter>(WorldLifetime.Scoped, _ => new WorldTickCounter());
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var counter = services.Resolve<WorldTickCounter>();
            var ctx = services.Resolve<IWorldContext>();
            systems.Add(new TickCounterSystem(ctx, counter));
        }
    }

    public sealed class WorldTickCounter
    {
        public int TickCount;
    }

    internal sealed class TickCounterSystem : global::Entitas.IExecuteSystem
    {
        private readonly IWorldContext _ctx;
        private readonly WorldTickCounter _counter;
        private IWorldLogger _logger;

        public TickCounterSystem(IWorldContext ctx, WorldTickCounter counter)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        public void Execute()
        {
            _counter.TickCount++;

            if (_logger == null)
            {
                _logger = _ctx.Services.Get<IWorldLogger>();
            }

            if (_counter.TickCount == 1)
            {
                _logger.Info("World tick started");
            }
        }
    }
}
