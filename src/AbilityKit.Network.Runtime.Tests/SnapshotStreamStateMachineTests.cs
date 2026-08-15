using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class SnapshotStreamStateMachineTests
{
    [Fact]
    public void FullBaselineThenDeltaAdvancesCommittedStreamState()
    {
        var stream = new SnapshotStreamStateMachine(1);
        var baseline = Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u);
        var baselineValidation = stream.Validate(in baseline);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedFullBaseline, baselineValidation.Status);
        Assert.False(stream.HasAppliedSnapshot);
        stream.CommitApplied(in baselineValidation);

        var delta = Envelope(7, 1, 11, 11, SnapshotStreamSnapshotKind.Delta, 10, 100u, 110u);
        var deltaValidation = stream.Validate(in delta);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedDelta, deltaValidation.Status);
        Assert.Equal(0, deltaValidation.GapCount);
        stream.CommitApplied(in deltaValidation);
        Assert.True(stream.HasAppliedSnapshot);
        Assert.Equal(7ul, stream.CurrentWorldId);
        Assert.Equal(11, stream.LastAppliedSequence);
        Assert.Equal(11, stream.LastAppliedFrame);
        Assert.Equal(110u, stream.LastAppliedStateHash);
        Assert.Equal(10, stream.LastBaselineFrame);
        Assert.Equal(100u, stream.LastBaselineHash);
    }

    [Fact]
    public void FrameZeroBaselineCanBeReferencedByDelta()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 0, 0, SnapshotStreamSnapshotKind.FullBaseline, 0, 100u, 100u));
        var delta = Envelope(7, 1, 1, 1, SnapshotStreamSnapshotKind.Delta, 0, 100u, 110u);

        var validation = stream.Validate(in delta);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedDelta, validation.Status);
        Assert.False(validation.NeedsFullBaseline);
    }

    [Fact]
    public void DuplicateAndStaleSnapshotsAreIgnoredWithoutChangingRecoveryMetadata()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var mismatch = Envelope(7, 1, 12, 12, SnapshotStreamSnapshotKind.Delta, 9, 99u, 120u);

        Assert.Equal(SnapshotStreamValidationStatus.BaselineMismatch, stream.Validate(in mismatch).Status);
        Assert.Equal(12, stream.LastRecoveryFrame);
        Assert.Equal(120u, stream.LastRecoveryStateHash);

        var duplicate = Envelope(7, 1, 10, 11, SnapshotStreamSnapshotKind.Delta, 10, 100u, 111u);
        var stale = Envelope(7, 1, 9, 9, SnapshotStreamSnapshotKind.Delta, 10, 100u, 90u);

        Assert.Equal(SnapshotStreamValidationStatus.IgnoredDuplicate, stream.Validate(in duplicate).Status);
        Assert.Equal(11, stream.LastIgnoredFrame);
        Assert.Equal(SnapshotStreamValidationStatus.IgnoredStale, stream.Validate(in stale).Status);
        Assert.Equal(9, stream.LastIgnoredFrame);
        Assert.Equal(SnapshotStreamRecoveryReason.BaselineMismatch, stream.LastRecoveryReason);
        Assert.Equal(12, stream.LastRecoveryFrame);
        Assert.Equal(120u, stream.LastRecoveryStateHash);
        Assert.True(stream.NeedsFullBaselineRecovery);
    }

    [Fact]
    public void DeltaSequenceGapIsReportedButCanStillBeCommitted()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var delta = Envelope(7, 1, 14, 14, SnapshotStreamSnapshotKind.Delta, 10, 100u, 140u);

        var validation = stream.Validate(in delta);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedDelta, validation.Status);
        Assert.Equal(3, validation.GapCount);
        stream.CommitApplied(in validation);
        Assert.Equal(14, stream.LastAppliedSequence);
    }

    [Fact]
    public void StrictDeltaSequenceGapRequestsBaselineRecovery()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var delta = Envelope(7, 1, 14, 14, SnapshotStreamSnapshotKind.Delta, 10, 100u, 140u);

        var validation = stream.Validate(in delta, maximumDeltaSequenceAdvance: 2);

        Assert.Equal(SnapshotStreamValidationStatus.SequenceGap, validation.Status);
        Assert.Equal(3, validation.GapCount);
        Assert.True(validation.NeedsFullBaseline);
        Assert.True(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.SequenceGap, stream.LastRecoveryReason);
        Assert.Equal(10, stream.LastAppliedSequence);
    }

    [Fact]
    public void StrictDeltaSequencePolicyAcceptsConfiguredBatchAdvance()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var delta = Envelope(7, 1, 12, 12, SnapshotStreamSnapshotKind.Delta, 10, 100u, 120u);

        var validation = stream.Validate(in delta, maximumDeltaSequenceAdvance: 2);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedDelta, validation.Status);
        Assert.Equal(1, validation.GapCount);
        Assert.False(validation.NeedsFullBaseline);
    }

    [Fact]
    public void DeltaWithoutBaselineRequestsRecovery()
    {
        var stream = new SnapshotStreamStateMachine(1);
        var delta = Envelope(7, 1, 4, 4, SnapshotStreamSnapshotKind.Delta, 3, 30u, 40u);

        var validation = stream.Validate(in delta);

        Assert.Equal(SnapshotStreamValidationStatus.MissingBaseline, validation.Status);
        Assert.True(validation.NeedsFullBaseline);
        Assert.True(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.MissingBaseline, stream.LastRecoveryReason);
        Assert.Equal(4, stream.LastRecoveryFrame);
        Assert.Equal(40u, stream.LastRecoveryStateHash);
    }

    [Fact]
    public void WorldChangeRequiresFullBaselineAndFullBaselineRecoversStream()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var foreignDelta = Envelope(8, 1, 11, 11, SnapshotStreamSnapshotKind.Delta, 10, 100u, 110u);

        var rejected = stream.Validate(in foreignDelta);

        Assert.Equal(SnapshotStreamValidationStatus.WorldChanged, rejected.Status);
        Assert.True(rejected.WorldChanged);
        Assert.True(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.WorldChanged, stream.LastRecoveryReason);

        var foreignBaseline = Envelope(8, 1, 1, 1, SnapshotStreamSnapshotKind.FullBaseline, 1, 200u, 200u);
        var accepted = stream.Validate(in foreignBaseline);

        Assert.Equal(SnapshotStreamValidationStatus.AcceptedFullBaseline, accepted.Status);
        Assert.True(accepted.WorldChanged);
        stream.CommitApplied(in accepted);
        Assert.Equal(8ul, stream.CurrentWorldId);
        Assert.False(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.None, stream.LastRecoveryReason);
        Assert.Equal(-1, stream.LastRecoveryFrame);
    }

    [Fact]
    public void UnsupportedVersionIsRejectedWithoutRequestingBaseline()
    {
        var stream = new SnapshotStreamStateMachine(1);
        var baseline = Envelope(7, 2, 1, 1, SnapshotStreamSnapshotKind.FullBaseline, 1, 100u, 100u);

        var validation = stream.Validate(in baseline);

        Assert.Equal(SnapshotStreamValidationStatus.UnsupportedVersion, validation.Status);
        Assert.False(validation.NeedsFullBaseline);
        Assert.False(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.UnsupportedVersion, stream.LastRecoveryReason);
        Assert.Equal(1, stream.LastRecoveryFrame);
    }

    [Fact]
    public void RejectedValidationCannotBeCommitted()
    {
        var stream = new SnapshotStreamStateMachine(1);
        var delta = Envelope(7, 1, 2, 2, SnapshotStreamSnapshotKind.Delta, 1, 10u, 20u);
        var validation = stream.Validate(in delta);

        Assert.Throws<InvalidOperationException>(() => stream.CommitApplied(in validation));
    }

    [Fact]
    public void ResetClearsCommittedIgnoredAndRecoveryState()
    {
        var stream = new SnapshotStreamStateMachine(1);
        Commit(stream, Envelope(7, 1, 10, 10, SnapshotStreamSnapshotKind.FullBaseline, 10, 100u, 100u));
        var mismatch = Envelope(7, 1, 11, 11, SnapshotStreamSnapshotKind.Delta, 9, 99u, 110u);
        stream.Validate(in mismatch);
        var stale = Envelope(7, 1, 9, 9, SnapshotStreamSnapshotKind.Delta, 10, 100u, 90u);
        stream.Validate(in stale);

        stream.Reset();

        Assert.False(stream.HasAppliedSnapshot);
        Assert.Equal(0ul, stream.CurrentWorldId);
        Assert.Equal(0, stream.LastAppliedSequence);
        Assert.Equal(0, stream.LastAppliedFrame);
        Assert.Equal(0u, stream.LastAppliedStateHash);
        Assert.Equal(0, stream.LastBaselineFrame);
        Assert.Equal(0u, stream.LastBaselineHash);
        Assert.False(stream.NeedsFullBaselineRecovery);
        Assert.Equal(SnapshotStreamRecoveryReason.None, stream.LastRecoveryReason);
        Assert.Equal(-1, stream.LastIgnoredFrame);
        Assert.Equal(-1, stream.LastRecoveryFrame);
        Assert.Equal(0u, stream.LastRecoveryStateHash);
    }

    private static void Commit(SnapshotStreamStateMachine stream, SnapshotStreamEnvelope envelope)
    {
        var validation = stream.Validate(in envelope);
        Assert.True(validation.Accepted);
        stream.CommitApplied(in validation);
    }

    private static SnapshotStreamEnvelope Envelope(
        ulong worldId,
        int version,
        long sequence,
        int frame,
        SnapshotStreamSnapshotKind kind,
        int baselineFrame,
        uint baselineHash,
        uint stateHash)
    {
        return new SnapshotStreamEnvelope(
            worldId,
            version,
            sequence,
            frame,
            kind,
            baselineFrame,
            baselineHash,
            stateHash);
    }
}

public sealed class BaselineDeltaSnapshotValidatorTests
{
    [Fact]
    public void FrameZeroBaselineCanBeReferencedByDelta()
    {
        var validator = new BaselineDeltaSnapshotValidator();
        var baseline = new BaselineDeltaSnapshotInfo(0, true, 0, 100u, 100u);
        validator.CommitApplied(in baseline);
        var delta = new BaselineDeltaSnapshotInfo(1, false, 0, 100u, 110u);

        var validation = validator.Validate(in delta);

        Assert.Equal(BaselineDeltaSnapshotValidationStatus.AcceptedDelta, validation.Status);
        Assert.False(validation.NeedsFullBaselineResync);
    }
}
