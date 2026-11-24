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
    private const float FixedDelta = 1f / 30f;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachedWorld_IsNotRestoredOrCaptured_AndReplacementStartsWithoutOldHistory(bool disposeAll)
    {
        using var scope = new RuntimeScope();
        var worldId = new WorldId("server-rollback-replacement");
        var original = scope.Create(worldId);
        scope.Runtime.Tick(FixedDelta);
        var firstFrame = scope.FrameSync.Frame;
        Assert.Equal(1, original.ExportCount);

        if (disposeAll)
            scope.Worlds.DisposeAll();
        else
        {
            original.ThrowOnDispose = true;
            Assert.Throws<InvalidOperationException>(() => scope.Runtime.DestroyWorld(worldId));
        }

        var healthy = scope.Create(new WorldId("server-rollback-healthy"));
        scope.Runtime.Tick(FixedDelta);
        Assert.Equal(1, healthy.ExportCount);
        Assert.Equal(1, original.ExportCount);
        Assert.False(scope.Rollback.TryRollbackAndReplay(worldId, firstFrame, scope.FrameSync.Frame, FixedDelta));
        Assert.Equal(0, original.ImportCount);
        Assert.Equal(0, original.InputSink.BeginReplayCount);
        Assert.Equal(1, original.TickCount);

        var replacement = scope.Create(worldId);
        Assert.False(scope.Rollback.TryRollbackAndReplay(worldId, firstFrame, firstFrame, FixedDelta));
        scope.Runtime.Tick(FixedDelta);
        var baseline = scope.FrameSync.Frame;
        scope.Runtime.Tick(FixedDelta);
        Assert.True(scope.Rollback.TryRollbackAndReplay(worldId, baseline, scope.FrameSync.Frame, FixedDelta));
        Assert.Equal(1, replacement.ImportCount);
        Assert.Equal(1, replacement.InputSink.EndReplayCount);
        Assert.Equal(1, original.ExportCount);
    }

    [Fact]
    public void ReplacedWorld_WithoutHostNotification_CannotUsePreviousInstanceHistory()
    {
        using var scope = new RuntimeScope();
        var worldId = new WorldId("server-rollback-direct-replacement");
        var original = scope.Create(worldId);
        scope.Runtime.Tick(FixedDelta);
        var baseline = scope.FrameSync.Frame;
        Assert.True(scope.Worlds.Destroy(worldId));
        var replacement = Assert.IsType<TestWorld>(scope.Worlds.Create(new WorldCreateOptions(worldId, "test")));

        Assert.False(scope.Rollback.TryRollbackAndReplay(worldId, baseline, baseline, FixedDelta));
        Assert.Equal(0, original.ImportCount);
        Assert.Equal(0, replacement.ImportCount);
    }

    [Fact]
    public void Uninstall_DropsLiveWorldHistory_AndReinstallOnlyTracksNewWorlds()
    {
        using var scope = new RuntimeScope();
        var worldId = new WorldId("server-rollback-uninstall");
        var original = scope.Create(worldId);
        scope.Runtime.Tick(FixedDelta);
        var baseline = scope.FrameSync.Frame;

        scope.Rollback.Uninstall(scope.Runtime, scope.Options);
        Assert.False(scope.Rollback.TryRollbackAndReplay(worldId, baseline, baseline, FixedDelta));
        scope.Runtime.Tick(FixedDelta);
        Assert.Equal(1, original.ExportCount);
        Assert.Equal(0, original.ImportCount);

        scope.Rollback.Install(scope.Runtime, scope.Options);
        Assert.False(scope.Rollback.TryRollbackAndReplay(worldId, baseline, baseline, FixedDelta));
        var nextId = new WorldId("server-rollback-next-installation");
        var next = scope.Create(nextId);
        scope.Runtime.Tick(FixedDelta);
        Assert.Equal(1, original.ExportCount);
        Assert.Equal(1, next.ExportCount);
        Assert.True(scope.Rollback.TryRollbackAndReplay(nextId, scope.FrameSync.Frame, scope.FrameSync.Frame, FixedDelta));
    }

    [Fact]
    public void Replay_failure_is_propagated_and_always_ends_the_replay_lifecycle()
    {
        using var scope = new RuntimeScope();
        var worldId = new WorldId("server-rollback-replay-failure");
        var world = scope.Create(worldId);

        scope.Runtime.Tick(FixedDelta);
        Assert.True(scope.Runtime.Features.TryGetFeature<IFrameSyncInputHub>(out var inputs));
        Assert.True(inputs.SubmitInput(
            new ServerClientId("client"),
            worldId,
            new PlayerInputCommand(new FrameIndex(2), new PlayerId("player"), 1001, new byte[] { 1 })));
        scope.Runtime.Tick(FixedDelta);

        world.InputSink.ThrowDuringReplay = true;
        var error = Assert.Throws<InvalidOperationException>(() =>
            scope.Rollback.TryRollbackAndReplay(worldId, new FrameIndex(1), new FrameIndex(2), FixedDelta));

        Assert.Contains("replay failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, world.InputSink.BeginReplayCount);
        Assert.Equal(1, world.InputSink.EndReplayCount);
        Assert.Equal(1, world.TickCount);
    }

    private sealed class RuntimeScope : IDisposable
    {
        public RuntimeScope()
        {
            Runtime = new HostRuntime(Worlds, Options);
            Rollback = new ServerRollbackModule(8, 1, world =>
            {
                var registry = new RollbackRegistry();
                registry.Register(new TestStateProvider((TestWorld)world));
                return registry;
            });
            FrameSync.Install(Runtime, Options);
            Rollback.Install(Runtime, Options);
        }

        public WorldManager Worlds { get; } = new(new TestWorldFactory());
        public HostRuntimeOptions Options { get; } = new();
        public HostRuntime Runtime { get; }
        public FrameSyncDriverModule FrameSync { get; } = new();
        public ServerRollbackModule Rollback { get; }

        public TestWorld Create(WorldId id) => Assert.IsType<TestWorld>(Runtime.CreateWorld(new WorldCreateOptions(id, "test")));

        public void Dispose()
        {
            Rollback.Uninstall(Runtime, Options);
            FrameSync.Uninstall(Runtime, Options);
            Worlds.DisposeAll();
        }
    }

    private sealed class TestWorldFactory : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => new TestWorld(options.Id, options.WorldType);
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
        public int ExportCount { get; set; }
        public int ImportCount { get; set; }
        public bool ThrowOnDispose { get; set; }

        public void Initialize() { }
        public void Tick(float deltaTime) => TickCount++;
        public void Dispose()
        {
            if (ThrowOnDispose) throw new InvalidOperationException("release failed");
        }
    }

    private sealed class TestStateProvider : IRollbackStateProvider
    {
        private readonly TestWorld _world;

        public TestStateProvider(TestWorld world) => _world = world;
        public int Key => 1;
        public byte[] Export(FrameIndex frame)
        {
            _world.ExportCount++;
            return BitConverter.GetBytes(_world.TickCount);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            _world.ImportCount++;
            _world.TickCount = BitConverter.ToInt32(payload, 0);
        }
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
