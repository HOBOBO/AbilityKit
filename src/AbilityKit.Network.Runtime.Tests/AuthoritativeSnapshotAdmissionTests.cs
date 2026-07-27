using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class AuthoritativeSnapshotAdmissionTests
{
    [Fact]
    public void DeltaBeforeBaseline_IsRejectedAndRequestsFullResync()
    {
        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(42UL);

        var result = admission.Admit(42UL, 10, isFullSnapshot: false);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, result.Status);
        Assert.True(result.ShouldRequestFullResync);
        Assert.False(admission.HasBaseline);
    }

    [Fact]
    public void WrongWorld_IsRejectedWithoutInvalidatingCurrentBaseline()
    {
        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(42UL);
        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted);

        var result = admission.Admit(7UL, 11, isFullSnapshot: false);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.WrongWorld, result.Status);
        Assert.False(result.ShouldRequestFullResync);
        Assert.True(admission.HasBaseline);
        Assert.Equal(10, admission.LastAcceptedFrame);
    }

    [Fact]
    public void UnsupportedSchema_InvalidatesBaseline()
    {
        var admission = new AuthoritativeSnapshotAdmission(minSchemaVersion: 1, maxSchemaVersion: 3);
        admission.Reset(42UL);
        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true, schemaVersion: 2).Accepted);

        var unsupported = admission.Admit(42UL, 11, isFullSnapshot: false, schemaVersion: 4);
        var nextDelta = admission.Admit(42UL, 12, isFullSnapshot: false, schemaVersion: 2);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.UnsupportedSchemaVersion, unsupported.Status);
        Assert.True(unsupported.ShouldRequestFullResync);
        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, nextDelta.Status);
    }

    [Fact]
    public void StaleSnapshot_IsIgnoredWithoutRequestingResync()
    {
        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(42UL);
        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted);

        var result = admission.Admit(42UL, 10, isFullSnapshot: false);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, result.Status);
        Assert.False(result.ShouldRequestFullResync);
        Assert.Equal(10, result.LastAcceptedFrame);
    }

    [Fact]
    public void LargeDeltaGap_InvalidatesBaselineUntilReplacementFullSnapshot()
    {
        var admission = new AuthoritativeSnapshotAdmission(maxDeltaFrameGap: 5);
        admission.Reset(42UL);
        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted);

        var gap = admission.Admit(42UL, 16, isFullSnapshot: false);
        var blockedDelta = admission.Admit(42UL, 17, isFullSnapshot: false);
        var replacement = admission.Admit(42UL, 18, isFullSnapshot: true);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.FrameGapTooLarge, gap.Status);
        Assert.True(gap.ShouldRequestFullResync);
        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, blockedDelta.Status);
        Assert.True(replacement.Accepted);
        Assert.Equal(18, admission.LastAcceptedFrame);
    }

    [Fact]
    public void RequireFullBaseline_BlocksDeltasWithoutDiscardingLastFrameEvidence()
    {
        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(42UL);
        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted);

        admission.RequireFullBaseline();
        var blocked = admission.Admit(42UL, 11, isFullSnapshot: false);

        Assert.Equal(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, blocked.Status);
        Assert.Equal(10, blocked.LastAcceptedFrame);
        Assert.True(admission.Admit(42UL, 12, isFullSnapshot: true).Accepted);
    }
}
