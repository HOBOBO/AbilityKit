#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Scenario;

namespace AbilityKit.Game.Battle.Testing
{

    public sealed class BattleFlowPredictionRunResult
    {
        public string BackendId { get; set; } = string.Empty;
        public string[] StateTrace { get; set; } = Array.Empty<string>();
        public string DeterminismFingerprint { get; set; } = string.Empty;
        public string FinalStateHash { get; set; } = string.Empty;
        public long PredictedHashes { get; set; }
        public long Mismatches { get; set; }
        public long Rollbacks { get; set; }
        public int MismatchFrame { get; set; } = -1;
        public int RollbackFrame { get; set; } = -1;
        public int ConfirmedFrame { get; set; }
        public int PredictedFrame { get; set; }
        public bool WasReplaying { get; set; }
        public bool IsReplaying { get; set; }
    }

    public sealed class MobaPredictionAssertionResult
    {
        public bool Passed => Failures.Length == 0;
        public string[] Failures { get; set; } = Array.Empty<string>();
    }

    /// <summary>Runs BattleFlow authoritative frame commands through the real prediction driver.</summary>
    public static class MobaBattleFlowPredictionScenarioRunner
    {
        public static IMobaPredictionScenarioBackend ResolveBackend(
            TestScenario scenario,
            IMobaPredictionScenarioBackend? backend = null)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            var requested = MobaPredictionBackendIds.Normalize(
                (scenario.Expectations as MobaBattleFlowAssertions)?.PredictionBackend ??
                MobaPredictionBackendIds.Headless);
            if (backend == null && requested == MobaPredictionBackendIds.Headless)
                return new HeadlessMobaPredictionScenarioBackend();
            if (backend == null)
                throw new NotSupportedException(
                    $"Sync backend '{requested}' requires a Unity execution host; no headless fallback is allowed.");
            if (!string.Equals(backend.Id, requested, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Scenario requests sync backend '{requested}', but execution host supplied '{backend.Id}'.");
            return backend;
        }

        public static BattleFlowPredictionRunResult Run(
            TestScenario scenario,
            IMobaPredictionScenarioBackend? backend = null)
        {
            var selectedBackend = ResolveBackend(scenario, backend);
            var tickRate = scenario.TickRate > 0 ? scenario.TickRate : 30;
            var worldId = new WorldId("battleflow-prediction:" + scenario.CaseId);
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
                rollbackHistoryFrames: 256,
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
            try
            {
                var world = (PredictionWorld)runtime.CreateWorld(
                    new WorldCreateOptions(worldId, "battleflow-prediction"));
                using var frameRoute = selectedBackend.CreateRoute(prediction, worldId, tickRate);

                long tickUnits = 0;
                long observedMismatches = 0;
                long observedRollbacks = 0;
                var mismatchFrame = -1;
                var rollbackFrame = -1;
                var wasReplaying = false;

                string CaptureState()
                {
                    prediction.TryGetFrames(worldId, out var confirmed, out var predicted);
                    if (prediction.TotalReconcileMismatch > observedMismatches)
                    {
                        observedMismatches = prediction.TotalReconcileMismatch;
                        mismatchFrame = prediction.LastReconcileMismatchFrame.Value;
                    }

                    if (prediction.TotalRollbackCount > observedRollbacks)
                    {
                        observedRollbacks = prediction.TotalRollbackCount;
                        rollbackFrame = prediction.LastRollbackFrame.Value;
                    }

                    wasReplaying |= prediction.IsReplaying;
                    return $"confirmed={confirmed.Value};predicted={predicted.Value};" +
                           $"mismatch={prediction.TotalReconcileMismatch};" +
                           $"rollback={prediction.TotalRollbackCount};" +
                           $"replaying={prediction.IsReplaying};state={world.State}";
                }

                var commands = scenario.Commands.Select(command => new VirtualNetworkCommand(
                    command.AtMs,
                    command.Name,
                    command.Parameters));
                var plan = VirtualNetworkScenarioPlan.Compile(commands, NetworkConditionProfile.Ideal);
                var sessionResult = new MobaVirtualFrameSessionRunner().Run(
                    packet =>
                    {
                        remote.Add(packet.Frame.Value, packet.Inputs.ToArray());
                        frameRoute.Feed(packet);
                    },
                    plan,
                    command => ResolveFrame(command, worldId),
                    scenario.Seed,
                    scenario.TimeoutMs,
                    serializeFrame: packet => new ArraySegment<byte>(
                        packet.Snapshot?.Payload ?? Array.Empty<byte>()),
                    advanceSimulationByMs: deltaMs =>
                    {
                        tickUnits += checked(deltaMs * tickRate);
                        while (tickUnits >= 1000)
                        {
                            runtime.Tick(1f / tickRate);
                            tickUnits -= 1000;
                        }
                    },
                    captureFinalState: () => world.State.ToString(CultureInfo.InvariantCulture),
                    captureSimulationState: CaptureState);

                CaptureState();
                prediction.TryGetFrames(worldId, out var finalConfirmed, out var finalPredicted);
                return new BattleFlowPredictionRunResult
                {
                    BackendId = selectedBackend.Id,
                    StateTrace = sessionResult.StateTrace,
                    DeterminismFingerprint = ComputeBackendFingerprint(
                        selectedBackend.Id, sessionResult.DeterminismFingerprint),
                    FinalStateHash = sessionResult.FinalStateFingerprint,
                    PredictedHashes = prediction.TotalPredictedHashRecorded,
                    Mismatches = prediction.TotalReconcileMismatch,
                    Rollbacks = prediction.TotalRollbackCount,
                    MismatchFrame = mismatchFrame,
                    RollbackFrame = rollbackFrame,
                    ConfirmedFrame = finalConfirmed.Value,
                    PredictedFrame = finalPredicted.Value,
                    WasReplaying = wasReplaying,
                    IsReplaying = prediction.IsReplaying,
                };
            }
            finally
            {
                try { runtime.DestroyWorld(worldId); }
                finally { prediction.Uninstall(runtime, runtimeOptions); }
            }
        }

        private static string ComputeBackendFingerprint(string backendId, string fingerprint)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(backendId + "\n" + fingerprint));
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        public static MobaPredictionAssertionResult Verify(
            IReadOnlyList<MobaPredictionAssertion> assertions,
            BattleFlowPredictionRunResult observation)
        {
            var failures = new List<string>();
            foreach (var assertion in assertions ?? Array.Empty<MobaPredictionAssertion>())
            {
                var actual = ResolveProperty(assertion.Property, observation);
                if (!Compare(actual, assertion.Comparator, assertion.ExpectedValue))
                    failures.Add(
                        $"prediction.{assertion.Property} expected {assertion.Comparator} " +
                        $"{assertion.ExpectedValue}, actual {actual}");
            }

            return new MobaPredictionAssertionResult { Failures = failures.ToArray() };
        }

