using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.HostExtension;

public sealed class FrameSyncDriverModuleHeadlessTests
{
    [Fact]
    public void HeadlessSession_WhenTicked_FlushesInputsAndBroadcastsFramePacketWithoutWorld()
    {
        var worlds = new EmptyWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var module = new FrameSyncDriverModule();
        var connection = new RecordingConnection(new ServerClientId("client-1"));
        var worldId = new WorldId("headless-world");
        var input = new PlayerInputCommand(new FrameIndex(0), new PlayerId("player-1"), 101, new byte[] { 1, 2, 3 });

        module.Install(runtime, options);
        runtime.Connect(connection);

        Assert.True(runtime.Features.TryGetFeature<IFrameSyncInputHub>(out var inputHub));
        Assert.True(runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out var events));

        WorldId flushedWorldId = default;
        FrameIndex flushedFrame = default;
        PlayerInputCommand[] flushedInputs = null!;
        events.AddInputsFlushed((id, frame, inputs) =>
        {
            flushedWorldId = id;
            flushedFrame = frame;
            flushedInputs = inputs;
        });

        module.RegisterSession(worldId);

        Assert.True(inputHub.SubmitInput(connection.ClientId, worldId, input));

        runtime.Tick(1f / 30f);

        Assert.Equal(worldId, flushedWorldId);
        Assert.Equal(1, flushedFrame.Value);
        Assert.NotNull(flushedInputs);
        var flushedInput = Assert.Single(flushedInputs);
        Assert.Equal(101, flushedInput.OpCode);
        Assert.Equal("player-1", flushedInput.Player.Value);

