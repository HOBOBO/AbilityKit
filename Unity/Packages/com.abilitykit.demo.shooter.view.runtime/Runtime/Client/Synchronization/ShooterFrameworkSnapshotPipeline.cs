#nullable enable

using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public readonly struct ShooterFrameworkSnapshotPipelineDiagnostics
    {
        public ShooterFrameworkSnapshotPipelineDiagnostics(
            int packetCount,
            int dispatchedSnapshotCount,
            int packedSnapshotCount,
            int pureStateSnapshotCount,
            int lastFrame,
            int lastPayloadOpCode,
            string lastWorldId)
        {
            PacketCount = packetCount;
            DispatchedSnapshotCount = dispatchedSnapshotCount;
            PackedSnapshotCount = packedSnapshotCount;
            PureStateSnapshotCount = pureStateSnapshotCount;
            LastFrame = lastFrame;
            LastPayloadOpCode = lastPayloadOpCode;
            LastWorldId = lastWorldId ?? string.Empty;
        }

        public int PacketCount { get; }
        public int DispatchedSnapshotCount { get; }
        public int PackedSnapshotCount { get; }
        public int PureStateSnapshotCount { get; }
        public int LastFrame { get; }
        public int LastPayloadOpCode { get; }
        public string LastWorldId { get; }
    }

    public sealed class ShooterFrameworkSnapshotPipeline : IDisposable
    {
        private readonly RemoteFrameAggregator _aggregator = new RemoteFrameAggregator();
        private readonly FrameSnapshotDispatcher _dispatcher = new FrameSnapshotDispatcher();
        private readonly SnapshotPipeline _pipeline;
        private readonly SnapshotApplyContext? _applyContext;
        private readonly IDisposable _packedFullStage;
        private readonly IDisposable _packedDeltaStage;
        private readonly IDisposable _pureStateFullStage;
        private readonly IDisposable _pureStateDeltaStage;
        private int _packetCount;
        private int _dispatchedSnapshotCount;
        private int _packedSnapshotCount;
        private int _pureStateSnapshotCount;
        private int _lastFrame;
        private int _lastPayloadOpCode;
        private string _lastWorldId = string.Empty;

        public ShooterFrameworkSnapshotPipeline()
            : this(null, null)
        {
        }

        public ShooterFrameworkSnapshotPipeline(IShooterBattleRuntimePort? runtime, ShooterPresentationFacade? presentation)
        {
            _applyContext = runtime != null && presentation != null
                ? new SnapshotApplyContext(runtime, presentation)
                : null;
            _dispatcher.SnapshotReceived += OnSnapshotReceived;
            _pipeline = new SnapshotPipeline((object?)_applyContext ?? this, _dispatcher);
            RegisterRoutes(_pipeline);
            _packedFullStage = _pipeline.AddStage<ShooterPackedSnapshotPayload>(
                ShooterOpCodes.Snapshot.PackedState,
                0,
                HandlePackedSnapshotStage);
            _packedDeltaStage = _pipeline.AddStage<ShooterPackedSnapshotPayload>(
                ShooterOpCodes.Snapshot.PackedStateDelta,
                0,
                HandlePackedSnapshotStage);
            _pureStateFullStage = _pipeline.AddStage<ShooterPureStateSnapshotPayload>(
                ShooterOpCodes.Snapshot.PureState,
                0,
                HandlePureStateSnapshotStage);
            _pureStateDeltaStage = _pipeline.AddStage<ShooterPureStateSnapshotPayload>(
                ShooterOpCodes.Snapshot.PureStateDelta,
                0,
                HandlePureStateSnapshotStage);
        }

        public ShooterFrameworkSnapshotPipelineDiagnostics Diagnostics => new ShooterFrameworkSnapshotPipelineDiagnostics(
            _packetCount,
            _dispatchedSnapshotCount,
            _packedSnapshotCount,
            _pureStateSnapshotCount,
            _lastFrame,
            _lastPayloadOpCode,
            _lastWorldId);

        public int LastAppliedFrame => _applyContext?.LastAppliedFrame ?? 0;
        public uint LastAppliedStateHash => _applyContext?.LastAppliedStateHash ?? 0u;
        public uint LastAppliedSnapshotFlags => _applyContext?.LastAppliedSnapshotFlags ?? 0u;
        public int LastIgnoredFrame => _applyContext?.LastIgnoredFrame ?? -1;

        public FramePacket FeedGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var packet = ToFramePacket(in snapshot);
            _packetCount++;
            _lastFrame = packet.Frame.Value;
            _lastPayloadOpCode = snapshot.PayloadOpCode;
            _lastWorldId = packet.WorldId.Value ?? string.Empty;
            _aggregator.AddPacket(packet);
            _dispatcher.Feed(packet);
            return packet;
        }

        public ShooterSnapshotApplyResult ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            if (_applyContext == null)
            {
                FeedGatewaySnapshot(in snapshot);
                return ShooterSnapshotApplyResult.Ignored;
            }

            _applyContext.Begin(snapshot);
            FeedGatewaySnapshot(in snapshot);
            return _applyContext.Complete(snapshot);
        }

        public RemoteSnapshotFrame BuildSnapshotFrame(int frame)
        {
            return _aggregator.BuildSnapshotFrame(new FrameIndex(frame));
        }

        public void TrimBefore(int minFrameInclusive)
        {
            _aggregator.TrimBefore(minFrameInclusive);
        }

        public void Clear()
        {
            _aggregator.Clear();
            _packetCount = 0;
            _dispatchedSnapshotCount = 0;
            _packedSnapshotCount = 0;
            _pureStateSnapshotCount = 0;
            _lastFrame = 0;
            _lastPayloadOpCode = 0;
            _lastWorldId = string.Empty;
            _applyContext?.Clear();
        }

        public void Dispose()
        {
            _packedFullStage.Dispose();
            _packedDeltaStage.Dispose();
            _pureStateFullStage.Dispose();
            _pureStateDeltaStage.Dispose();
            _dispatcher.SnapshotReceived -= OnSnapshotReceived;
            _pipeline.Dispose();
            _dispatcher.Dispose();
        }

        public static FramePacket ToFramePacket(in ShooterGatewaySnapshot snapshot)
        {
            return new FramePacket(
                ToFrameworkWorldId(snapshot.WorldId),
                new FrameIndex(snapshot.Frame),
                Array.Empty<PlayerInputCommand>(),
                new WorldStateSnapshot(snapshot.PayloadOpCode, ResolvePayloadBytes(in snapshot)));
        }

        public static WorldId ToFrameworkWorldId(ulong shooterWorldId)
        {
            return new WorldId($"shooter:{shooterWorldId}");
        }

        private static void RegisterRoutes(SnapshotPipeline pipeline)
        {
            pipeline.Register<ShooterPackedSnapshotPayload>(
                ShooterOpCodes.Snapshot.PackedState,
                TryDecodePackedSnapshot);
            pipeline.Register<ShooterPackedSnapshotPayload>(
                ShooterOpCodes.Snapshot.PackedStateDelta,
                TryDecodePackedSnapshot);
            pipeline.Register<ShooterPureStateSnapshotPayload>(
                ShooterOpCodes.Snapshot.PureState,
                TryDecodePureStateSnapshot);
            pipeline.Register<ShooterPureStateSnapshotPayload>(
                ShooterOpCodes.Snapshot.PureStateDelta,
                TryDecodePureStateSnapshot);
        }

        private static byte[] ResolvePayloadBytes(in ShooterGatewaySnapshot snapshot)
        {
            if (snapshot.PayloadBytes != null && snapshot.PayloadBytes.Length > 0)
            {
                return snapshot.PayloadBytes;
            }

            if (snapshot.PackedSnapshot.HasValue)
            {
                var packed = snapshot.PackedSnapshot.Value;
                return ShooterPackedSnapshotCodec.Serialize(in packed);
            }

            if (snapshot.PureStateSnapshot.HasValue)
            {
                var pureState = snapshot.PureStateSnapshot.Value;
                return ShooterPureStateSyncCodec.Serialize(in pureState);
            }

            return Array.Empty<byte>();
        }

        private static bool TryDecodePackedSnapshot(in WorldStateSnapshot snap, out ShooterPackedSnapshotPayload value)
        {
            value = default;
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                return false;
            }

            value = ShooterPackedSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        private static bool TryDecodePureStateSnapshot(in WorldStateSnapshot snap, out ShooterPureStateSnapshotPayload value)
        {
            value = default;
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                return false;
            }

            value = ShooterPureStateSyncCodec.Deserialize(snap.Payload);
            return true;
        }

        private void OnSnapshotReceived(ISnapshotEnvelope envelope, WorldStateSnapshot snapshot)
        {
            _dispatchedSnapshotCount++;
        }

        private void HandlePackedSnapshotStage(object ctx, ISnapshotEnvelope envelope, ShooterPackedSnapshotPayload snapshot)
        {
            _packedSnapshotCount++;
            if (ctx is SnapshotApplyContext applyContext)
            {
                applyContext.ApplyPacked(in snapshot);
            }
        }

        private void HandlePureStateSnapshotStage(object ctx, ISnapshotEnvelope envelope, ShooterPureStateSnapshotPayload snapshot)
        {
            _pureStateSnapshotCount++;
            if (ctx is SnapshotApplyContext applyContext)
            {
                applyContext.ApplyPureState(in snapshot);
            }
        }

        private sealed class SnapshotApplyContext
        {
            private readonly IShooterBattleRuntimePort _runtime;
            private readonly ShooterPresentationFacade _presentation;
            private ShooterGatewaySnapshot _currentSnapshot;
            private bool _stageApplied;
            private ShooterSnapshotApplyResult _stageResult;
            private bool _hasAppliedSnapshot;

            public SnapshotApplyContext(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation)
            {
                _runtime = runtime;
                _presentation = presentation;
            }

            public int LastAppliedFrame { get; private set; }
            public uint LastAppliedStateHash { get; private set; }
            public uint LastAppliedSnapshotFlags { get; private set; }
            public int LastIgnoredFrame { get; private set; } = -1;

            public void Begin(in ShooterGatewaySnapshot snapshot)
            {
                _currentSnapshot = snapshot;
                _stageApplied = false;
                _stageResult = ShooterSnapshotApplyResult.Ignored;
            }

            public void ApplyPacked(in ShooterPackedSnapshotPayload packed)
            {
                var compatibility = ShooterStateSyncCompatibilityPolicy.EvaluatePacked(packed.Version);
                if (!compatibility.IsCompatible)
                {
                    _stageApplied = true;
                    _stageResult = ShooterSnapshotApplyResult.UnsupportedVersion;
                    return;
                }

                var snapshotFrame = packed.Frame;
                if (_hasAppliedSnapshot && snapshotFrame <= LastAppliedFrame)
                {
                    LastIgnoredFrame = snapshotFrame;
                    _stageApplied = true;
                    _stageResult = ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
                    return;
                }

                if (!_runtime.ImportPackedSnapshot(in packed))
                {
                    _stageApplied = true;
                    _stageResult = ShooterSnapshotApplyResult.ImportFailed;
                    return;
                }

                LastAppliedFrame = packed.Frame;
                LastAppliedStateHash = packed.StateHash;
                LastAppliedSnapshotFlags = packed.SnapshotFlags;
                _hasAppliedSnapshot = true;
                _presentation.ApplyInterpolatedGatewaySnapshot(in _currentSnapshot);
                _stageApplied = true;
                _stageResult = ShooterSnapshotApplyResult.AppliedPackedSnapshot;
            }

            public void ApplyPureState(in ShooterPureStateSnapshotPayload pureState)
            {
                var result = _presentation.ApplyPureStateGatewaySnapshot(in _currentSnapshot);
                _stageApplied = true;
                _stageResult = ShooterSnapshotApplyResults.FromPureStateResult(result);
            }

            public ShooterSnapshotApplyResult Complete(in ShooterGatewaySnapshot snapshot)
            {
                if (_stageApplied)
                {
                    return _stageResult;
                }

                var snapshotFrame = snapshot.PackedSnapshot.HasValue
                    ? snapshot.PackedSnapshot.Value.Frame
                    : snapshot.Frame;
                if (_hasAppliedSnapshot && snapshotFrame <= LastAppliedFrame)
                {
                    LastIgnoredFrame = snapshotFrame;
                    return ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
                }

                if (!snapshot.PackedSnapshot.HasValue && !snapshot.PureStateSnapshot.HasValue)
                {
                    _presentation.ApplyInterpolatedGatewaySnapshot(in snapshot);
                    LastAppliedFrame = snapshot.Frame;
                    LastAppliedStateHash = 0u;
                    LastAppliedSnapshotFlags = 0u;
                    _hasAppliedSnapshot = true;
                    return ShooterSnapshotApplyResult.AppliedActorSnapshot;
                }

                return ShooterSnapshotApplyResult.Ignored;
            }

            public void Clear()
            {
                _stageApplied = false;
                _stageResult = ShooterSnapshotApplyResult.Ignored;
                _hasAppliedSnapshot = false;
                LastAppliedFrame = 0;
                LastAppliedStateHash = 0u;
                LastAppliedSnapshotFlags = 0u;
                LastIgnoredFrame = -1;
            }
        }
    }
}
