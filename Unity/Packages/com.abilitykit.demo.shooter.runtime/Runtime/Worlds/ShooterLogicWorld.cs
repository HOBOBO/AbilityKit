using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Diagnostics;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterLogicWorld : IWorld
    {
        private readonly WorldContainer _container;

        public ShooterLogicWorld(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            Id = options.Id;
            WorldType = string.IsNullOrEmpty(options.WorldType) ? ShooterGameplay.WorldType : options.WorldType;

            var builder = options.ServiceBuilder ?? new WorldContainerBuilder();
            var modulePlan = WorldModulePlanner.Create(
                options.Modules,
                $"World[{Id.Value}/{WorldType}]");

            builder.RegisterInstance<WorldId>(Id);
            builder.RegisterInstance<string>(WorldType);
            builder.RegisterExternalInstance<IWorld>(this);

            for (var i = 0; i < modulePlan.Entries.Count; i++)
            {
                builder.AddModule(modulePlan.Entries[i].Module);
            }

            _container = builder.Build();

            var report = WorldCompositionReportBuilder.Create(
                Id.Value,
                WorldType,
                modulePlan,
                builder);
            foreach (var serviceType in _container.RegisteredServiceTypes)
            {
                report.AddRegisteredService(serviceType.FullName);
            }

            WorldDebugRegistry.Report(report);
        }

        public WorldId Id { get; }

        public string WorldType { get; }

        public IWorldResolver Services => _container;

        public void Initialize()
        {
        }

        public void Tick(float deltaTime)
        {
            if (_container.TryResolve<IShooterBattleRuntimePort>(out var runtime) && runtime.IsStarted)
            {
                runtime.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}
