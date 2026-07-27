using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ClientSyncRecoveryCoordinatorTests
{
    private enum RecoveryReason
    {
        None = 0,
        HashMismatch = 1
    }

    [Fact]
    public void EnterCatchUp_RecordsTargetAndProjectsResumingPhase()
    {
        var currentFrame = 10;
        var recovery = new ClientSyncRecoveryCoordinator<RecoveryReason>(
            resumeWindowFrames: 120,
            () => currentFrame,
            RecoveryReason.None);

        recovery.EnterCatchUp(14);

        Assert.Equal(SyncRecoveryState.CatchUp, recovery.State);
        Assert.Equal(14, recovery.CatchUpTargetFrame);
        Assert.Equal(14, recovery.LastRecoveryAuthoritativeFrame);
        Assert.Equal(FastReconnectPhase.Resuming, recovery.FastReconnectPhase);
        Assert.Contains(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => healthEvent.Kind == SyncHealthEventKind.SnapshotGap);
    }

    [Fact]
    public void MarkFullSnapshotResyncNeeded_RecordsEvidenceAndRequestsFullSnapshot()
    {
        var recovery = new ClientSyncRecoveryCoordinator<RecoveryReason>(
            resumeWindowFrames: 8,
            () => 10,
            RecoveryReason.None);

        recovery.MarkFullSnapshotResyncNeeded(
            RecoveryReason.HashMismatch,
            clientFrame: 10,
            authoritativeFrame: 40,
            clientStateHash: 11u,
            authoritativeStateHash: 22u);

        Assert.True(recovery.NeedsFullSnapshotResync);
        Assert.Equal(SyncRecoveryState.AwaitingFullSnapshot, recovery.State);
        Assert.Equal(FastReconnectPhase.AwaitingFullSnapshot, recovery.FastReconnectPhase);
        Assert.Equal(RecoveryReason.HashMismatch, recovery.LastRecoveryReason);
        Assert.Equal(10, recovery.LastRecoveryClientFrame);
        Assert.Equal(40, recovery.LastRecoveryAuthoritativeFrame);
        Assert.Equal(11u, recovery.LastRecoveryClientStateHash);
        Assert.Equal(22u, recovery.LastRecoveryAuthoritativeStateHash);
        Assert.Equal(40, recovery.CatchUpTargetFrame);
        Assert.Contains(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => healthEvent.Kind == SyncHealthEventKind.FullSnapshotRequested);
    }

    [Fact]
    public void Recovered_CompletesFullSnapshotRecoveryAndCapturesClosureEvents()
    {
        var recovery = new ClientSyncRecoveryCoordinator<RecoveryReason>(
            resumeWindowFrames: 8,
            () => 10,
            RecoveryReason.None);
        recovery.MarkFullSnapshotResyncNeeded(
            RecoveryReason.HashMismatch,
            clientFrame: 10,
            authoritativeFrame: 40,
            clientStateHash: 11u,
            authoritativeStateHash: 22u);

        recovery.SetState(SyncRecoveryState.ApplyingFullSnapshot);
        recovery.ClearFullSnapshotResync();
        recovery.SetState(SyncRecoveryState.Recovered);

        Assert.False(recovery.NeedsFullSnapshotResync);
        Assert.Equal(RecoveryReason.None, recovery.LastRecoveryReason);
        Assert.Equal(SyncRecoveryState.Recovered, recovery.State);
        Assert.Equal(FastReconnectPhase.Recovered, recovery.FastReconnectPhase);
        Assert.Contains(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => healthEvent.Kind == SyncHealthEventKind.FullSnapshotApplied);
        Assert.Contains(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => healthEvent.Kind == SyncHealthEventKind.InterpolationRecovered);
    }

    [Fact]
    public void Heartbeat_ReplacesPreviousOperationEventsWithSnapshotReceipt()
    {
        var recovery = new ClientSyncRecoveryCoordinator<RecoveryReason>(
            resumeWindowFrames: 8,
            () => 10,
            RecoveryReason.None);
        recovery.MarkFullSnapshotResyncNeeded(
            RecoveryReason.HashMismatch,
            clientFrame: 10,
            authoritativeFrame: 40,
            clientStateHash: 11u,
            authoritativeStateHash: 22u);
        recovery.SetState(SyncRecoveryState.Recovered);
        recovery.SetState(SyncRecoveryState.Normal);

        recovery.HeartbeatFastReconnect(41);

        Assert.NotEmpty(recovery.LastFastReconnectHealthEvents);
        Assert.All(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => Assert.Equal(SyncHealthEventKind.SnapshotReceived, healthEvent.Kind));
        Assert.DoesNotContain(
            recovery.LastFastReconnectHealthEvents,
            healthEvent => healthEvent.Kind == SyncHealthEventKind.FullSnapshotRequested);
    }
}
