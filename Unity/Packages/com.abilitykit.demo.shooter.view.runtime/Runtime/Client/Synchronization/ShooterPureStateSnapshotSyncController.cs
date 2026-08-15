using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterPureStateSnapshotSyncController
    {
        private static readonly SyncHealthEvent[] EmptyHealthEvents = Array.Empty<SyncHealthEvent>();
        private static readonly NetworkSyncProfile PureStatePlaybackProfile = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.DeltaSnapshot | SnapshotPolicy.FixedRateStateStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);
        private static readonly NetworkSyncCapabilities PureStatePlaybackCapabilities =
            NetworkSyncCapabilities.FromProfile(
                in PureStatePlaybackProfile,
                ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
                ShooterPureStateSyncCodec.CurrentVersion);

        private readonly Action<ShooterPureStateSnapshotPayload> _applySnapshot;
        private readonly ShooterGatewaySnapshotDecoder _decoder;
        private readonly ClientSnapshotSyncPipeline<ShooterPureStateSnapshotPayload> _pipeline;
        private IReadOnlyList<SyncHealthEvent> _lastHealthEvents = EmptyHealthEvents;

        public ShooterPureStateSnapshotSyncController(ShooterPresentationFacade presentation)
            : this(presentation, new ShooterGatewaySnapshotDecoder())
        {
        }

        public ShooterPureStateSnapshotSyncController(ShooterPresentationFacade presentation, ShooterGatewaySnapshotDecoder decoder)
            : this(snapshot => (presentation ?? throw new ArgumentNullException(nameof(presentation))).ApplyPureStateSnapshot(in snapshot), decoder)
        {
        }

        public ShooterPureStateSnapshotSyncController(Action<ShooterPureStateSnapshotPayload> applySnapshot, ShooterGatewaySnapshotDecoder decoder)
        {
            _applySnapshot = applySnapshot ?? throw new ArgumentNullException(nameof(applySnapshot));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _pipeline = new ClientSnapshotSyncPipeline<ShooterPureStateSnapshotPayload>(
                new ClientSnapshotSyncOptions<ShooterPureStateSnapshotPayload>(
                    ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
                    ShooterPureStateSyncCodec.CurrentVersion,
                    CreateStreamEnvelope,
                    ApplyPresentationSnapshot)
                {
                    MaximumSequenceAdvance = ResolveMaximumSequenceAdvance,
                    EntityCount = GetEntityCount,
                    // PureState 管线只声明自身负责的权威快照播放能力，不继承外层会话的输入策略。
                    RequiredProfile = PureStatePlaybackProfile,
                    AvailableCapabilities = PureStatePlaybackCapabilities
                });
        }

        /// <summary>PureState 快照管线完成的 Profile、能力与版本协商结果。</summary>
        public NetworkSyncNegotiationResult? Negotiation => _pipeline.Negotiation;

        public int LastAppliedFrame => _pipeline.State.LastAppliedFrame;

        public uint LastAppliedStateHash => _pipeline.State.LastAppliedStateHash;

        public int LastAppliedSnapshotKind { get; private set; }

        public int LastBaselineFrame => _pipeline.State.LastBaselineFrame;

        public uint LastBaselineHash => _pipeline.State.LastBaselineHash;

        public bool NeedsFullBaselineResync => _pipeline.State.NeedsFullBaselineRecovery;

        public IReadOnlyList<SyncHealthEvent> LastHealthEvents => _lastHealthEvents;

        public ShooterPureStateResyncReason LastResyncReason => ToShooterResyncReason(_pipeline.State.LastRecoveryReason);

        public int LastIgnoredFrame => _pipeline.State.LastIgnoredFrame;

        public int LastResyncFrame => _pipeline.State.LastRecoveryFrame;

        public uint LastResyncStateHash => _pipeline.State.LastRecoveryStateHash;

        public ShooterPureStateSyncDiagnostics LastDiagnostics { get; private set; }

        public ShooterPureStateSnapshotApplyResult TryApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (!_decoder.IsSnapshotPush(opCode))
            {
                ClearHealthEvents();
                LastDiagnostics = ShooterPureStateSyncDiagnostics.Ignored(LastAppliedFrame, LastAppliedStateHash, NeedsFullBaselineResync, LastResyncReason);
                return ShooterPureStateSnapshotApplyResult.Ignored;
            }

            var snapshot = _decoder.Decode(payload);
            return ApplyGatewaySnapshot(in snapshot);
        }

        public ShooterPureStateSnapshotApplyResult ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            if (!snapshot.PureStateSnapshot.HasValue)
            {
                ClearHealthEvents();
                LastDiagnostics = ShooterPureStateSyncDiagnostics.Ignored(LastAppliedFrame, LastAppliedStateHash, NeedsFullBaselineResync, LastResyncReason);
                return ShooterPureStateSnapshotApplyResult.Ignored;
            }

            var pureState = snapshot.PureStateSnapshot.Value;
            var pipelineResult = _pipeline.Apply(in pureState);
            _lastHealthEvents = pipelineResult.HealthEvents;
            var result = ToShooterApplyResult(pipelineResult.Status);
            if (pipelineResult.Applied)
            {
                LastAppliedSnapshotKind = pureState.SnapshotKind;
            }

            LastDiagnostics = ShooterPureStateSyncDiagnostics.FromSnapshot(
                result,
                in pureState,
                LastAppliedFrame,
                LastAppliedStateHash,
                NeedsFullBaselineResync,
                LastResyncReason,
                LastResyncFrame,
                LastResyncStateHash,
                LastIgnoredFrame);
            return result;
        }

        private void ApplyPresentationSnapshot(in ShooterPureStateSnapshotPayload snapshot)
        {
            _applySnapshot(snapshot);
        }

        private static int ResolveMaximumSequenceAdvance(in ShooterPureStateSnapshotPayload snapshot)
        {
            return snapshot.Settings.DeltaIntervalFrames > 0
                ? snapshot.Settings.DeltaIntervalFrames
                : ShooterPureStateSyncSettings.Default.DeltaIntervalFrames;
        }

        private static int GetEntityCount(in ShooterPureStateSnapshotPayload snapshot)
        {
            return snapshot.EffectiveEntityCount;
        }

        private static SnapshotStreamEnvelope CreateStreamEnvelope(in ShooterPureStateSnapshotPayload pureState)
        {
            return new SnapshotStreamEnvelope(
                pureState.WorldId,
                pureState.Version,
                pureState.Frame,
                pureState.Frame,
                pureState.SnapshotKind == ShooterPureStateSnapshotKinds.FullBaseline
                    ? SnapshotStreamSnapshotKind.FullBaseline
                    : SnapshotStreamSnapshotKind.Delta,
                pureState.BaselineFrame,
                pureState.BaselineHash,
                pureState.StateHash);
        }

        private static ShooterPureStateResyncReason ToShooterResyncReason(SnapshotStreamRecoveryReason reason)
        {
            switch (reason)
            {
                case SnapshotStreamRecoveryReason.MissingBaseline:
                    return ShooterPureStateResyncReason.MissingBaseline;
                case SnapshotStreamRecoveryReason.BaselineMismatch:
                    return ShooterPureStateResyncReason.BaselineMismatch;
                case SnapshotStreamRecoveryReason.WorldChanged:
                    return ShooterPureStateResyncReason.WorldChanged;
                case SnapshotStreamRecoveryReason.UnsupportedVersion:
                    return ShooterPureStateResyncReason.UnsupportedVersion;
                case SnapshotStreamRecoveryReason.SequenceGap:
                    return ShooterPureStateResyncReason.SequenceGap;
                case SnapshotStreamRecoveryReason.None:
                default:
                    return ShooterPureStateResyncReason.None;
            }
        }

        private static ShooterPureStateSnapshotApplyResult ToShooterApplyResult(ClientSnapshotSyncStatus status)
        {
            switch (status)
            {
                case ClientSnapshotSyncStatus.AppliedFullBaseline:
                    return ShooterPureStateSnapshotApplyResult.AppliedFullBaseline;
                case ClientSnapshotSyncStatus.AppliedDelta:
                    return ShooterPureStateSnapshotApplyResult.AppliedDelta;
                case ClientSnapshotSyncStatus.IgnoredStale:
                    return ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot;
                case ClientSnapshotSyncStatus.NeedsFullBaseline:
                    return ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync;
                case ClientSnapshotSyncStatus.UnsupportedVersion:
                    return ShooterPureStateSnapshotApplyResult.UnsupportedVersion;
                default:
                    return ShooterPureStateSnapshotApplyResult.Ignored;
            }
        }

        private void ClearHealthEvents()
        {
            _lastHealthEvents = EmptyHealthEvents;
        }
    }
    
    public readonly struct ShooterPureStateSyncDiagnostics
    {
        public ShooterPureStateSyncDiagnostics(
            ShooterPureStateSnapshotApplyResult lastApplyResult,
            int sourceFrame,
            int sourceSnapshotKind,
            int sourceEntityCount,
            int sourceVisibilityHintCount,
            int sourceBaselineFrame,
            uint sourceBaselineHash,
            uint sourceStateHash,
            long sourceServerTick,
            int appliedFrame,
            uint appliedStateHash,
            bool needsFullBaselineResync,
            ShooterPureStateResyncReason lastResyncReason,
            int lastResyncFrame,
            uint lastResyncStateHash,
            int lastIgnoredFrame)
        {
            LastApplyResult = lastApplyResult;
            SourceFrame = sourceFrame;
            SourceSnapshotKind = sourceSnapshotKind;
            SourceEntityCount = sourceEntityCount;
            SourceVisibilityHintCount = sourceVisibilityHintCount;
            SourceBaselineFrame = sourceBaselineFrame;
            SourceBaselineHash = sourceBaselineHash;
            SourceStateHash = sourceStateHash;
            SourceServerTick = sourceServerTick;
            AppliedFrame = appliedFrame;
            AppliedStateHash = appliedStateHash;
            NeedsFullBaselineResync = needsFullBaselineResync;
            LastResyncReason = lastResyncReason;
            LastResyncFrame = lastResyncFrame;
            LastResyncStateHash = lastResyncStateHash;
            LastIgnoredFrame = lastIgnoredFrame;
        }

        public ShooterPureStateSnapshotApplyResult LastApplyResult { get; }
        public int SourceFrame { get; }
        public int SourceSnapshotKind { get; }
        public int SourceEntityCount { get; }
        public int SourceVisibilityHintCount { get; }
        public int SourceBaselineFrame { get; }
        public uint SourceBaselineHash { get; }
        public uint SourceStateHash { get; }
        public long SourceServerTick { get; }
        public int AppliedFrame { get; }
        public uint AppliedStateHash { get; }
        public bool NeedsFullBaselineResync { get; }
        public ShooterPureStateResyncReason LastResyncReason { get; }
        public int LastResyncFrame { get; }
        public uint LastResyncStateHash { get; }
        public int LastIgnoredFrame { get; }
        public bool HasSourceSnapshot => SourceFrame > 0 || SourceEntityCount > 0 || SourceVisibilityHintCount > 0;
        public bool AppliedPresentation => LastApplyResult == ShooterPureStateSnapshotApplyResult.AppliedFullBaseline || LastApplyResult == ShooterPureStateSnapshotApplyResult.AppliedDelta;

        public static ShooterPureStateSyncDiagnostics Ignored(int appliedFrame, uint appliedStateHash, bool needsFullBaselineResync, ShooterPureStateResyncReason lastResyncReason)
        {
            return new ShooterPureStateSyncDiagnostics(
                ShooterPureStateSnapshotApplyResult.Ignored,
                0,
                0,
                0,
                0,
                0,
                0u,
                0u,
                0L,
                appliedFrame,
                appliedStateHash,
                needsFullBaselineResync,
                lastResyncReason,
                0,
                0u,
                -1);
        }

        public static ShooterPureStateSyncDiagnostics FromSnapshot(
            ShooterPureStateSnapshotApplyResult result,
            in ShooterPureStateSnapshotPayload snapshot,
            int appliedFrame,
            uint appliedStateHash,
            bool needsFullBaselineResync,
            ShooterPureStateResyncReason lastResyncReason,
            int lastResyncFrame,
            uint lastResyncStateHash,
            int lastIgnoredFrame)
        {
            return new ShooterPureStateSyncDiagnostics(
                result,
                snapshot.Frame,
                snapshot.SnapshotKind,
                snapshot.EffectiveEntityCount,
                snapshot.EffectiveVisibilityHintCount,
                snapshot.BaselineFrame,
                snapshot.BaselineHash,
                snapshot.StateHash,
                snapshot.ServerTick,
                appliedFrame,
                appliedStateHash,
                needsFullBaselineResync,
                lastResyncReason,
                lastResyncFrame,
                lastResyncStateHash,
                lastIgnoredFrame);
        }
    }

    public enum ShooterPureStateSnapshotApplyResult
    {
        Ignored = 0,
        AppliedFullBaseline = 1,
        AppliedDelta = 2,
        IgnoredStaleSnapshot = 3,
        NeedsFullBaselineResync = 4,
        UnsupportedVersion = 5
    }

    public enum ShooterPureStateResyncReason
    {
        None = 0,
        MissingBaseline = 1,
        BaselineMismatch = 2,
        WorldChanged = 3,
        UnsupportedVersion = 4,
        SequenceGap = 5
    }
}
