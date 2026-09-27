using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class ServerRollbackModuleTests
{
    [Fact]
    public void Replay_failure_is_propagated_and_always_ends_the_replay_lifecycle()
    {
        const float fixedDelta = 1f / 30f;
        var worlds = new TestWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var frameSync = new FrameSyncDriverModule();
        var rollback = new ServerRollbackModule(
            historyFrames: 8,
            captureEveryNFrames: 1,
            buildRegistry: world =>
            {
                var registry = new RollbackRegistry();
                registry.Register(new TestStateProvider((TestWorld)world));
                return registry;
            });

        frameSync.Install(runtime, options);
        rollback.Install(runtime, options);
        var worldId = new WorldId("server-rollback-replay-failure");
        var world = Assert.IsType<TestWorld>(runtime.CreateWorld(new WorldCreateOptions(worldId, "test")));

        runtime.Tick(fixedDelta);
        Assert.True(runtime.Features.TryGetFeature<IFrameSyncInputHub>(out var inputs));
        Assert.True(inputs.SubmitInput(
            new ServerClientId("client"),
            worldId,
            new PlayerInputCommand(new FrameIndex(2), new PlayerId("player"), 1001, new byte[] { 1 })));
        runtime.Tick(fixedDelta);

        world.InputSink.ThrowDuringReplay = true;
        var error = Assert.Throws<InvalidOperationException>(() =>
            rollback.TryRollbackAndReplay(worldId, new FrameIndex(1), new FrameIndex(2), fixedDelta));

        Assert.Contains("replay failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, world.InputSink.BeginReplayCount);
        Assert.Equal(1, world.InputSink.EndReplayCount);
        Assert.Equal(1, world.TickCount);
    }

    private sealed class TestWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> _worlds = new();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            var world = new TestWorld(options.Id, options.WorldType);
            _worlds.Add(world.Id, world);
            return world;
        }

        public bool TryGet(WorldId id, out IWorld world) => _worlds.TryGetValue(id, out world!);
        public bool Destroy(WorldId id) => _worlds.Remove(id);

        public void Tick(float deltaTime)
        {
            foreach (var world in _worlds.Values) world.Tick(deltaTime);
        }

        public void DisposeAll() => _worlds.Clear();
    }

    private sealed class TestWorld : IWorld
    {
        public TestWorld(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
            InputSink = new FailingReplayInputSink();
            Services = new TestWorldResolver(InputSink);
        }

        public WorldId Id { get; }
        public string WorldType { get; }
        public IWorldResolver Services { get; }
        public FailingReplayInputSink InputSink { get; }
        public int TickCount { get; set; }

        public void Initialize() { }
        public void Tick(float deltaTime) => TickCount++;
        public void Dispose() { }
    }

    private sealed class TestStateProvider : IRollbackStateProvider
    {
        private readonly TestWorld _world;

        public TestStateProvider(TestWorld world) => _world = world;
        public int Key => 1;
        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(_world.TickCount);
        public void Import(FrameIndex frame, byte[] payload) => _world.TickCount = BitConverter.ToInt32(payload, 0);
    }

    private sealed class FailingReplayInputSink : IWorldInputReplaySink
    {
        public bool ThrowDuringReplay { get; set; }
        public int BeginReplayCount { get; private set; }
        public int EndReplayCount { get; private set; }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) { }

        public void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame) => BeginReplayCount++;

        public void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (ThrowDuringReplay) throw new InvalidOperationException("replay failed");
        }

        public void EndReplay() => EndReplayCount++;
        public void Dispose() { }
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly IWorldInputSink _inputSink;

        public TestWorldResolver(IWorldInputSink inputSink) => _inputSink = inputSink;

        public object Resolve(Type serviceType)
        {
            if (TryResolve(serviceType, out var instance)) return instance;
            throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");
        }

        public T Resolve<T>()
        {
            if (TryResolve<T>(out var instance)) return instance;
            throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}");
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            if (serviceType == typeof(IWorldInputSink))
            {
                instance = _inputSink;
                return true;
            }

            instance = null!;
            return false;
        }

        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out var resolved))
            {
                instance = (T)resolved;
                return true;
            }

            instance = default!;
            return false;
        }
    }
}
