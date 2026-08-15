#nullable enable

using System;

namespace AbilityKit.Network.Runtime.Sync
{
    public enum SnapshotStreamSnapshotKind
    {
        FullBaseline = 0,
        Delta = 1
    }

    public enum SnapshotStreamValidationStatus
    {
        AcceptedFullBaseline = 0,
        AcceptedDelta = 1,
        IgnoredDuplicate = 2,
        IgnoredStale = 3,
        MissingBaseline = 4,
        BaselineMismatch = 5,
        WorldChanged = 6,
        UnsupportedVersion = 7,
        SequenceGap = 8
    }

    public enum SnapshotStreamRecoveryReason
    {
        None = 0,
        MissingBaseline = 1,
        BaselineMismatch = 2,
        WorldChanged = 3,
        UnsupportedVersion = 4,
        SequenceGap = 5
    }

    public readonly struct SnapshotStreamEnvelope
    {
        public SnapshotStreamEnvelope(
            ulong worldId,
            int schemaVersion,
            long sequence,
            int frame,
            SnapshotStreamSnapshotKind snapshotKind,
            int baselineFrame,
            uint baselineHash,
            uint stateHash)
        {
            if (schemaVersion < 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (baselineFrame < 0) throw new ArgumentOutOfRangeException(nameof(baselineFrame));

            WorldId = worldId;
            SchemaVersion = schemaVersion;
            Sequence = sequence;
            Frame = frame;
            SnapshotKind = snapshotKind;
            BaselineFrame = baselineFrame;
            BaselineHash = baselineHash;
            StateHash = stateHash;
        }

        public ulong WorldId { get; }

        public int SchemaVersion { get; }

        public long Sequence { get; }

        public int Frame { get; }

        public SnapshotStreamSnapshotKind SnapshotKind { get; }

        public int BaselineFrame { get; }

        public uint BaselineHash { get; }

        public uint StateHash { get; }

        public bool IsFullBaseline => SnapshotKind == SnapshotStreamSnapshotKind.FullBaseline;
    }

    public readonly struct SnapshotStreamValidationResult
    {
        internal SnapshotStreamValidationResult(
            SnapshotStreamValidationStatus status,
            SnapshotStreamRecoveryReason recoveryReason,
            in SnapshotStreamEnvelope envelope,
            ulong currentWorldId,
            long lastAppliedSequence,
            int lastAppliedFrame,
            int gapCount,
            bool worldChanged)
        {
            Status = status;
            RecoveryReason = recoveryReason;
            Envelope = envelope;
            CurrentWorldId = currentWorldId;
            LastAppliedSequence = lastAppliedSequence;
            LastAppliedFrame = lastAppliedFrame;
            GapCount = gapCount;
            WorldChanged = worldChanged;
        }

        public SnapshotStreamValidationStatus Status { get; }

        public SnapshotStreamRecoveryReason RecoveryReason { get; }

        public SnapshotStreamEnvelope Envelope { get; }

        public ulong CurrentWorldId { get; }

        public long LastAppliedSequence { get; }

        public int LastAppliedFrame { get; }

        public int GapCount { get; }

        public bool WorldChanged { get; }

        public bool Accepted =>
            Status == SnapshotStreamValidationStatus.AcceptedFullBaseline ||
            Status == SnapshotStreamValidationStatus.AcceptedDelta;

        public bool NeedsFullBaseline =>
            Status == SnapshotStreamValidationStatus.MissingBaseline ||
            Status == SnapshotStreamValidationStatus.BaselineMismatch ||
            Status == SnapshotStreamValidationStatus.WorldChanged ||
            Status == SnapshotStreamValidationStatus.SequenceGap;
    }

    public sealed class SnapshotStreamStateMachine
    {
        private readonly int _minimumSupportedVersion;
        private readonly int _maximumSupportedVersion;
        private bool _hasAppliedSnapshot;
        private bool _hasBaseline;

        public SnapshotStreamStateMachine(int supportedVersion)
            : this(supportedVersion, supportedVersion)
        {
        }

        public SnapshotStreamStateMachine(int minimumSupportedVersion, int maximumSupportedVersion)
        {
            if (minimumSupportedVersion < 0) throw new ArgumentOutOfRangeException(nameof(minimumSupportedVersion));
            if (maximumSupportedVersion < minimumSupportedVersion) throw new ArgumentOutOfRangeException(nameof(maximumSupportedVersion));

            _minimumSupportedVersion = minimumSupportedVersion;
            _maximumSupportedVersion = maximumSupportedVersion;
        }

        public bool HasAppliedSnapshot => _hasAppliedSnapshot;

        public ulong CurrentWorldId { get; private set; }

        public long LastAppliedSequence { get; private set; }

        public int LastAppliedFrame { get; private set; }

        public uint LastAppliedStateHash { get; private set; }

        public int LastBaselineFrame { get; private set; }

        public uint LastBaselineHash { get; private set; }

        public bool NeedsFullBaselineRecovery { get; private set; }

        public SnapshotStreamRecoveryReason LastRecoveryReason { get; private set; }

        public int LastIgnoredFrame { get; private set; } = -1;

        public int LastRecoveryFrame { get; private set; } = -1;

        public uint LastRecoveryStateHash { get; private set; }

        public SnapshotStreamValidationResult Validate(in SnapshotStreamEnvelope envelope)
        {
            return Validate(in envelope, int.MaxValue);
        }

        /// <summary>
        /// 校验快照，并可拒绝大于发布方预期序列增量的 delta 序列缺口。
        /// 完整基线始终允许进入，以便数据流从缺口或恢复状态重新建立基线。
        /// </summary>
        public SnapshotStreamValidationResult Validate(
            in SnapshotStreamEnvelope envelope,
            int maximumDeltaSequenceAdvance)
        {
            if (maximumDeltaSequenceAdvance <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumDeltaSequenceAdvance));

            if (envelope.SchemaVersion < _minimumSupportedVersion || envelope.SchemaVersion > _maximumSupportedVersion)
            {
                MarkRejected(SnapshotStreamRecoveryReason.UnsupportedVersion, in envelope, needsFullBaseline: false);
                return CreateResult(
                    SnapshotStreamValidationStatus.UnsupportedVersion,
                    SnapshotStreamRecoveryReason.UnsupportedVersion,
                    in envelope,
                    gapCount: 0,
                    worldChanged: false);
            }

            var worldChanged = _hasAppliedSnapshot && envelope.WorldId != CurrentWorldId;
            if (worldChanged && !envelope.IsFullBaseline)
            {
                MarkRejected(SnapshotStreamRecoveryReason.WorldChanged, in envelope, needsFullBaseline: true);
                return CreateResult(
                    SnapshotStreamValidationStatus.WorldChanged,
                    SnapshotStreamRecoveryReason.WorldChanged,
                    in envelope,
                    gapCount: 0,
                    worldChanged: true);
            }

            if (!worldChanged && _hasAppliedSnapshot)
            {
                if (envelope.Sequence == LastAppliedSequence)
                {
                    MarkIgnored(in envelope);
                    return CreateResult(
                        SnapshotStreamValidationStatus.IgnoredDuplicate,
                        SnapshotStreamRecoveryReason.None,
                        in envelope,
                        gapCount: 0,
                        worldChanged: false);
                }

                if (envelope.Sequence < LastAppliedSequence || envelope.Frame <= LastAppliedFrame)
                {
                    MarkIgnored(in envelope);
                    return CreateResult(
                        SnapshotStreamValidationStatus.IgnoredStale,
                        SnapshotStreamRecoveryReason.None,
                        in envelope,
                        gapCount: 0,
                        worldChanged: false);
                }
            }

            if (envelope.IsFullBaseline)
            {
                return CreateResult(
                    SnapshotStreamValidationStatus.AcceptedFullBaseline,
                    SnapshotStreamRecoveryReason.None,
                    in envelope,
                    gapCount: 0,
                    worldChanged: worldChanged);
            }

            if (!_hasAppliedSnapshot || !_hasBaseline)
            {
                MarkRejected(SnapshotStreamRecoveryReason.MissingBaseline, in envelope, needsFullBaseline: true);
                return CreateResult(
                    SnapshotStreamValidationStatus.MissingBaseline,
                    SnapshotStreamRecoveryReason.MissingBaseline,
                    in envelope,
                    gapCount: 0,
                    worldChanged: false);
            }

            if (envelope.BaselineFrame != LastBaselineFrame || envelope.BaselineHash != LastBaselineHash)
            {
                MarkRejected(SnapshotStreamRecoveryReason.BaselineMismatch, in envelope, needsFullBaseline: true);
                return CreateResult(
                    SnapshotStreamValidationStatus.BaselineMismatch,
                    SnapshotStreamRecoveryReason.BaselineMismatch,
                    in envelope,
                    gapCount: 0,
                    worldChanged: false);
            }

            var gapCount = CalculateGapCount(in envelope);
            if (maximumDeltaSequenceAdvance != int.MaxValue &&
                gapCount >= maximumDeltaSequenceAdvance)
            {
                MarkRejected(SnapshotStreamRecoveryReason.SequenceGap, in envelope, needsFullBaseline: true);
                return CreateResult(
                    SnapshotStreamValidationStatus.SequenceGap,
                    SnapshotStreamRecoveryReason.SequenceGap,
                    in envelope,
                    gapCount,
                    worldChanged: false);
            }

            return CreateResult(
                SnapshotStreamValidationStatus.AcceptedDelta,
                SnapshotStreamRecoveryReason.None,
                in envelope,
                gapCount,
                worldChanged: false);
        }

        public void CommitApplied(in SnapshotStreamValidationResult validation)
        {
            if (!validation.Accepted)
            {
                throw new InvalidOperationException("Only an accepted snapshot stream validation result can be committed.");
            }

            var envelope = validation.Envelope;
            CurrentWorldId = envelope.WorldId;
            LastAppliedSequence = envelope.Sequence;
            LastAppliedFrame = envelope.Frame;
            LastAppliedStateHash = envelope.StateHash;
            if (envelope.IsFullBaseline)
            {
                _hasBaseline = true;
                LastBaselineFrame = envelope.BaselineFrame;
                LastBaselineHash = envelope.BaselineHash;
                NeedsFullBaselineRecovery = false;
                LastRecoveryReason = SnapshotStreamRecoveryReason.None;
                LastRecoveryFrame = -1;
                LastRecoveryStateHash = 0u;
            }

            _hasAppliedSnapshot = true;
        }

        public void Reset()
        {
            _hasAppliedSnapshot = false;
            _hasBaseline = false;
            CurrentWorldId = 0ul;
            LastAppliedSequence = 0L;
            LastAppliedFrame = 0;
            LastAppliedStateHash = 0u;
            LastBaselineFrame = 0;
            LastBaselineHash = 0u;
            NeedsFullBaselineRecovery = false;
            LastRecoveryReason = SnapshotStreamRecoveryReason.None;
            LastIgnoredFrame = -1;
            LastRecoveryFrame = -1;
            LastRecoveryStateHash = 0u;
        }

        private void MarkIgnored(in SnapshotStreamEnvelope envelope)
        {
            LastIgnoredFrame = envelope.Frame;
        }

        private void MarkRejected(
            SnapshotStreamRecoveryReason reason,
            in SnapshotStreamEnvelope envelope,
            bool needsFullBaseline)
        {
            LastRecoveryReason = reason;
            LastRecoveryFrame = envelope.Frame;
            LastRecoveryStateHash = envelope.StateHash;
            if (needsFullBaseline)
            {
                NeedsFullBaselineRecovery = true;
            }
        }

        private int CalculateGapCount(in SnapshotStreamEnvelope envelope)
        {
            if (!_hasAppliedSnapshot || envelope.Sequence <= LastAppliedSequence + 1)
            {
                return 0;
            }

            var gap = envelope.Sequence - LastAppliedSequence - 1;
            return gap > int.MaxValue ? int.MaxValue : (int)gap;
        }

        private SnapshotStreamValidationResult CreateResult(
            SnapshotStreamValidationStatus status,
            SnapshotStreamRecoveryReason recoveryReason,
            in SnapshotStreamEnvelope envelope,
            int gapCount,
            bool worldChanged)
        {
            return new SnapshotStreamValidationResult(
                status,
                recoveryReason,
                in envelope,
                CurrentWorldId,
                LastAppliedSequence,
                LastAppliedFrame,
                gapCount,
                worldChanged);
        }
    }
}
