using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class WorldAutoStartModuleTests
{
    [Fact]
    public void PostTick_RetriesUntilSuccess_AndForgetsDestroyedWorld()
    {
        var manager = new WorldManager(new TestWorldFactory());
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(manager, options);
        var module = new WorldAutoStartModule();
        module.Install(runtime, options);
        var worldId = new WorldId("auto-start");

        try
        {
            var first = new StartProbe();
            var firstWorld = runtime.CreateWorld(CreateOptions(worldId, first));
            runtime.Tick(0.125f);
            Assert.Equal(1, first.Attempts);
            first.Ready = true;
            runtime.Tick(0.125f);
            runtime.Tick(0.125f);
            Assert.Equal(2, first.Attempts);
            Assert.Same(firstWorld, first.LastWorld);
            Assert.True(runtime.DestroyWorld(worldId));

            var replacement = new StartProbe();
            runtime.CreateWorld(CreateOptions(worldId, replacement));
            runtime.Tick(0.125f);
            Assert.Equal(1, replacement.Attempts);
            module.Uninstall(runtime, options);
            runtime.Tick(0.125f);
            Assert.Equal(1, replacement.Attempts);
        }
        finally
        {
            manager.DisposeAll();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completion_DoesNotCarryToReplacementWithoutDestroyNotification(bool disposeAll)
    {
        var manager = new WorldManager(new TestWorldFactory());
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(manager, options);
        var module = new WorldAutoStartModule();
        module.Install(runtime, options);
        var worldId = new WorldId("auto-start-replacement");

        try
        {
            var first = new StartProbe { Ready = true };
            var firstWorld = Assert.IsType<TestWorld>(runtime.CreateWorld(CreateOptions(worldId, first)));
            runtime.Tick(0.125f);

            if (disposeAll)
                manager.DisposeAll();
            else
            {
                firstWorld.ThrowOnDispose = true;
                Assert.Throws<InvalidOperationException>(() => runtime.DestroyWorld(worldId));
            }

            var replacement = new StartProbe { Ready = true };
            var replacementWorld = runtime.CreateWorld(CreateOptions(worldId, replacement));
            runtime.Tick(0.125f);
            runtime.Tick(0.125f);
            Assert.Equal(1, first.Attempts);
            Assert.Equal(1, replacement.Attempts);
            Assert.Same(replacementWorld, replacement.LastWorld);
        }
        finally
        {
            module.Uninstall(runtime, options);
            manager.DisposeAll();
        }
    }

    private static WorldCreateOptions CreateOptions(WorldId id, StartProbe probe)
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterExternalInstance<IWorldAutoStartHandler>(probe);
        return new WorldCreateOptions(id, "test") { ServiceBuilder = builder };
    }

    private sealed class StartProbe : IWorldAutoStartHandler
    {
        public bool Ready { get; set; }
        public int Attempts { get; private set; }
        public IWorld? LastWorld { get; private set; }

        public bool TryAutoStart(IWorld world, float deltaTime)
        {
            LastWorld = world;
            Attempts++;
            return Ready;
        }

        public void Dispose() { }
    }

    private sealed class TestWorldFactory : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => new TestWorld(options.Id, options.ServiceBuilder!);
    }

    private sealed class TestWorld(WorldId id, WorldContainerBuilder builder) : IWorld
    {
        private WorldContainer? _container;
        public WorldId Id => id;
        public string WorldType => "test";
        public IWorldResolver Services => _container!;
        public bool ThrowOnDispose { get; set; }
        public void Initialize() => _container = builder.Build();
        public void Tick(float deltaTime) { }
        public void Dispose()
        {
            _container?.Dispose();
            if (ThrowOnDispose) throw new InvalidOperationException("release failed");
        }
    }
}
