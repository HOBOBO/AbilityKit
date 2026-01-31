using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Examples
{
    public static class LogicWorldServerExample
    {
        private sealed class DebugInputSink : IWorldInputSink
        {
            private int _lastCount;

            public void Submit(FrameIndex frame, System.Collections.Generic.IReadOnlyList<PlayerInputCommand> inputs)
            {
                _lastCount = inputs == null ? 0 : inputs.Count;
            }

            public void Dispose()
            {

            }

            public int LastCount => _lastCount;
        }

        private sealed class DebugSnapshotProvider : IWorldStateSnapshotProvider
        {
            private readonly DebugInputSink _sink;

            public DebugSnapshotProvider(DebugInputSink sink)
            {
                _sink = sink;
            }

            public void Dispose()
            {

            }

            public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
            {
                snapshot = new WorldStateSnapshot(1, BitConverter.GetBytes(_sink.LastCount));
                return true;
            }
        }

        private sealed class DebugServerWorldModule : IWorldModule
        {
            public void Configure(WorldContainerBuilder builder)
            {
                builder.RegisterType<IWorldInputSink, DebugInputSink>(WorldLifetime.Scoped);
                builder.Register<IWorldStateSnapshotProvider>(WorldLifetime.Scoped, r => new DebugSnapshotProvider(r.Resolve<IWorldInputSink>() as DebugInputSink));
            }
        }

        public static HostRuntime CreateServerWithOneWorld()
        {
            var registry = new WorldTypeRegistry().RegisterEntitasWorld("battle");
            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var options = new HostRuntimeOptions();
            var server = new HostRuntime(manager, options);

            var modules = new HostRuntimeModuleHost();
            modules.Add(new FrameSyncDriverModule());
            modules.InstallAll(server, options);

            var builder = WorldServiceContainerFactory.CreateDefaultOnly();
            builder.AddModule(new AbilityKit.Ability.World.Entitas.Systems.TickCounterWorldModule());
            builder.AddModule(new DebugServerWorldModule());

            server.CreateWorld(new WorldCreateOptions(new WorldId("room_1"), "battle")
            {
                ServiceBuilder = builder
            });

            if (server.Features.TryGetFeature<IFrameSyncInputHub>(out var hub) && hub != null)
            {
                hub.SubmitInput(new ServerClientId("client_1"), new WorldId("room_1"), new PlayerInputCommand(new FrameIndex(0), new PlayerId("p1"), 1, new byte[] { 1, 2, 3 }));
            }

            server.Tick(0.016f);
            return server;
        }
    }
}