        var frameMessage = Assert.IsType<FrameMessage>(Assert.Single(connection.Messages));
        Assert.Equal(worldId, frameMessage.Packet.WorldId);
        Assert.Equal(1, frameMessage.Packet.Frame.Value);
        Assert.Null(frameMessage.Packet.Snapshot);
        var packetInput = Assert.Single(frameMessage.Packet.Inputs);
        Assert.Equal(101, packetInput.OpCode);
        Assert.Equal("player-1", packetInput.Player.Value);
    }

    [Fact]
    public void HeadlessSession_WhenUnregistered_RejectsLaterInput()
    {
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(new EmptyWorldManager(), options);
        var module = new FrameSyncDriverModule();
        var worldId = new WorldId("headless-world");
        var input = new PlayerInputCommand(new FrameIndex(0), new PlayerId("player-1"), 102, new byte[] { 4 });

        module.Install(runtime, options);
        module.RegisterSession(worldId);
        module.UnregisterSession(worldId);

        Assert.True(runtime.Features.TryGetFeature<IFrameSyncInputHub>(out var inputHub));
        Assert.False(inputHub.SubmitInput(new ServerClientId("client-1"), worldId, input));
    }

    [Fact]
    public void WorldCreatedSession_WhenTicked_StillAcceptsInputThroughExistingHubPath()
    {
        var worlds = new InMemoryWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var module = new FrameSyncDriverModule();
        var connection = new RecordingConnection(new ServerClientId("client-1"));
        var worldId = new WorldId("world-backed-session");
        var input = new PlayerInputCommand(new FrameIndex(0), new PlayerId("player-1"), 103, new byte[] { 5 });

        module.Install(runtime, options);
        runtime.Connect(connection);

        runtime.CreateWorld(new WorldCreateOptions(worldId, "test-world"));

        Assert.True(runtime.Features.TryGetFeature<IFrameSyncInputHub>(out var inputHub));
        Assert.True(inputHub.SubmitInput(connection.ClientId, worldId, input));

        runtime.Tick(1f / 30f);

        var frameMessages = connection.Messages.OfType<FrameMessage>().ToArray();
        var frameMessage = Assert.Single(frameMessages);
        Assert.Equal(worldId, frameMessage.Packet.WorldId);
        Assert.Equal(1, frameMessage.Packet.Frame.Value);
        var packetInput = Assert.Single(frameMessage.Packet.Inputs);
        Assert.Equal(103, packetInput.OpCode);
    }

    [Fact]
    public void ClientPrediction_WhenMatchingAuthorityConfirmsPredictedFrame_DoesNotSimulateFrameTwice()
    {
        const float fixedDelta = 1f / 30f;
        var remote = new FrameJitterBuffer<PlayerInputCommand[]>(
            delayFrames: 0,
            MissingFrameMode.Wait,
            () => System.Array.Empty<PlayerInputCommand>());
        var worlds = new PredictionWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var module = new ClientPredictionDriverModule(
            _ => remote,
            _ => null,
            maxPredictionAheadFrames: 2,
            minPredictionWindow: 2,
            enableRollback: true,
            rollbackHistoryFrames: 16);
        var worldId = new WorldId("prediction-confirmation");

        module.Install(runtime, options);
        var world = Assert.IsType<PredictionWorld>(runtime.CreateWorld(new WorldCreateOptions(worldId, "test-world")));

        runtime.Tick(fixedDelta);
        remote.Add(1, System.Array.Empty<PlayerInputCommand>());
        runtime.Tick(fixedDelta);

        Assert.Equal(new[] { 1, 2 }, world.InputSink.SubmittedFrames);
        Assert.Equal(new[] { 1, 2 }, world.SimulatedFrames);
        Assert.True(module.TryGetFrames(worldId, out var confirmed, out var predicted));
        Assert.Equal(1, confirmed.Value);
        Assert.Equal(2, predicted.Value);
    }

    [Fact]
    public void ClientPrediction_WhenReplayUsesCachedAuthority_AdvancesConfirmedFrame()
    {
        const float fixedDelta = 1f / 30f;
        var remote = new FrameJitterBuffer<PlayerInputCommand[]>(
            delayFrames: 0,
            MissingFrameMode.Wait,
            () => System.Array.Empty<PlayerInputCommand>());
        var worlds = new PredictionWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var module = new ClientPredictionDriverModule(
            _ => remote,
            _ => null,
            maxPredictionAheadFrames: 2,
            minPredictionWindow: 2,
            enableRollback: true,
            rollbackHistoryFrames: 16,
            buildComputeHash: _ => frame => new WorldStateHash((uint)frame.Value));
        var worldId = new WorldId("prediction-replay-confirmation");

        module.Install(runtime, options);
        var world = Assert.IsType<PredictionWorld>(runtime.CreateWorld(new WorldCreateOptions(worldId, "test-world")));
        runtime.Tick(fixedDelta);
        remote.Add(1, System.Array.Empty<PlayerInputCommand>());
        runtime.Tick(fixedDelta);

        module.OnAuthoritativeStateHash(worldId, new FrameIndex(1), new WorldStateHash(999u));
        Assert.True(module.TryGetFrames(worldId, out var rolledBackConfirmed, out _));
        Assert.Equal(0, rolledBackConfirmed.Value);

        runtime.Tick(fixedDelta);

        Assert.True(module.TryGetFrames(worldId, out var replayConfirmed, out var replayPredicted));
        Assert.Equal(1, replayConfirmed.Value);
        Assert.Equal(1, replayPredicted.Value);

        var simulatedWhileWaiting = world.SimulatedFrames.Count;
        for (var index = 0; index < 200; index++)
        {
            runtime.Tick(fixedDelta);
        }
        Assert.Equal(simulatedWhileWaiting, world.SimulatedFrames.Count);
        Assert.Equal(0, module.TotalReplayTimeout);

        remote.Add(2, System.Array.Empty<PlayerInputCommand>());
        runtime.Tick(fixedDelta);
        var simulatedBeforeReplayCompletion = world.SimulatedFrames.Count;

        runtime.Tick(fixedDelta);

        Assert.Equal(simulatedBeforeReplayCompletion, world.SimulatedFrames.Count);
        Assert.False(module.IsReplaying);
    }

    [Fact]
    public void ClientPrediction_WhenContinuousIntentBecomesStale_RetargetsWithoutDropping()
    {
        const float fixedDelta = 1f / 30f;
        var local = new QueuedLocalInputSource();
        var worlds = new PredictionWorldManager();
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, options);
        var module = new ClientPredictionDriverModule(
            _ => null,
            _ => local,
            maxPredictionAheadFrames: 3,
            minPredictionWindow: 3,
            enableRollback: true,
            rollbackHistoryFrames: 16);
        var worldId = new WorldId("prediction-stale-intent");

        module.Install(runtime, options);
        var world = Assert.IsType<PredictionWorld>(runtime.CreateWorld(new WorldCreateOptions(worldId, "test-world")));
        runtime.Tick(fixedDelta);
        local.Enqueue(new[]
        {
            new LocalPlayerInputEvent(
                new FrameIndex(1),
                new PlayerId("player-1"),
                3003,
                new byte[] { 1 },
                canRetargetIfStale: true),
        });

        runtime.Tick(fixedDelta);

        Assert.Equal(0, module.TotalLocalDelayQueueDroppedBatches);
        Assert.Equal(new[] { 1, 2 }, world.InputSink.SubmittedFrames);
        var retargeted = Assert.Single(world.InputSink.SubmittedInputs[1]);
        Assert.Equal(2, retargeted.Frame.Value);
        Assert.Equal(3003, retargeted.OpCode);
    }

    private sealed class EmptyWorldManager : IWorldManager
    {
        private readonly IReadOnlyDictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            throw new System.NotSupportedException("Headless frame sync tests do not create worlds.");
        }

        public bool TryGet(WorldId id, out IWorld world)
        {
            world = null!;
            return false;
        }

        public bool Destroy(WorldId id) => false;

        public void Tick(float deltaTime)
        {
        }

        public void DisposeAll()
        {
        }
    }

    private sealed class InMemoryWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            var world = new TestWorld(options.Id, options.WorldType);
            _worlds[world.Id] = world;
            return world;
        }

        public bool TryGet(WorldId id, out IWorld world)
        {
            return _worlds.TryGetValue(id, out world!);
        }

        public bool Destroy(WorldId id)
        {
            return _worlds.Remove(id);
        }

        public void Tick(float deltaTime)
        {
            foreach (var world in _worlds.Values)
            {
                world.Tick(deltaTime);
            }
        }

        public void DisposeAll()
        {
            _worlds.Clear();
        }
    }

    private sealed class PredictionWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            var world = new PredictionWorld(options.Id, options.WorldType);
            _worlds.Add(world.Id, world);
            return world;
        }

        public bool TryGet(WorldId id, out IWorld world) => _worlds.TryGetValue(id, out world!);

        public bool Destroy(WorldId id) => _worlds.Remove(id);

        public void Tick(float deltaTime)
        {
            foreach (var world in _worlds.Values)
            {
                world.Tick(deltaTime);
            }
        }

        public void DisposeAll() => _worlds.Clear();
    }

    private sealed class PredictionWorld : IWorld
    {
        public PredictionWorld(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
            InputSink = new RecordingWorldInputSink();
            FrameTime = new FrameTime();
            Services = new PredictionWorldResolver(InputSink, FrameTime);
        }

        public WorldId Id { get; }

        public string WorldType { get; }

        public IWorldResolver Services { get; }

        public RecordingWorldInputSink InputSink { get; }

        public FrameTime FrameTime { get; }

        public List<int> SimulatedFrames { get; } = new List<int>();

        public void Initialize()
        {
        }

        public void Tick(float deltaTime)
        {
            SimulatedFrames.Add(InputSink.LastSubmittedFrame);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingWorldInputSink : IWorldInputSink
    {
        public List<int> SubmittedFrames { get; } = new List<int>();

        public List<PlayerInputCommand[]> SubmittedInputs { get; } = new List<PlayerInputCommand[]>();

        public int LastSubmittedFrame { get; private set; } = -1;

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            LastSubmittedFrame = frame.Value;
            SubmittedFrames.Add(frame.Value);
            var copy = new PlayerInputCommand[inputs?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = inputs[index];
            }
            SubmittedInputs.Add(copy);
        }

        public void Dispose()
        {
        }
    }

    private sealed class QueuedLocalInputSource : ILocalInputSource<LocalPlayerInputEvent[]>
    {
        private readonly Queue<LocalPlayerInputEvent[]> _inputs = new Queue<LocalPlayerInputEvent[]>();

        public int LocalFrame { get; private set; }

        public void Enqueue(LocalPlayerInputEvent[] inputs)
        {
            _inputs.Enqueue(inputs);
        }

        public bool TryDequeue(out LocalPlayerInputEvent[] input)
        {
            LocalFrame++;
            if (_inputs.Count > 0)
            {
                input = _inputs.Dequeue();
                return true;
            }

            input = null!;
            return false;
        }

        public void Dispose()
        {
            _inputs.Clear();
        }
    }

    private sealed class PredictionWorldResolver : IWorldResolver
    {
        private readonly IWorldInputSink _inputSink;
        private readonly FrameTime _frameTime;

        public PredictionWorldResolver(IWorldInputSink inputSink, FrameTime frameTime)
        {
            _inputSink = inputSink;
            _frameTime = frameTime;
        }

        public object Resolve(System.Type serviceType)
        {
            if (TryResolve(serviceType, out var instance)) return instance;
            throw new System.InvalidOperationException($"Service not registered: {serviceType.FullName}");
        }

        public T Resolve<T>()
        {
            if (TryResolve<T>(out var instance)) return instance;
            throw new System.InvalidOperationException($"Service not registered: {typeof(T).FullName}");
        }

        public bool TryResolve(System.Type serviceType, out object instance)
        {
            if (serviceType == typeof(IWorldInputSink))
            {
                instance = _inputSink;
                return true;
            }

            if (serviceType == typeof(IFrameTime) || serviceType == typeof(FrameTime))
            {
                instance = _frameTime;
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

    private sealed class TestWorld : IWorld
    {
        public TestWorld(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
            Services = EmptyWorldResolver.Instance;
        }

        public WorldId Id { get; }

        public string WorldType { get; }

        public IWorldResolver Services { get; }

        public int TickCount { get; private set; }

        public void Initialize()
        {
        }

        public void Tick(float deltaTime)
        {
            TickCount++;
        }

        public void Dispose()
        {
        }
    }

    private sealed class EmptyWorldResolver : IWorldResolver
    {
        public static readonly EmptyWorldResolver Instance = new EmptyWorldResolver();

        private EmptyWorldResolver()
        {
        }

        public object Resolve(System.Type serviceType)
        {
            throw new System.InvalidOperationException($"Service not registered: {serviceType.FullName}");
        }

        public T Resolve<T>()
        {
            throw new System.InvalidOperationException($"Service not registered: {typeof(T).FullName}");
        }

        public bool TryResolve(System.Type serviceType, out object instance)
        {
            instance = null!;
            return false;
        }

        public bool TryResolve<T>(out T instance)
        {
            instance = default!;
            return false;
        }
    }

    private sealed class RecordingConnection : IServerConnection
    {
        public RecordingConnection(ServerClientId clientId)
        {
            ClientId = clientId;
        }

        public ServerClientId ClientId { get; }

        public List<ServerMessage> Messages { get; } = new List<ServerMessage>();

        public void Send(ServerMessage message)
        {
            Messages.Add(message);
        }
    }
}
