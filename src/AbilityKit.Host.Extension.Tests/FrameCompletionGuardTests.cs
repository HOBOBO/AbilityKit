using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class FrameCompletionGuardTests
{
    [Theory]
    [InlineData(0, 8, 8, 8, 6)]
    [InlineData(3, 8, 3, 2, 1)]
    [InlineData(0, 0, 0, 0, 0)]
    public void Prediction_window_preserves_smoothing_delay_hard_limit_and_ideal_cap(
        int ideal, int maximum, int firstWindow, int secondWindow, int thirdWindow)
    {
        var source = new MissingRemoteFrames { TargetFrame = 10 };
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(new GuardedWorldManager(), options);
        var prediction = new ClientPredictionDriverModule(_ => source, _ => null!, _ => ideal,
            inputDelayFrames: 2, maxPredictionAheadFrames: maximum, minPredictionWindow: 1,
            backlogEwmaAlpha: .5f);
        prediction.Install(runtime, options);
        var world = runtime.CreateWorld(new WorldCreateOptions(new WorldId("window-contract"), "test"));

        var targets = new[] { 10, 6, 0 };
        var rawBacklogs = new[] { 10, 5, 0 };
        var smoothed = new[] { 10f, 7.5f, 3.75f };
        var windows = new[] { firstWindow, secondWindow, thirdWindow };
        for (var i = 0; i < targets.Length; i++)
        {
            source.TargetFrame = targets[i];
            runtime.Tick(.033f);
            Assert.True(prediction.TryGetPredictionWindowStats(world.Id, out var raw, out var ewma, out var window, out _));
            Assert.Equal(rawBacklogs[i], raw);
            Assert.Equal(smoothed[i], ewma);
            Assert.Equal(windows[i], window);
            Assert.Equal(raw, prediction.CurrentBacklogRaw);
            Assert.Equal(ewma, prediction.CurrentBacklogEwma);
            Assert.Equal(window, prediction.CurrentPredictionWindow);
        }
    }

    private sealed class MissingRemoteFrames : AbilityKit.Network.Abstractions.IConsumableRemoteFrameSource<PlayerInputCommand[]>
    {
        public int DelayFrames { get; set; }
        public int TargetFrame { get; set; }
        public int MaxReceivedFrame => TargetFrame;
        public int LastConsumedFrame => 0;
        public bool TryGet(int frame, out PlayerInputCommand[] data) { data = null!; return false; }
        public bool TryConsume(int frame, out PlayerInputCommand[] data) => TryGet(frame, out data);
        public void TrimBefore(int minFrameInclusive) { }
        public void Dispose() { }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoWorlds_GuardedFailureDoesNotCommit_WhileHealthyWorldContinues(bool failingWorldFirst)
    {
        using var scenario = new DualWorldCommitScenario();
        var first = scenario.Create("first");
        var second = scenario.Create("second");
        var failed = failingWorldFirst ? first : second;
        var healthy = failingWorldFirst ? second : first;
        scenario.Tick();

        failed.Fail = true;
        Assert.True(scenario.Driver.SubmitInput(scenario.Connection.ClientId, failed.Id,
            new PlayerInputCommand(new FrameIndex(2), new PlayerId("player"), 101, new byte[] { 1 })));
        scenario.Tick();
        scenario.Tick();

        Assert.Equal(new[] { 1, 2, 3 }, scenario.HostSteps);
        Assert.Equal(3, scenario.Driver.Frame.Value);
        Assert.Equal(new[] { 1, 2 }, scenario.InputsFlushed.Where(entry => entry.World == failed.Id).Select(entry => entry.Frame));
        var failedInput = Assert.Single(scenario.InputsFlushed.Single(entry => entry.World == failed.Id && entry.Frame == 2).Inputs);
        Assert.Equal(101, failedInput.OpCode);
        Assert.Equal(new[] { 1, 2, 3 }, scenario.InputsFlushed.Where(entry => entry.World == healthy.Id).Select(entry => entry.Frame));

        Assert.Equal(new[] { 1 }, scenario.PacketFrames(failed));
        Assert.Equal(new[] { 1, 2, 3 }, scenario.PacketFrames(healthy));
        Assert.Equal(1, failed.SnapshotReads);
        Assert.Equal(3, healthy.SnapshotReads);
        Assert.Equal(new[] { (1, 1) }, scenario.Probes[failed.Id].CapturedFrames);
        Assert.Equal(new[] { (1, 1), (2, 2), (3, 3) }, scenario.Probes[healthy.Id].CapturedFrames);
        Assert.Equal((1, DualWorldCommitScenario.Delta), scenario.Clock(failed));
        Assert.Equal((3, 3 * DualWorldCommitScenario.Delta), scenario.Clock(healthy));
        Assert.False(scenario.TryRestoreAt(failed, 2));
    }

    [Fact]
    public void TwoWorlds_CharacterizesFailureOutputWhenWorldDoesNotExposeGuard()
    {
        using var scenario = new DualWorldCommitScenario();
        var failed = scenario.Create("unguarded");
        failed.ExposeGuard = false;
        var healthy = scenario.Create("healthy");
        scenario.Tick();

        failed.Fail = true;
        scenario.Tick();

        Assert.False(failed.Guard.CanCommit);
        Assert.Equal(new[] { 1, 2 }, scenario.HostSteps);
        Assert.Equal(new[] { 1, 2 }, scenario.PacketFrames(healthy));
        Assert.Equal(new[] { 1, 2 }, scenario.PacketFrames(failed));
        Assert.Equal(2, failed.SnapshotReads);
        Assert.Equal((2, 2 * DualWorldCommitScenario.Delta), scenario.Clock(failed));
        Assert.Equal(new[] { (1, 1), (2, 2) }, scenario.Probes[failed.Id].CapturedFrames);
    }

    [Fact]
    public void TwoWorlds_CharacterizesPublicationWhenRollbackCaptureFails()
    {
        using var scenario = new DualWorldCommitScenario();
        var captureFails = scenario.Create("capture-fails");
        var healthy = scenario.Create("healthy");
        scenario.Tick();

        scenario.Probes[captureFails.Id].RejectCapture = true;
        scenario.Tick();

        Assert.True(captureFails.Guard.CanCommit);
        Assert.Equal(new[] { 1, 2 }, scenario.HostSteps);
        Assert.Equal(new[] { 1, 2 }, scenario.PacketFrames(captureFails));
        Assert.Equal((2, 2 * DualWorldCommitScenario.Delta), scenario.Clock(captureFails));
        Assert.Equal(1, scenario.Probes[captureFails.Id].RejectedCaptures);
        Assert.Equal(new[] { (1, 1) }, scenario.Probes[captureFails.Id].CapturedFrames);
        Assert.False(scenario.TryRestoreAt(captureFails, 2));
        Assert.Equal(new[] { 1, 2 }, scenario.PacketFrames(healthy));
        Assert.Equal(new[] { (1, 1), (2, 2) }, scenario.Probes[healthy.Id].CapturedFrames);
    }

    [Fact]
    public void Authority_does_not_broadcast_or_commit_failed_frame_after_world_exception_isolation()
    {
        var manager = new GuardedWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(manager, options);
        var frames = new FrameSyncDriverModule();
        frames.Install(runtime, options);
        var connection = new RecordingConnection();
        runtime.Connect(connection);
        var world = (GuardedWorld)runtime.CreateWorld(new WorldCreateOptions(new WorldId("authority-guard"), "test"));
        var committed = new List<int>();
        frames.AddPostStep((frame, _) => committed.Add(frame.Value));
        runtime.Tick(.033f);
        Assert.Equal(1, frames.Frame.Value);
        Assert.Single(committed);
        Assert.Single(connection.Messages.OfType<FrameMessage>());
        var reads = world.SnapshotReads;
        world.Fail = true;
        runtime.Tick(.033f);
        runtime.Tick(.033f);
        Assert.Equal(1, frames.Frame.Value);
        Assert.Single(committed);
        Assert.Single(connection.Messages.OfType<FrameMessage>());
        Assert.Equal(reads, world.SnapshotReads);
        Assert.Equal(2, world.Attempts);
        Assert.Equal(2, world.Inputs);
        Assert.False(world.Guard.CanAdvance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prediction_does_not_publish_failed_progress_with_or_without_rollback(bool rollback)
    {
        var manager = new GuardedWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(manager, options);
        var captures = 0;
        var prediction = new ClientPredictionDriverModule(_ => null!, _ => null!,
            maxPredictionAheadFrames: 8, minPredictionWindow: 8, enableRollback: rollback,
            buildRollbackRegistry: world =>
            {
                var registry = new RollbackRegistry();
                registry.Register(new GuardedProvider((GuardedWorld)world, () => captures++));
                return registry;
            });
        prediction.Install(runtime, options);
        var world = (GuardedWorld)runtime.CreateWorld(new WorldCreateOptions(new WorldId("prediction-guard"), "test"));
        runtime.Tick(.033f);
        Assert.True(prediction.TryGetFrames(world.Id, out _, out var before));
        Assert.Equal(1, before.Value);
        var capturedBeforeFailure = captures;
        world.Fail = true;
        runtime.Tick(.033f);
        runtime.Tick(.033f);
        Assert.True(prediction.TryGetFrames(world.Id, out _, out var after));
        Assert.Equal(before, after);
        Assert.Equal(capturedBeforeFailure, captures);
        Assert.Equal(2, world.Attempts);
        Assert.Equal(2, world.Inputs);
        Assert.False(prediction.ShouldRunWorldTick);
        Assert.False(world.Guard.CanAdvance);
    }

    [Fact]
    public void Prediction_restore_floor_preserves_boundary_after_rebase()
    {
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(new GuardedWorldManager(), options);
        var prediction = new ClientPredictionDriverModule(_ => null!, _ => null!,
            maxPredictionAheadFrames: 8, minPredictionWindow: 8, enableRollback: true,
            buildRollbackRegistry: world =>
            {
                var registry = new RollbackRegistry();
                registry.Register(new GuardedProvider((GuardedWorld)world, () => { }));
                return registry;
            });
        prediction.Install(runtime, options);
        var world = runtime.CreateWorld(new WorldCreateOptions(new WorldId("prediction-floor"), "test"));
        Assert.True(runtime.Features.TryGetFeature<IClientPredictionRestoreFloor>(out var boundary));
        Assert.False(boundary.TryGetRestoreFloor(world.Id, out _));
        Assert.False(prediction.TryCommitVerifiedRestoreAnchor(world.Id, new FrameIndex(1)));

        runtime.Tick(.033f);
        var frame = new FrameIndex(1);
        Assert.True(prediction.TryCommitVerifiedRestoreAnchor(world.Id, frame));
        Assert.True(boundary.TryGetRestoreFloor(world.Id, out var committed));
        Assert.Equal(frame, committed);
        Assert.False(prediction.TryRebase(world.Id, new FrameIndex(0)));
        Assert.True(prediction.TryGetFrames(world.Id, out _, out var predicted));
        Assert.Equal(frame, predicted);

        Assert.True(prediction.TryRebase(world.Id, frame));
        Assert.True(boundary.TryGetRestoreFloor(world.Id, out committed));
        Assert.Equal(frame, committed);
        Assert.False(prediction.TryRebase(world.Id, new FrameIndex(0)));
    }

    [Fact]
    public void Catch_up_does_not_feed_or_advance_failed_frame()
    {
        var manager = new GuardedWorldManager();
        var runtime = new HostRuntime(manager);
        var world = (GuardedWorld)runtime.CreateWorld(new WorldCreateOptions(new WorldId("catchup-guard"), "test"));
        world.Fail = true;
        var packets = new List<FramePacket>();
        var result = WorldCatchUpDriver.CatchUpAndFeedSnapshots(runtime, world, 0, 3, .033f, 3,
            world, 4, packets.Add);
        Assert.Equal(0, result);
        Assert.Empty(packets);
        Assert.Equal(0, world.SnapshotReads);
        Assert.Equal(1, world.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_world_does_not_advance_server_time_while_healthy_world_continues(bool frameSync)
    {
        var manager = new GuardedWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(manager, options);
        var frames = new FrameSyncDriverModule();
        if (frameSync) frames.Install(runtime, options);
        var times = new ServerFrameTimeModule();
        times.Install(runtime, options);
        var connection = new RecordingConnection();
        runtime.Connect(connection);
        var failed = (GuardedWorld)runtime.CreateWorld(new WorldCreateOptions(new WorldId("failed"), "test"));
        var healthy = (GuardedWorld)runtime.CreateWorld(new WorldCreateOptions(new WorldId("healthy"), "test"));
        Assert.True(times.TryGet(failed.Id, out var failedTime));
        Assert.True(times.TryGet(healthy.Id, out var healthyTime));
        runtime.Tick(.033f);
        Assert.Equal(1, failedTime.Frame.Value);
        Assert.Equal(1, healthyTime.Frame.Value);
        failed.Fail = true;
        runtime.Tick(.033f);
        runtime.Tick(.033f);
        Assert.Equal(1, failedTime.Frame.Value);
        Assert.Equal(3, healthyTime.Frame.Value);
        Assert.Equal(2, failed.Attempts);
        Assert.Equal(3, healthy.Attempts);
        if (frameSync)
        {
            Assert.Equal(3, frames.Frame.Value);
            Assert.Single(connection.Messages.OfType<FrameMessage>(), message => message.Packet.WorldId == failed.Id);
            Assert.Equal(3, connection.Messages.OfType<FrameMessage>().Count(message => message.Packet.WorldId == healthy.Id));
            Assert.Equal(2, failed.Inputs);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Server_frame_time_uninstall_detaches_driver_and_drops_old_world_times(bool frameSync)
    {
        var worldId = new WorldId("reused-id");
        var module = new ServerFrameTimeModule();
        var firstOptions = new HostRuntimeOptions();
        var first = new HostRuntime(new GuardedWorldManager(), firstOptions);
        var frames = new FrameSyncDriverModule();
        if (frameSync) frames.Install(first, firstOptions);
        module.Install(first, firstOptions);
        first.CreateWorld(new WorldCreateOptions(worldId, "test"));
        Assert.True(module.TryGet(worldId, out var oldTime));
        first.Tick(0.125f);
        Assert.Equal(1, oldTime.Frame.Value);

        module.Uninstall(first, firstOptions);
        Assert.False(module.TryGet(worldId, out _));

        var secondOptions = new HostRuntimeOptions();
        var second = new HostRuntime(new GuardedWorldManager(), secondOptions);
        module.Install(second, secondOptions);
        second.CreateWorld(new WorldCreateOptions(worldId, "test"));
        Assert.True(module.TryGet(worldId, out var newTime));
        Assert.NotSame(oldTime, newTime);
        first.Tick(0.125f);
        Assert.Equal(1, oldTime.Frame.Value);
        Assert.Equal(0, newTime.Frame.Value);
        second.Tick(0.125f);
        Assert.Equal(1, newTime.Frame.Value);
        module.Uninstall(second, secondOptions);
    }

    private sealed class DualWorldCommitScenario : IDisposable
    {
        public const float Delta = 0.125f;
        private readonly WorldManager worlds = new(new GuardedWorldFactory());
        private readonly HostRuntimeOptions options = new();
        private readonly HostRuntime runtime;
        private readonly ServerFrameTimeModule time = new(Delta);
        private readonly ServerRollbackModule rollback;
        public FrameSyncDriverModule Driver { get; } = new();
        public RecordingConnection Connection { get; } = new();
        public Dictionary<WorldId, CaptureProbe> Probes { get; } = new();
        public List<int> HostSteps { get; } = new();
        public List<(WorldId World, int Frame, PlayerInputCommand[] Inputs)> InputsFlushed { get; } = new();

        public DualWorldCommitScenario()
        {
            runtime = new HostRuntime(worlds, options);
            runtime.Connect(Connection);
            rollback = new ServerRollbackModule(8, 1, world =>
            {
                var probe = new CaptureProbe((GuardedWorld)world);
                Probes.Add(world.Id, probe);
                var registry = new RollbackRegistry();
                registry.Register(probe);
                return registry;
            });
            Driver.Install(runtime, options);
            time.Install(runtime, options);
            rollback.Install(runtime, options);
            Driver.AddInputsFlushed((world, frame, inputs) => InputsFlushed.Add((world, frame.Value, inputs)));
            Driver.AddPostStep((frame, _) => HostSteps.Add(frame.Value));
        }

        public GuardedWorld Create(string id) => Assert.IsType<GuardedWorld>(runtime.CreateWorld(new WorldCreateOptions(new WorldId(id), "test")));
        public void Tick() => runtime.Tick(Delta);
        public IEnumerable<int> PacketFrames(GuardedWorld world) => Connection.Messages.OfType<FrameMessage>()
            .Where(message => message.Packet.WorldId == world.Id).Select(message => message.Packet.Frame.Value);

        public (int Frame, float Time) Clock(GuardedWorld world)
        {
            Assert.True(time.TryGet(world.Id, out var clock));
            return (clock.Frame.Value, clock.Time);
        }

        public bool TryRestoreAt(GuardedWorld world, int frame) =>
            rollback.TryRollbackAndReplay(world.Id, new FrameIndex(frame), new FrameIndex(frame), Delta);

        public void Dispose()
        {
            rollback.Uninstall(runtime, options);
            time.Uninstall(runtime, options);
            Driver.Uninstall(runtime, options);
            worlds.DisposeAll();
        }
    }

    private sealed class CaptureProbe(GuardedWorld world) : IRollbackStateProvider
    {
        public int Key => 1;
        public List<(int Frame, int Clock)> CapturedFrames { get; } = new();
        public bool RejectCapture { get; set; }
        public int RejectedCaptures { get; private set; }

        public byte[] Export(FrameIndex frame)
        {
            if (RejectCapture)
            {
                RejectedCaptures++;
                throw new InvalidOperationException("injected capture failure");
            }

            CapturedFrames.Add((frame.Value, world.Resolve<IFrameTime>().Frame.Value));
            return BitConverter.GetBytes(world.Attempts);
        }

        public void Import(FrameIndex frame, byte[] payload) { }
    }

    private sealed class GuardedWorldFactory : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => new GuardedWorld(options.Id, options.ServiceBuilder?.Build());
    }

    private sealed class RecordingConnection : IServerConnection
    {
        public ServerClientId ClientId => new("guard-client");
        public List<ServerMessage> Messages { get; } = new();
        public void Send(ServerMessage message) => Messages.Add(message);
    }

    private sealed class GuardedProvider(GuardedWorld world, Action captured) : IRollbackStateProvider
    {
        public int Key => 1;
        public byte[] Export(FrameIndex frame)
        {
            Assert.True(world.Guard.CanCommit);
            captured();
            return BitConverter.GetBytes(world.Attempts);
        }
        public void Import(FrameIndex frame, byte[] payload) { }
    }

    private sealed class GuardedWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> worlds = new();
        public IReadOnlyDictionary<WorldId, IWorld> Worlds => worlds;
        public IWorld Create(WorldCreateOptions options)
        {
            var world = new GuardedWorld(options.Id, options.ServiceBuilder?.Build());
            worlds.Add(world.Id, world);
            return world;
        }
        public bool TryGet(WorldId id, out IWorld world) => worlds.TryGetValue(id, out world!);
        public bool Destroy(WorldId id)
        {
            if (!worlds.Remove(id, out var world)) return false;
            world.Dispose();
            return true;
        }
        public void Tick(float deltaTime)
        {
            foreach (var world in worlds.Values)
            {
                /*
                 * 模拟正式 WorldManager 的异常隔离，确保 PostTick 仍会被调用。
                 */
                try { world.Tick(deltaTime); }
                catch (InvalidOperationException) { }
            }
        }
        public void DisposeAll()
        {
            foreach (var world in worlds.Values) world.Dispose();
            worlds.Clear();
        }
    }

    private sealed class GuardedWorld(WorldId id, WorldContainer? container) : IWorld, IWorldResolver, IWorldInputSink, IWorldStateSnapshotProvider
    {
        private readonly FrameTime time = new();
        public TestGuard Guard { get; } = new();
        public bool ExposeGuard { get; set; } = true;
        public bool Fail { get; set; }
        public int Attempts { get; private set; }
        public int Inputs { get; private set; }
        public int SnapshotReads { get; private set; }
        public WorldId Id => id;
        public string WorldType => "test";
        public IWorldResolver Services => this;
        public void Initialize() { }
        public void Dispose() => container?.Dispose();
        public void Tick(float deltaTime)
        {
            Guard.BeginFrame();
            Attempts++;
            if (Fail)
            {
                var error = new InvalidOperationException("injected world failure");
                Guard.FailFrame(error);
                throw error;
            }
            Guard.CompleteFrame();
        }
        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => Inputs++;
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            SnapshotReads++;
            snapshot = new WorldStateSnapshot(1, new byte[] { 1 });
            return true;
        }
        public object Resolve(Type type) => TryResolve(type, out var result) ? result : throw new InvalidOperationException(type.Name);
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object result)
        {
            if (container != null && container.TryResolve(type, out result)) return true;
            result = type == typeof(IWorldFrameGuard) && ExposeGuard ? Guard : type == typeof(IFrameTime) ? time :
                type == typeof(IWorldInputSink) || type == typeof(IWorldStateSnapshotProvider) ? this : null!;
            return result != null;
        }
        public bool TryResolve<T>(out T result)
        {
            var found = TryResolve(typeof(T), out var value);
            result = found ? (T)value : default!;
            return found;
        }
    }

    private sealed class TestGuard : IWorldFrameGuard
    {
        private bool failed, executing;
        public bool CanAdvance => !failed && !executing;
        public bool CanCommit => CanAdvance;
        public void BeginFrame()
        {
            if (!CanAdvance) throw new InvalidOperationException("world cannot advance");
            executing = true;
        }
        public void CompleteFrame() => executing = false;
        public void FailFrame(Exception error) { failed = true; executing = false; }
    }
}