        private static FramePacket ResolveFrame(VirtualNetworkCommand command, WorldId worldId)
        {
            var frame = ParseInt(command, "frame");
            var hash = ParseOptionalUInt(command, "hash");
            var frameIndex = new FrameIndex(frame);
            WorldStateSnapshot? snapshot = hash.HasValue
                ? new WorldStateSnapshot(
                    MobaOpCodes.Snapshot.StateHash,
                    MobaStateHashSnapshotCodec.Serialize(frame, hash.Value))
                : null;
            return new FramePacket(
                worldId,
                frameIndex,
                Array.Empty<PlayerInputCommand>(),
                snapshot);
        }

        private static int ParseInt(VirtualNetworkCommand command, string name)
        {
            var text = command.RequireParameter(name);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
                throw new InvalidOperationException(
                    $"Invalid {name} in {command.Name} atMs={command.AtMs}.");
            return value;
        }

        private static uint? ParseOptionalUInt(VirtualNetworkCommand command, string name)
        {
            string? text = null;
            foreach (var pair in command.Parameters)
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) text = pair.Value;
            if (text == null) return null;
            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException(
                    $"Invalid {name} in {command.Name} atMs={command.AtMs}.");
            return value;
        }

        private static string ResolveProperty(
            string property,
            BattleFlowPredictionRunResult observation)
        {
            return (property ?? string.Empty).ToLowerInvariant() switch
            {
                "predictedhashes" => observation.PredictedHashes.ToString(CultureInfo.InvariantCulture),
                "mismatch" or "mismatches" => observation.Mismatches.ToString(CultureInfo.InvariantCulture),
                "rollback" or "rollbacks" => observation.Rollbacks.ToString(CultureInfo.InvariantCulture),
                "mismatchframe" => observation.MismatchFrame.ToString(CultureInfo.InvariantCulture),
                "rollbackframe" => observation.RollbackFrame.ToString(CultureInfo.InvariantCulture),
                "confirmedframe" => observation.ConfirmedFrame.ToString(CultureInfo.InvariantCulture),
                "predictedframe" => observation.PredictedFrame.ToString(CultureInfo.InvariantCulture),
                "wasreplaying" => observation.WasReplaying.ToString().ToLowerInvariant(),
                "replaying" => observation.IsReplaying.ToString().ToLowerInvariant(),
                "finalhash" => observation.FinalStateHash,
                _ => throw new InvalidOperationException(
                    $"Unknown prediction assertion property '{property}'."),
            };
        }

        private static bool Compare(string actual, string comparator, string expected)
        {
            comparator = (comparator ?? "eq").ToLowerInvariant();
            if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualNumber) &&
                decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNumber))
            {
                return comparator switch
                {
                    "eq" => actualNumber == expectedNumber,
                    "ne" => actualNumber != expectedNumber,
                    "gt" => actualNumber > expectedNumber,
                    "gte" => actualNumber >= expectedNumber,
                    "lt" => actualNumber < expectedNumber,
                    "lte" => actualNumber <= expectedNumber,
                    _ => throw new InvalidOperationException($"Unknown comparator '{comparator}'."),
                };
            }

            return comparator switch
            {
                "eq" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "ne" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                _ => throw new InvalidOperationException(
                    $"Comparator '{comparator}' requires numeric values."),
            };
        }

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

            public bool TryGet(WorldId id, out IWorld world) => _worlds.TryGetValue(id, out world!);
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
            public void Tick(float deltaTime) => State += _inputSink.LastSubmittedFrame;
            public void Dispose() { }
        }

        private sealed class PredictionInputSink : IWorldInputSink
        {
            public int LastSubmittedFrame { get; private set; }
            public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
                LastSubmittedFrame = frame.Value;
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

            public object Resolve(Type serviceType) => TryResolve(serviceType, out var instance)
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
                if (TryResolve(typeof(T), out var value))
                {
                    instance = (T)value;
                    return true;
                }
                instance = default!;
                return false;
            }
        }

        private sealed class PredictionStateRollbackProvider : IRollbackStateProvider
        {
            private readonly PredictionWorld _world;
            public PredictionStateRollbackProvider(PredictionWorld world) => _world = world;
            public int Key => 91001;
            public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(_world.State);
            public void Import(FrameIndex frame, byte[] payload) =>
                _world.State = BitConverter.ToInt32(payload, 0);
        }
    }
    }
