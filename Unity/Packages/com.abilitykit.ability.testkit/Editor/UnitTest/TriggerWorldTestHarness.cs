using System;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.UnitTest
{
    public sealed class TriggerWorldTestHarness : IDisposable
    {
        private readonly EntitasWorld _world;

        public TriggerWorldTestHarness(WorldId id, string worldType)
        {
            var builder = WorldServiceContainerFactory.CreateDefaultOnly();

            var options = new WorldCreateOptions(id, worldType)
            {
                ServiceBuilder = builder
            };

            options.Modules.Add(new TriggeringWorldModule());

            _world = new EntitasWorld(options);
            _world.Initialize();
        }

        public IEntitasWorld World => _world;

        public TriggerRunner TriggerRunner => _world.Services.Resolve<TriggerRunner>();

        public ITriggerActionRunner ActionRunner => _world.Services.Resolve<ITriggerActionRunner>();

        public void Tick(float deltaTime)
        {
            _world.Tick(deltaTime);
        }

        public void Dispose()
        {
            _world.Dispose();
        }
    }
}
