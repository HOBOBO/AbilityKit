using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaVirtualFrameCarrierTests
{
    [Fact]
    public void DisconnectDropsPendingAndCatchUpFramesUseNormalDeliverySink()
    {
        var delivered = new List<string>();
        MobaVirtualFrameCarrier? carrier = null;
        carrier = new MobaVirtualFrameCarrier(
            new NetworkConditionScenario(new NetworkConditionProfile(30, 0, 0, 0, 0)),
            packet => delivered.Add($"{packet.Frame.Value}@{carrier!.Link.NowMs}"),
            seed: 17);
        using (carrier)
        {
            carrier.Play(Commands(), ResolveFrame, finishAtMs: 100);

            Assert.Equal(new[] { "1@90", "3@100" }, delivered);
            Assert.Equal(new[]
            {
                VirtualNetworkLinkEventKind.Disconnected,
                VirtualNetworkLinkEventKind.BlockedInbound,
                VirtualNetworkLinkEventKind.Reconnected,
            }, carrier.Link.Events.Select(entry => entry.Kind));
        }
    }

    [Fact]
    public void SameCommandsAndSeedProduceSameDeliveredFrameTrace()
    {
        Assert.Equal(RunTrace(), RunTrace());
    }

    [Fact]
    public void SerializedPayloadSizeDrivesBandwidthDelay()
    {
        var delivered = new List<int>();
        using var carrier = new MobaVirtualFrameCarrier(
            new NetworkConditionScenario(new NetworkConditionProfile(0, 0, 0, 0, 8)),
            packet => delivered.Add(packet.Frame.Value));

        carrier.Play(
            new[] { Packet(0, 1) },
            ResolveFrame,
            finishAtMs: 999,
            serializeFrame: _ => new ArraySegment<byte>(new byte[1000]));
        Assert.Empty(delivered);

        carrier.Link.AdvanceTo(1000);
        Assert.Equal(new[] { 1 }, delivered);
    }

    [Fact]
    public void SessionRunner_ReconnectCatchUpReplaysDeterministically()
    {
        var first = RunSessionScenario();
        var second = RunSessionScenario();

        Assert.Equal(new[] { 1, 2, 3, 4 }, first.ObservedFrames);
        Assert.Equal(2, first.Result.LinkEvents.Count(entry =>
            entry.Kind == VirtualNetworkLinkEventKind.BlockedInbound));
        Assert.Equal(60, first.Result.FinishedAtMs);
        Assert.Equal(60, first.Result.SimulationFinishedAtMs);
        Assert.Equal(60, first.SimulationAdvancedMs);
        Assert.Equal("1,2,3,4", first.Result.FinalStateFingerprint);
        Assert.Equal(first.Result.DeterminismFingerprint, second.Result.DeterminismFingerprint);
        Assert.Equal(first.Result.Trace, second.Result.Trace);
    }

    [Fact]
    public void SessionRunner_RejectsDeliveryBeyondVirtualTimeout()
    {
        var plan = VirtualNetworkScenarioPlan.Compile(
            new[]
            {
                new VirtualNetworkCommand(0, VirtualNetworkScenarioPlayer.PhaseCommand,
                    new Dictionary<string, string>
                    {
                        ["until"] = "1000",
                        ["latency"] = "100",
                    }),
                SessionPacket(0, 1, 1),
            },
            NetworkConditionProfile.Ideal);

        Assert.Throws<TimeoutException>(() =>
            new MobaVirtualFrameSessionRunner().Run(
                _ => { },
                plan,
                ResolveSessionFrame,
                timeoutMs: 50));
    }

    [Fact]
    public void SessionRunner_HybridHashMismatchRollsBackAndReplaysDeterministically()
    {
        var first = RunHybridReconcileScenario();
        var second = RunHybridReconcileScenario();

        Assert.True(first.PredictedHashesRecorded >= 3);
        Assert.Equal(1, first.ReconcileMismatches);
        Assert.Equal(1, first.Rollbacks);
        Assert.Equal(1, first.LastRollbackFrame);
        Assert.False(first.IsReplaying);
        Assert.Equal(first.ConfirmedFrame, first.PredictedFrame);
        Assert.Equal(3, first.PredictedFrame);
        Assert.Equal("6", first.Result.FinalStateFingerprint);
        Assert.Contains(first.Result.StateTrace, entry =>
            entry.StartsWith("frame:2@40:", StringComparison.Ordinal) &&
            entry.Contains("rollback=1", StringComparison.Ordinal));
        Assert.Equal(first.Result.StateTrace, second.Result.StateTrace);
        Assert.Equal(first.Result.DeterminismFingerprint, second.Result.DeterminismFingerprint);
        Assert.Equal(first.Result.FinalStateFingerprint, second.Result.FinalStateFingerprint);
    }

    private static string[] RunTrace()
    {
        var trace = new List<string>();
        MobaVirtualFrameCarrier? carrier = null;
        carrier = new MobaVirtualFrameCarrier(
            new NetworkConditionScenario(new NetworkConditionProfile(20, 10, 0.25, 0.15, 0)),
            packet => trace.Add($"{packet.Frame.Value}@{carrier!.Link.NowMs}"),
            seed: 71);
        using (carrier)
        {
            var commands = Enumerable.Range(1, 40)
                .Select(frame => Packet(frame, frame));
            carrier.Play(commands, ResolveFrame, finishAtMs: 100);
        }

        return trace.ToArray();
    }

    private static SessionScenarioObservation RunSessionScenario()
    {
        var observed = new List<int>();
        long simulationAdvancedMs = 0;
        var plan = VirtualNetworkScenarioPlan.Compile(
            SessionCommands(),
            NetworkConditionProfile.Ideal);
        var result = new MobaVirtualFrameSessionRunner().Run(
            packet => observed.Add(packet.Frame.Value),
            plan,
            ResolveSessionFrame,
            seed: 47,
            timeoutMs: 1000,
            serializeFrame: packet => new ArraySegment<byte>(
                packet.Snapshot?.Payload ?? Array.Empty<byte>()),
            advanceSimulationByMs: deltaMs => simulationAdvancedMs += deltaMs,
            captureFinalState: () => string.Join(",", observed));

        return new SessionScenarioObservation(result, observed.ToArray(), simulationAdvancedMs);
    }

    private static HybridScenarioObservation RunHybridReconcileScenario()
    {
        const int simulationStepMs = 10;
        var worldId = new WorldId("virtual-hybrid-battle");
        var remote = new FrameJitterBuffer<PlayerInputCommand[]>(
            delayFrames: 0,
            MissingFrameMode.Wait,
            () => Array.Empty<PlayerInputCommand>());
        var worlds = new PredictionWorldManager();
        var runtimeOptions = new HostRuntimeOptions();
        var runtime = new HostRuntime(worlds, runtimeOptions);
        var prediction = new ClientPredictionDriverModule(
            _ => remote,
            _ => null,
            maxPredictionAheadFrames: 2,
            minPredictionWindow: 2,
            enableRollback: true,
            rollbackHistoryFrames: 16,
            rollbackCaptureEveryNFrames: 1,
            buildRollbackRegistry: world =>
            {
                var predictionWorld = (PredictionWorld)world;
                var registry = new RollbackRegistry();
                registry.Register(new PredictionStateRollbackProvider(predictionWorld));
                return registry;
            },
            buildComputeHash: world => _ =>
                new WorldStateHash((uint)((PredictionWorld)world).State));
        prediction.Install(runtime, runtimeOptions);
        var world = (PredictionWorld)runtime.CreateWorld(
            new WorldCreateOptions(worldId, "virtual-hybrid"));

        var plan = VirtualNetworkScenarioPlan.Compile(
            HybridCommands(),
            NetworkConditionProfile.Ideal);
        var result = new MobaVirtualFrameSessionRunner().Run(
            packet =>
            {
                remote.Add(packet.Frame.Value, packet.Inputs.ToArray());
                if (!packet.Snapshot.HasValue ||
                    packet.Snapshot.Value.OpCode != MobaOpCodes.Snapshot.StateHash)
                    return;

                var stateHash = MobaStateHashSnapshotCodec.Deserialize(
                    packet.Snapshot.Value.Payload);
                prediction.OnAuthoritativeStateHash(
                    packet.WorldId,
                    new FrameIndex(stateHash.Frame),
                    new WorldStateHash(stateHash.Hash));
            },
            plan,
            ResolveHybridFrame,
            seed: 83,
            timeoutMs: 1000,
            advanceSimulationByMs: deltaMs =>
            {
                Assert.Equal(0, deltaMs % simulationStepMs);
                for (var elapsed = 0L; elapsed < deltaMs; elapsed += simulationStepMs)
                    runtime.Tick(simulationStepMs / 1000f);
            },
            captureFinalState: () => world.State.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            captureSimulationState: () => CapturePredictionState(prediction, worldId, world));

        Assert.True(prediction.TryGetFrames(worldId, out var confirmed, out var predicted));
        return new HybridScenarioObservation(
            result,
            prediction.TotalPredictedHashRecorded,
            prediction.TotalReconcileMismatch,
            prediction.TotalRollbackCount,
            prediction.LastRollbackFrame.Value,
            prediction.IsReplaying,
            confirmed.Value,
            predicted.Value);
    }

    private static string CapturePredictionState(
        ClientPredictionDriverModule prediction,
        WorldId worldId,
        PredictionWorld world)
    {
        prediction.TryGetFrames(worldId, out var confirmed, out var predicted);
        return $"confirmed={confirmed.Value};predicted={predicted.Value};" +
               $"mismatch={prediction.TotalReconcileMismatch};" +
               $"rollback={prediction.TotalRollbackCount};" +
               $"replaying={prediction.IsReplaying};state={world.State}";
    }

    private static VirtualNetworkCommand[] HybridCommands() =>
    new[]
    {
        HybridPacket(0, 1, 1, authoritativeHash: 0),
        new VirtualNetworkCommand(10, VirtualNetworkScenarioPlayer.DisconnectCommand),
        HybridPacket(20, 2, 2, authoritativeHash: 0),
        HybridPacket(30, 3, 3, authoritativeHash: 0),
        new VirtualNetworkCommand(40, VirtualNetworkScenarioPlayer.ReconnectCommand),
        HybridPacket(40, 102, 2, authoritativeHash: 999),
        HybridPacket(40, 103, 3, authoritativeHash: 6),
        HybridPacket(70, 104, 4, authoritativeHash: 0),
    };

    private static VirtualNetworkCommand HybridPacket(
        int atMs,
        int sequence,
        int frame,
        uint authoritativeHash) => new(
        atMs,
        VirtualNetworkScenarioPlayer.PacketCommand,
        new Dictionary<string, string>
        {
            ["direction"] = "inbound",
            ["opCode"] = "5202",
            ["seq"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["hash"] = authoritativeHash.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    private static FramePacket ResolveHybridFrame(VirtualNetworkCommand command)
    {
        var frame = int.Parse(command.RequireParameter("frame"),
            System.Globalization.CultureInfo.InvariantCulture);
        var frameIndex = new FrameIndex(frame);
        var authoritativeHash = uint.Parse(command.RequireParameter("hash"),
            System.Globalization.CultureInfo.InvariantCulture);
        WorldStateSnapshot? snapshot = authoritativeHash != 0
            ? new WorldStateSnapshot(
                MobaOpCodes.Snapshot.StateHash,
                MobaStateHashSnapshotCodec.Serialize(frame, authoritativeHash))
            : null;
        return new FramePacket(
            new WorldId("virtual-hybrid-battle"),
            frameIndex,
            Array.Empty<PlayerInputCommand>(),
            snapshot);
    }

    private static VirtualNetworkCommand[] SessionCommands() =>
    new[]
    {
        new VirtualNetworkCommand(0, VirtualNetworkScenarioPlayer.PhaseCommand,
            new Dictionary<string, string>
            {
                ["until"] = "1000",
                ["direction"] = "inbound",
                ["opCode"] = "5202",
                ["latency"] = "10",
            }),
        SessionPacket(0, 1, 1),
        new VirtualNetworkCommand(15, VirtualNetworkScenarioPlayer.DisconnectCommand),
        SessionPacket(20, 2, 2),
        SessionPacket(30, 3, 3),
        new VirtualNetworkCommand(40, VirtualNetworkScenarioPlayer.ReconnectCommand),
        SessionPacket(40, 102, 2),
        SessionPacket(40, 103, 3),
        SessionPacket(50, 4, 4),
    };

    private static VirtualNetworkCommand SessionPacket(int atMs, int sequence, int frame) => new(
        atMs,
        VirtualNetworkScenarioPlayer.PacketCommand,
        new Dictionary<string, string>
        {
            ["direction"] = "inbound",
            ["opCode"] = "5202",
            ["seq"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    private static FramePacket ResolveSessionFrame(VirtualNetworkCommand command)
    {
        var frame = int.Parse(command.RequireParameter("frame"),
            System.Globalization.CultureInfo.InvariantCulture);
        var frameIndex = new FrameIndex(frame);
        return new FramePacket(
            new WorldId("virtual-session-battle"),
            frameIndex,
            new[]
            {
                new PlayerInputCommand(
                    frameIndex,
                    new PlayerId("p1"),
                    opCode: 4101,
                    payload: BitConverter.GetBytes(frame)),
            },
            new WorldStateSnapshot(opCode: 7101, payload: BitConverter.GetBytes(frame * 31)));
    }

    private static VirtualNetworkCommand[] Commands() =>
    new[]
    {
        Packet(0, 1),
        new(10, VirtualNetworkScenarioPlayer.DisconnectCommand),
        Packet(20, 2),
        new(50, VirtualNetworkScenarioPlayer.ReconnectCommand),
        Packet(60, 1),
        Packet(70, 3),
    };

    private static VirtualNetworkCommand Packet(int atMs, int frame) => new(
        atMs,
        VirtualNetworkScenarioPlayer.PacketCommand,
        new Dictionary<string, string>
        {
            ["direction"] = "inbound",
            ["opCode"] = "5202",
            ["seq"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    private static FramePacket ResolveFrame(VirtualNetworkCommand command)
    {
        var frame = int.Parse(command.RequireParameter("seq"),
            System.Globalization.CultureInfo.InvariantCulture);
        return new FramePacket(
            new WorldId("virtual-battle"),
            new FrameIndex(frame),
            Array.Empty<PlayerInputCommand>(),
            snapshot: null);
    }

    private sealed record SessionScenarioObservation(
        MobaVirtualFrameSessionRunResult Result,
        int[] ObservedFrames,
        long SimulationAdvancedMs);

    private sealed record HybridScenarioObservation(
        MobaVirtualFrameSessionRunResult Result,
        long PredictedHashesRecorded,
        long ReconcileMismatches,
        long Rollbacks,
        int LastRollbackFrame,
        bool IsReplaying,
        int ConfirmedFrame,
        int PredictedFrame);

    private sealed class PredictionWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> _worlds = new();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            var world = new PredictionWorld(options.Id, options.WorldType);
            _worlds.Add(world.Id, world);
            return world;
        }

        public bool TryGet(WorldId id, out IWorld world) =>
            _worlds.TryGetValue(id, out world!);

        public bool Destroy(WorldId id) => _worlds.Remove(id);

        public void Tick(float deltaTime)
        {
            foreach (var world in _worlds.Values) world.Tick(deltaTime);
        }

        public void DisposeAll() => _worlds.Clear();
    }

    private sealed class PredictionWorld : IWorld
    {
        private readonly PredictionInputSink _inputSink = new();

        public PredictionWorld(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
            Services = new PredictionWorldResolver(_inputSink, new FrameTime());
        }

        public WorldId Id { get; }
        public string WorldType { get; }
        public IWorldResolver Services { get; }
        public int State { get; set; }

        public void Initialize() { }

        public void Tick(float deltaTime)
        {
            State += _inputSink.LastSubmittedFrame;
        }

        public void Dispose() { }
    }

    private sealed class PredictionInputSink : IWorldInputSink
    {
        public int LastSubmittedFrame { get; private set; }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            LastSubmittedFrame = frame.Value;
        }

        public void Dispose() { }
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

        public object Resolve(Type serviceType) =>
            TryResolve(serviceType, out var instance)
                ? instance
                : throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public bool TryResolve(Type serviceType, out object instance)
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

    private sealed class PredictionStateRollbackProvider : IRollbackStateProvider
    {
        private readonly PredictionWorld _world;

        public PredictionStateRollbackProvider(PredictionWorld world)
        {
            _world = world;
        }

        public int Key => 91001;

        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(_world.State);

        public void Import(FrameIndex frame, byte[] payload)
        {
            _world.State = BitConverter.ToInt32(payload, 0);
        }
    }
}
