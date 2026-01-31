using System;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Examples
{
    public static class LogicWorldServerExample
    {
        public const string ExampleWorldType = "example.framesync";

        public static HostRuntime CreateFrameSyncServerWithOneWorld(WorldId worldId)
        {
            var registry = new WorldTypeRegistry();
            registry.Register(ExampleWorldType, MinimalWorldFactory.CreateWorld);
            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var options = new HostRuntimeOptions();
            var server = new HostRuntime(manager, options);

            var modules = new HostRuntimeModuleHost();
            modules.Add(new FrameSyncDriverModule());
            modules.Add(new ServerFrameTimeModule());
            modules.InstallAll(server, options);

            var blueprints = new WorldBlueprintRegistry();
            blueprints.Register(new DelegateWorldBlueprint(ExampleWorldType, ConfigureDefaultWorld));
            blueprints.Configure(new WorldCreateOptions(worldId, ExampleWorldType));

            var builder = WorldServiceContainerFactory.CreateDefaultOnly();
            server.CreateWorld(new WorldCreateOptions(worldId, ExampleWorldType)
            {
                ServiceBuilder = builder
            });

            return server;
        }

        public static (HostRuntime server, ServerRollbackModule rollback) CreateFrameSyncRollbackServerWithOneWorld(WorldId worldId, int historyFrames, int captureEveryNFrames)
        {
            var registry = new WorldTypeRegistry();
            registry.Register(ExampleWorldType, MinimalWorldFactory.CreateWorld);
            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var options = new HostRuntimeOptions();
            var rollback = new ServerRollbackModule(historyFrames, captureEveryNFrames, _ => new RollbackRegistry());

            var server = new HostRuntime(manager, options);

            var modules = new HostRuntimeModuleHost();
            modules.Add(new FrameSyncDriverModule());
            modules.Add(new ServerFrameTimeModule());
            modules.Add(rollback);
            modules.InstallAll(server, options);

            var builder = WorldServiceContainerFactory.CreateDefaultOnly();
            server.CreateWorld(new WorldCreateOptions(worldId, ExampleWorldType)
            {
                ServiceBuilder = builder
            });

            return (server, rollback);
        }

        private static void ConfigureDefaultWorld(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.ServiceBuilder == null)
            {
                options.ServiceBuilder = WorldServiceContainerFactory.CreateDefaultOnly();
            }
        }

        private sealed class MinimalWorld : IWorld
        {
            private readonly WorldContainer _container;
            private WorldScope _scope;

            public MinimalWorld(WorldId id, string worldType, WorldContainer container)
            {
                Id = id;
                WorldType = worldType;
                _container = container;
            }

            public WorldId Id { get; }

            public string WorldType { get; }

            public IWorldResolver Services => _scope ?? (IWorldResolver)_container;

            public void Initialize()
            {
                _scope = _container.CreateScope();
            }

            public void Tick(float deltaTime)
            {
            }

            public void Dispose()
            {
                try
                {
                    _scope?.Dispose();
                }
                catch
                {
                }

                try
                {
                    _container?.Dispose();
                }
                catch
                {
                }
            }
        }

        private static class MinimalWorldFactory
        {
            public static IWorld CreateWorld(WorldCreateOptions options)
            {
                if (options == null) throw new ArgumentNullException(nameof(options));
                if (string.IsNullOrEmpty(options.Id.Value)) throw new ArgumentException("WorldId is required", nameof(options));
                if (string.IsNullOrEmpty(options.WorldType)) throw new ArgumentException("WorldType is required", nameof(options));

                var blueprints = new WorldBlueprintRegistry();
                blueprints.Register(new DelegateWorldBlueprint(ExampleWorldType, ConfigureDefaultWorld));
                blueprints.Configure(options);

                if (options.ServiceBuilder == null)
                {
                    options.ServiceBuilder = WorldServiceContainerFactory.CreateDefaultOnly();
                }

                if (options.Modules != null)
                {
                    for (int i = 0; i < options.Modules.Count; i++)
                    {
                        var m = options.Modules[i];
                        if (m == null) continue;
                        options.ServiceBuilder.AddModule(m);
                    }
                }

                var container = options.ServiceBuilder.Build();
                return new MinimalWorld(options.Id, options.WorldType, container);
            }
        }
    }
}
