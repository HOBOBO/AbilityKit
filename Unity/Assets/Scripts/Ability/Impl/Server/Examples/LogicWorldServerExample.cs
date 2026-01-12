using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Server.Examples
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
                builder.Register<IWorldStateSnapshotProvider>(WorldLifetime.Scoped, r => new DebugSnapshotProvider(r.Get<IWorldInputSink>() as DebugInputSink));
            }
        }

        private sealed class LogClient : ILogicServerClient
        {
            public LogClient(string id)
            {
                ClientId = new ServerClientId(id);
            }

            public ServerClientId ClientId { get; }

            public void OnWorldCreated(WorldId worldId, string worldType)
            {
            }

            public void OnWorldDestroyed(WorldId worldId)
            {
            }

            public void OnPlayerJoined(WorldId worldId, PlayerId player)
            {
            }

            public void OnPlayerLeft(WorldId worldId, PlayerId player)
            {
            }

            public void OnFrame(FramePacket packet)
            {
            }
        }

        public static LogicWorldServer CreateServerWithOneWorld()
        {
            var registry = new WorldTypeRegistry().RegisterEntitasWorld("battle");
            var manager = new WorldManager(new RegistryWorldFactory(registry));
            var server = new LogicWorldServer(manager);

            server.Connect(new LogClient("client_1"));

            var builder = WorldServiceContainerFactory.CreateDefaultOnly();
            builder.AddModule(new AbilityKit.Ability.World.Entitas.Systems.TickCounterWorldModule());
            builder.AddModule(new DebugServerWorldModule());

            server.CreateWorld(new WorldCreateOptions(new WorldId("room_1"), "battle")
            {
                ServiceBuilder = builder
            });

            server.JoinWorld(new ServerClientId("client_1"), new WorldId("room_1"), new PlayerId("p1"));
            server.SubmitInput(new ServerClientId("client_1"), new WorldId("room_1"), new PlayerInputCommand(new FrameIndex(0), new PlayerId("p1"), 1, new byte[] { 1, 2, 3 }));

            server.Tick(0.016f);
            return server;
        }
    }
}
