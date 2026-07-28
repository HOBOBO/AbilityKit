using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterPureStateSnapshotSyncController
    {
        private static readonly SyncHealthEvent[] EmptyHealthEvents = Array.Empty<SyncHealthEvent>();

        private readonly Action<ShooterPureStateSnapshotPayload> _applySnapshot;
        private readonly ShooterGatewaySnapshotDecoder _decoder;
        private readonly SnapshotStreamStateMachine _snapshotStream = new SnapshotStreamStateMachine(
            ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
            ShooterPureStateSyncCodec.CurrentVersion);
        private SyncHealthEvent[] _lastHealthEvents = EmptyHealthEvents;

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
        }

        public int LastAppliedFrame => _snapshotStream.LastAppliedFrame;

        public uint LastAppliedStateHash => _snapshotStream.LastAppliedStateHash;

        public int LastAppliedSnapshotKind { get; private set; }

        public int LastBaselineFrame => _snapshotStream.LastBaselineFrame;

        public uint LastBaselineHash => _snapshotStream.LastBaselineHash;

        public bool NeedsFullBaselineResync => _snapshotStream.NeedsFullBaselineRecovery;

        public IReadOnlyList<SyncHealthEvent> LastHealthEvents => _lastHealthEvents;

        public ShooterPureStateResyncReason LastResyncReason => ToShooterResyncReason(_snapshotStream.LastRecoveryReason);

        public int LastIgnoredFrame => _snapshotStream.LastIgnoredFrame;

        public int LastResyncFrame => _snapshotStream.LastRecoveryFrame;

        public uint LastResyncStateHash => _snapshotStream.LastRecoveryStateHash;

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
            var envelope = CreateStreamEnvelope(in pureState);
            var validation = _snapshotStream.Validate(in envelope);
            if (validation.Status == SnapshotStreamValidationStatus.IgnoredDuplicate ||
                validation.Status == SnapshotStreamValidationStatus.IgnoredStale)
            {
                return CompleteRejectedSnapshot(
                    ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot,
                    SyncHealthEvent.Warning(SyncHealthEventKind.SnapshotStale, pureState.Frame, LastAppliedFrame),
                    in pureState);
            }

            if (validation.Status == SnapshotStreamValidationStatus.UnsupportedVersion)
            {
                return CompleteRejectedSnapshot(
                    ShooterPureStateSnapshotApplyResult.UnsupportedVersion,
                    SyncHealthEvent.Warning(SyncHealthEventKind.SnapshotDropped, pureState.Frame, pureState.Version),
                    in pureState);
            }

            if (validation.NeedsFullBaseline)
            {
                return CompleteRejectedSnapshot(
                    ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync,
                    SyncHealthEvent.Info(SyncHealthEventKind.FullSnapshotRequested, pureState.Frame, (long)LastResyncReason),
                    in pureState);
            }

            _applySnapshot(pureState);
            _snapshotStream.CommitApplied(in validation);
            LastAppliedSnapshotKind = pureState.SnapshotKind;
            if (envelope.IsFullBaseline)
            {
                SetHealthEvents(
                    SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, pureState.Frame, pureState.EffectiveEntityCount),
                    SyncHealthEvent.Info(SyncHealthEventKind.FullSnapshotApplied, pureState.Frame, pureState.BaselineFrame));
            }
            else
            {
                SetHealthEvents(SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, pureState.Frame, pureState.EffectiveEntityCount));
            }

            var result = envelope.IsFullBaseline
                ? ShooterPureStateSnapshotApplyResult.AppliedFullBaseline
                : ShooterPureStateSnapshotApplyResult.AppliedDelta;
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

        private ShooterPureStateSnapshotApplyResult CompleteRejectedSnapshot(
            ShooterPureStateSnapshotApplyResult result,
            SyncHealthEvent healthEvent,
            in ShooterPureStateSnapshotPayload pureState)
        {
            SetHealthEvents(healthEvent);
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
                case SnapshotStreamRecoveryReason.None:
                default:
                    return ShooterPureStateResyncReason.None;
            }
        }

        private void ClearHealthEvents()
        {
            _lastHealthEvents = EmptyHealthEvents;
        }

        private void SetHealthEvents(params SyncHealthEvent[] events)
        {
            _lastHealthEvents = events == null || events.Length == 0 ? EmptyHealthEvents : events;
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
        UnsupportedVersion = 4
    }
}
