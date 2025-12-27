using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.Entitas.Systems
{
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
