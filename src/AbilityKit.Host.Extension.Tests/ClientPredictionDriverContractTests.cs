using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Abstractions;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class ClientPredictionDriverContractTests
{
    private const float Delta = 1f / 30f;

    [Theory]
    [InlineData("custom")]
    [InlineData("default")]
    [InlineData("readonly")]
    [InlineData("missing")]
    public void Clock_capability_is_optional_and_rebase_uses_the_registered_clock(string kind)
    {
        IFrameTime? clock = kind switch
        {
            "custom" => new CustomClock(),
            "default" => new FrameTime(),
            "readonly" => new ReadOnlyClock(),
            _ => null,
        };
        using var scenario = new Scenario(clock);
        scenario.Tick();
        scenario.AssertFrames(0, 1);
        Assert.Equal(1, scenario.World.Ticks);
        Assert.True(scenario.Driver.TryRebase(scenario.World.Id, new FrameIndex(10)));
        scenario.Tick();
        scenario.AssertFrames(10, 11);
        Assert.Equal(2, scenario.World.Ticks);

        if (clock is IFrameTimeAligner)
        {
            Assert.Equal(11, clock.Frame.Value);
            Assert.Equal(11 * Delta, clock.Time, 5);
            Assert.Equal(Delta, clock.DeltaTime);
        }
        if (clock is CustomClock custom)
            Assert.Equal(new[] { (1, Delta), (10, 0f), (11, Delta) }, custom.Alignments);
        if (clock is ReadOnlyClock)
            Assert.Equal(0, clock.Frame.Value);
    }

    [Fact]
    public void Matching_authority_confirms_without_simulating_the_same_frame_twice()
    {
        using var scenario = new Scenario(new CustomClock());
        scenario.Tick();
        scenario.Remote.Add(1);
        scenario.Tick();

        scenario.AssertFrames(1, 2);
        Assert.Equal(new[] { "predict:1", "predict:2" }, scenario.World.InputCalls);
        Assert.Equal(2, scenario.World.Ticks);
        Assert.Equal(0, scenario.Driver.TotalRollbackCount);
        Assert.Equal(1, scenario.Driver.TotalConsumedConfirmedFrames);
        Assert.Equal(2, scenario.Driver.TotalPredictedFrames);
    }

    [Fact]
    public void Mismatch_restores_and_replays_from_the_next_frame_before_resuming_prediction()
    {
        var clock = new CustomClock();
        using var scenario = new Scenario(clock);
        scenario.Tick();
        scenario.Tick();
        scenario.Remote.Add(1, Command(1, 77));
        scenario.Tick();
        scenario.AssertFrames(1, 1);
        Assert.Equal(1, scenario.World.Ticks);
        Assert.Equal(new[] { 0 }, scenario.World.RestoredFrames);

        scenario.Tick();
        Assert.False(scenario.Driver.ShouldRunWorldTick);
        Assert.Equal(1, scenario.World.Ticks);
        scenario.Remote.Add(2);
        scenario.Tick();
        scenario.AssertFrames(2, 2);
        scenario.Tick();
        Assert.False(scenario.Driver.ShouldRunWorldTick);
        scenario.Tick();
        scenario.AssertFrames(2, 3);

        Assert.Equal(new[] { "predict:1", "predict:2", "begin:0:2", "replay:1", "replay:2", "end", "predict:3" }, scenario.World.InputCalls);
        Assert.Equal(new[] { 1, 2, 1, 2, 3 }, clock.Alignments.Select(entry => entry.Frame));
        Assert.Equal(1, scenario.Driver.TotalRollbackCount);
        Assert.Equal(3, scenario.World.Ticks);
    }

    [Fact]
    public void Frame_guard_runs_before_source_resolvers_and_resolver_order_is_preserved()
    {
        using var scenario = new Scenario(new CustomClock());
        scenario.World.CanAdvance = false;
        scenario.Tick();
        Assert.Empty(scenario.Resolutions);
        Assert.Empty(scenario.World.InputCalls);
        scenario.AssertFrames(0, 0);

        scenario.World.CanAdvance = true;
        scenario.Tick();
        Assert.Equal(new[] { "remote", "local", "ideal", "dequeue" }, scenario.Resolutions);
        Assert.Equal(new[] { "predict:1" }, scenario.World.InputCalls);
    }

    [Fact]
    public void Local_queue_preserves_stale_retarget_merge_and_batch_drop_count()
    {
        using var scenario = new Scenario(new CustomClock());
        scenario.Tick();
        var player = new PlayerId("player");
        scenario.Local.Enqueue(
            new LocalPlayerInputEvent(new FrameIndex(1), player, 10, new byte[] { 1 }, true),
            new LocalPlayerInputEvent(new FrameIndex(1), player, 10, new byte[] { 2 }, true),
            new LocalPlayerInputEvent(new FrameIndex(1), player, 11, new byte[] { 3 }),
            new LocalPlayerInputEvent(new FrameIndex(3), player, 12, new byte[] { 4 }));
        scenario.Tick();
        var retargeted = Assert.Single(scenario.World.InputBatches[1]);
        Assert.Equal(2, retargeted.Frame.Value);
        Assert.Equal(10, retargeted.OpCode);
        Assert.Equal(new byte[] { 2 }, retargeted.Payload);
        scenario.Tick();
        Assert.Equal(12, Assert.Single(scenario.World.InputBatches[2]).OpCode);
        Assert.Equal(1, scenario.Driver.TotalLocalDelayQueueDroppedBatches);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(false, "world")]
    [InlineData(false, "capture")]
    [InlineData(true, "none")]
    [InlineData(true, "world")]
    [InlineData(true, "capture")]
    public void Settlement_consumes_only_after_successful_world_tick_and_capture(bool timeline, string failure)
    {
        using var scenario = new Scenario(new CustomClock(), settled: true, timeline: timeline);
        scenario.World.FailOnTick = failure == "world";
        scenario.World.FailCapture = failure == "capture";
        if (timeline) scenario.Timeline.Add(1, Command(1, 77));
        else scenario.Remote.Add(1, Command(1, 77));
        int Consumed() => timeline ? scenario.Timeline.LastConsumedFrame : scenario.Remote.LastConsumedFrame;
        scenario.World.BeforeTick = () => Assert.Equal(0, Consumed());
        scenario.World.BeforeCapture = () => Assert.Equal(0, Consumed());

        scenario.Tick();

        var committed = failure == "none" ? 1 : 0;
        scenario.AssertFrames(committed, committed);
        Assert.Equal(committed, Consumed());
        Assert.Equal(timeline && committed == 1 ? new[] { 1 } : Array.Empty<int>(), scenario.World.TimelineCommits);
        Assert.Equal(new[] { "settled:1" }, scenario.World.InputCalls);
        if (committed == 0)
        {
            scenario.Tick();
            Assert.False(scenario.Driver.ShouldRunWorldTick);
            Assert.Equal(1, scenario.World.Ticks);
            Assert.Equal(0, Consumed());
        }
    }

    [Fact]
    public void Settled_timeline_correction_replays_the_original_predicted_suffix()
    {
        using var scenario = new Scenario(new CustomClock(), timeline: true);
        scenario.Tick();
        scenario.Tick();
        scenario.Timeline.Add(1, Command(1, 77));
        scenario.Tick();
        scenario.AssertFrames(1, 1);
        scenario.Tick();
        scenario.AssertFrames(1, 2);
        scenario.Tick();
        scenario.AssertFrames(1, 3);

        Assert.Equal(new[] { "predict:1", "predict:2", "settled:1", "predict:2", "predict:3" }, scenario.World.InputCalls);
        Assert.Equal(new[] { 0 }, scenario.World.RestoredFrames);
        Assert.Equal(new[] { 1 }, scenario.World.TimelineCommits);
        Assert.Equal(1, scenario.Driver.TotalRollbackCount);
        Assert.Equal(3, scenario.World.Ticks);
    }

    private static PlayerInputCommand Command(int frame, int opcode) =>
        new(new FrameIndex(frame), new PlayerId("player"), opcode, new byte[] { 9 });

    private sealed class Scenario : IDisposable
    {
        private readonly HostRuntimeOptions options = new();
        private readonly WorldManager worlds;
        private readonly HostRuntime runtime;
        public ClientPredictionDriverModule Driver { get; }
        public TestWorld World { get; }
        public RemoteFrames Remote { get; } = new();
        public SettledFrames Timeline { get; } = new();
        public LocalInputs Local { get; }
        public List<string> Resolutions { get; } = new();

        public Scenario(IFrameTime? clock, bool settled = false, bool timeline = false)
        {
            worlds = new WorldManager(new Factory(clock));
            runtime = new HostRuntime(worlds, options);
            Local = new LocalInputs(Resolutions);
            Driver = new ClientPredictionDriverModule(
                timeline ? null! : _ => { Resolutions.Add("remote"); return Remote; },
                _ => { Resolutions.Add("local"); return Local; },
                _ => { Resolutions.Add("ideal"); return 0; },
                maxPredictionAheadFrames: 8, minPredictionWindow: 8, enableRollback: true,
                buildRollbackRegistry: world =>
                {
                    var registry = new RollbackRegistry();
                    registry.Register(new WorldState((TestWorld)world));
                    return registry;
                }, useSettledExecutionInputs: settled, resolveSettledFrames: timeline ? _ => Timeline : null!);
            Driver.Install(runtime, options);
            World = Assert.IsType<TestWorld>(runtime.CreateWorld(new WorldCreateOptions(new WorldId("prediction-contract"), "test")));
        }

        public void Tick() => runtime.Tick(Delta);
        public void AssertFrames(int confirmed, int predicted)
        {
            Assert.True(Driver.TryGetFrames(World.Id, out var actualConfirmed, out var actualPredicted));
            Assert.Equal(confirmed, actualConfirmed.Value);
            Assert.Equal(predicted, actualPredicted.Value);
        }
        public void Dispose()
        {
            Driver.Uninstall(runtime, options);
            worlds.DisposeAll();
        }
    }

    private sealed class Factory(IFrameTime? clock) : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => new TestWorld(options.Id, clock);
    }

    private sealed class TestWorld(WorldId id, IFrameTime? clock) : IWorld, IWorldSettledTimelineSink, IWorldInputReplaySink, IWorldFrameGuard
    {
        private WorldContainer? services;
        public WorldId Id => id;
        public string WorldType => "test";
        public IWorldResolver Services => services!;
        public bool CanAdvance { get; set; } = true;
        public bool CanCommit => CanAdvance;
        public void BeginFrame() { }
        public void CompleteFrame() { }
        public void FailFrame(Exception error) => CanAdvance = false;
        public int Ticks { get; set; }
        public bool FailOnTick { get; set; }
        public bool FailCapture { get; set; }
        public Action? BeforeTick { get; set; }
        public Action? BeforeCapture { get; set; }
        public List<string> InputCalls { get; } = new();
        public List<PlayerInputCommand[]> InputBatches { get; } = new();
        public List<int> RestoredFrames { get; } = new();
        public List<int> TimelineCommits { get; } = new();
        public void Initialize()
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterExternalInstance<IWorldInputSink>(this);
            builder.RegisterExternalInstance<IWorldFrameGuard>(this);
            if (clock != null) builder.RegisterExternalInstance<IFrameTime>(clock);
            services = builder.Build();
        }
        public void Tick(float deltaTime)
        {
            BeforeTick?.Invoke();
            Ticks++;
            if (FailOnTick) CanAdvance = false;
        }
        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => throw new InvalidOperationException("Expected typed input entry.");
        public void BindClientInput() { }
        public void SubmitPredicted(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            InputCalls.Add($"predict:{frame.Value}");
            InputBatches.Add(inputs.ToArray());
        }
        public void SubmitReceived(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => InputCalls.Add($"received:{frame.Value}");
        public void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame) => InputCalls.Add($"begin:{restoredFrame.Value}:{replayToFrame.Value}");
        public void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => InputCalls.Add($"replay:{frame.Value}");
        public void EndReplay() => InputCalls.Add("end");
        public PlayerInputCommand[] ExecutePredictedInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            SubmitPredicted(frame, inputs);
            return inputs.ToArray();
        }
        public void ExecuteSettledInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> executedInputs) => InputCalls.Add($"settled:{frame.Value}");
        public void BindSettledTimeline() { }
        public IWorldSettledFrameUpdate PrepareSettledFrame(WorldSettledInputFrame frame) => new FrameUpdate(this, frame.Frame.Value);
        public void Dispose() => services?.Dispose();
    }

    private sealed class FrameUpdate(TestWorld world, int frame) : IWorldSettledFrameUpdate
    {
        public bool InvalidatesPrediction => false;
        public void Commit() => world.TimelineCommits.Add(frame);
    }

    private sealed class WorldState(TestWorld world) : IRollbackStateProvider
    {
        public int Key => 1;
        public byte[] Export(FrameIndex frame)
        {
            world.BeforeCapture?.Invoke();
            if (world.FailCapture) throw new InvalidOperationException("Injected capture failure.");
            return BitConverter.GetBytes(world.Ticks);
        }
        public void Import(FrameIndex frame, byte[] payload)
        {
            world.Ticks = BitConverter.ToInt32(payload);
            world.RestoredFrames.Add(frame.Value);
        }
    }

    private sealed class CustomClock : IFrameTimeAligner
    {
        public FrameIndex Frame { get; private set; }
        public float DeltaTime { get; private set; }
        public float Time => Frame.Value * DeltaTime;
        public List<(int Frame, float Fallback)> Alignments { get; } = new();
        public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
        public FrameIndex TimeToFrame(float time) => new((int)(time / DeltaTime));
        public bool AlignTo(FrameIndex targetFrame, float fallbackDeltaTime)
        {
            Alignments.Add((targetFrame.Value, fallbackDeltaTime));
            if (DeltaTime <= 0) DeltaTime = fallbackDeltaTime;
            Frame = targetFrame;
            return true;
        }
    }

    private sealed class ReadOnlyClock : IFrameTime
    {
        public FrameIndex Frame => default;
        public float DeltaTime => Delta;
        public float Time => 0;
        public float FrameToTime(FrameIndex frame) => frame.Value * Delta;
        public FrameIndex TimeToFrame(float time) => new((int)(time / Delta));
    }

    private sealed class RemoteFrames : IConsumableRemoteFrameSource<PlayerInputCommand[]>
    {
        private readonly Dictionary<int, PlayerInputCommand[]> frames = new();
        public int DelayFrames { get; set; }
        public int TargetFrame { get; private set; }
        public int MaxReceivedFrame => TargetFrame;
        public int LastConsumedFrame { get; private set; }
        public void Add(int frame, params PlayerInputCommand[] inputs)
        {
            frames.Add(frame, inputs);
            TargetFrame = Math.Max(TargetFrame, frame);
        }
        public bool TryGet(int frame, out PlayerInputCommand[] data) => frames.TryGetValue(frame, out data!);
        public bool TryConsume(int frame, out PlayerInputCommand[] data)
        {
            if (!frames.Remove(frame, out data!)) return false;
            LastConsumedFrame = frame;
            return true;
        }
        public void TrimBefore(int minFrameInclusive) { }
        public void Dispose() { }
    }

    private sealed class LocalInputs(List<string> resolutions) : ILocalInputSource<LocalPlayerInputEvent[]>
    {
        private readonly Queue<LocalPlayerInputEvent[]> pending = new();
        public int LocalFrame => 0;
        public void Enqueue(params LocalPlayerInputEvent[] inputs) => pending.Enqueue(inputs);
        public bool TryDequeue(out LocalPlayerInputEvent[] input)
        {
            resolutions.Add("dequeue");
            return pending.TryDequeue(out input!);
        }
        public void Dispose() { }
    }

    private sealed class SettledMetadata : IWorldSettledFrameMetadata { }

    private sealed class SettledFrames : IConsumableRemoteFrameSource<WorldSettledInputFrame>
    {
        private readonly Dictionary<int, WorldSettledInputFrame> frames = new();
        public int DelayFrames { get; set; }
        public int TargetFrame { get; private set; }
        public int MaxReceivedFrame => TargetFrame;
        public int LastConsumedFrame { get; private set; }
        public void Add(int frame, params PlayerInputCommand[] inputs)
        {
            frames.Add(frame, new WorldSettledInputFrame(new FrameIndex(frame), inputs, new SettledMetadata()));
            TargetFrame = Math.Max(TargetFrame, frame);
        }
        public bool TryGet(int frame, out WorldSettledInputFrame data) => frames.TryGetValue(frame, out data!);
        public bool TryConsume(int frame, out WorldSettledInputFrame data)
        {
            if (!frames.Remove(frame, out data!)) return false;
            LastConsumedFrame = frame;
            return true;
        }
        public void TrimBefore(int minFrameInclusive) { }
        public void Dispose() { }
    }
}
