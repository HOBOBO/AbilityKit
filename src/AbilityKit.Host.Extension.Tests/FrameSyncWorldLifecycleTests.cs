using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class FrameSyncWorldLifecycleTests
{
    private const float FixedDelta = 1f / 30f;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prediction_DetachedWorldStopsAndReplacementStartsFresh(bool disposeAll)
    {
        using var scope = new RuntimeScope();
        var driver = InstallPrediction(scope);
        var worldId = new WorldId("prediction");
        var original = scope.Create(worldId);
        scope.Runtime.Tick(FixedDelta);
        Assert.True(driver.TryGetFrames(worldId, out _, out var last));
        Assert.Equal(1, last.Value);
        Assert.True(driver.TryCommitVerifiedRestoreAnchor(worldId, last));
        var inputCount = original.Inputs.Count;
        var captures = original.Captures;

        scope.Detach(original, disposeAll);
        var healthy = scope.Create(new WorldId("healthy"));
        scope.Runtime.Tick(FixedDelta);
        Assert.Single(healthy.Inputs);
        Assert.Equal(inputCount, original.Inputs.Count);
        Assert.Equal(captures, original.Captures);
        Assert.False(driver.TryGetFrames(worldId, out _, out _));
        Assert.False(driver.TryGetRestoreFloor(worldId, out _));

        var replacement = scope.Create(worldId);
        Assert.True(driver.TryGetFrames(worldId, out _, out var initial));
        Assert.Equal(0, initial.Value);
        Assert.False(driver.TryGetRestoreFloor(worldId, out _));
        scope.Runtime.Tick(FixedDelta);
        Assert.Single(replacement.Inputs);
        Assert.Equal(inputCount, original.Inputs.Count);
    }

    [Fact]
    public void Prediction_RebaseRejectsPreviousInstanceBeforeNextTick()
    {
        using var scope = new RuntimeScope();
        var driver = InstallPrediction(scope);
        var worldId = new WorldId("prediction-direct-replacement");
        var original = scope.Create(worldId);
        scope.Runtime.Tick(FixedDelta);
        var captures = original.Captures;

        Assert.True(scope.Worlds.Destroy(worldId));
        scope.Worlds.Create(new WorldCreateOptions(worldId, "test"));

        Assert.False(driver.TryGetFrames(worldId, out _, out _));
        Assert.False(driver.TryRebase(worldId, new FrameIndex(1)));
        Assert.Equal(captures, original.Captures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Authority_SameIdHostReplacementDoesNotInheritQueuedInputs(bool disposeAll)
    {
        using var scope = new RuntimeScope();
        var driver = scope.Install(new FrameSyncDriverModule());
        var worldId = new WorldId("authority");
        var original = scope.Create(worldId);
        Assert.True(driver.SubmitInput(scope.Connection.ClientId, worldId, Command(101)));

        scope.Detach(original, disposeAll);
        var replacement = scope.Create(worldId);
        Assert.True(driver.SubmitInput(scope.Connection.ClientId, worldId, Command(102)));
        scope.Runtime.Tick(FixedDelta);

        Assert.Empty(original.Inputs);
        Assert.Equal(102, Assert.Single(Assert.Single(replacement.Inputs)).OpCode);
        Assert.Equal(102, Assert.Single(Assert.Single(scope.Connection.Frames).Inputs).OpCode);
    }

    [Fact]
    public void Authority_DetachedBindingRejectsInputAndDoesNotPreventHeadlessSession()
    {
        using var scope = new RuntimeScope();
        var driver = scope.Install(new FrameSyncDriverModule());
        var boundId = new WorldId("old-binding");
        var headlessId = new WorldId("explicit-headless");
        var original = scope.Create(boundId);
        Assert.True(driver.SubmitInput(scope.Connection.ClientId, boundId, Command(101)));
        driver.RegisterSession(headlessId);
        Assert.True(driver.SubmitInput(scope.Connection.ClientId, headlessId, Command(102)));

        Assert.True(scope.Worlds.Destroy(boundId));
        Assert.False(driver.SubmitInput(scope.Connection.ClientId, boundId, Command(103)));
        var directReplacement = Assert.IsType<TestWorld>(scope.Worlds.Create(new WorldCreateOptions(boundId, "test")));
        Assert.False(driver.SubmitInput(scope.Connection.ClientId, boundId, Command(104)));
        scope.Runtime.Tick(FixedDelta);

        Assert.Empty(original.Inputs);
        Assert.Empty(directReplacement.Inputs);
        var packet = Assert.Single(scope.Connection.Frames);
        Assert.Equal(headlessId, packet.WorldId);
        Assert.Equal(102, Assert.Single(packet.Inputs).OpCode);
    }

    [Fact]
    public void Authority_FirstHostBindingKeepsExplicitHeadlessInputs()
    {
        using var scope = new RuntimeScope();
        var driver = scope.Install(new FrameSyncDriverModule());
        var worldId = new WorldId("headless-first-binding");
        driver.RegisterSession(worldId);
        Assert.True(driver.SubmitInput(scope.Connection.ClientId, worldId, Command(101)));
        var world = scope.Create(worldId);

        scope.Runtime.Tick(FixedDelta);

        Assert.Equal(101, Assert.Single(Assert.Single(world.Inputs)).OpCode);
        Assert.Equal(101, Assert.Single(Assert.Single(scope.Connection.Frames).Inputs).OpCode);
    }

    private static ClientPredictionDriverModule InstallPrediction(RuntimeScope scope)
    {
        return scope.Install(new ClientPredictionDriverModule(_ => null!, _ => null!,
            maxPredictionAheadFrames: 8, minPredictionWindow: 8, enableRollback: true,
            buildRollbackRegistry: world =>
            {
                var registry = new RollbackRegistry();
                registry.Register(new StateProbe((TestWorld)world));
                return registry;
            }));
    }

    private static PlayerInputCommand Command(int opcode) =>
        new(new FrameIndex(1), new PlayerId("player"), opcode, new byte[] { 1 });

    private sealed class RuntimeScope : IDisposable
    {
        private readonly HostRuntimeOptions options = new();
        private IHostRuntimeModule? installed;
        public WorldManager Worlds { get; } = new(new TestWorldFactory());
        public HostRuntime Runtime { get; }
        public RecordingConnection Connection { get; } = new();

        public RuntimeScope()
        {
            Runtime = new HostRuntime(Worlds, options);
            Runtime.Connect(Connection);
        }

        public T Install<T>(T module) where T : IHostRuntimeModule
        {
            module.Install(Runtime, options);
            installed = module;
            return module;
        }

        public TestWorld Create(WorldId id) => Assert.IsType<TestWorld>(Runtime.CreateWorld(new WorldCreateOptions(id, "test")));

        public void Detach(TestWorld world, bool disposeAll)
        {
            if (disposeAll) Worlds.DisposeAll();
            else
            {
                world.FailRelease = true;
                Assert.Throws<InvalidOperationException>(() => Runtime.DestroyWorld(world.Id));
            }
        }

        public void Dispose()
        {
            installed?.Uninstall(Runtime, options);
            Worlds.DisposeAll();
        }
    }

    private sealed class TestWorldFactory : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => new TestWorld(options.Id);
    }

    private sealed class TestWorld(WorldId id) : IWorld, IWorldInputSink
    {
        private WorldContainer? services;
        public WorldId Id => id;
        public string WorldType => "test";
        public IWorldResolver Services => services!;
        public int Ticks { get; set; }
        public int Captures { get; set; }
        public bool FailRelease { get; set; }
        public List<PlayerInputCommand[]> Inputs { get; } = new();

        public void Initialize()
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterExternalInstance<IWorldInputSink>(this);
            services = builder.Build();
        }

        public void Tick(float deltaTime) => Ticks++;
        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => Inputs.Add(inputs.ToArray());
        public void Dispose()
        {
            services?.Dispose();
            if (FailRelease) throw new InvalidOperationException("release failed");
        }
    }

    private sealed class StateProbe(TestWorld world) : IRollbackStateProvider
    {
        public int Key => 1;

        public byte[] Export(FrameIndex frame)
        {
            world.Captures++;
            return BitConverter.GetBytes(world.Ticks);
        }

        public void Import(FrameIndex frame, byte[] payload) => world.Ticks = BitConverter.ToInt32(payload, 0);
    }

    private sealed class RecordingConnection : IServerConnection
    {
        public ServerClientId ClientId { get; } = new("client");
        public List<FramePacket> Frames { get; } = new();
        public void Send(ServerMessage message)
        {
            if (message is FrameMessage frame) Frames.Add(frame.Packet);
        }
    }
}
