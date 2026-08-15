using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterPureStateSnapshotSyncControllerTests
{
    [Fact]
    public void ConstructorNegotiatesPureStatePlaybackProfileAndSchemaVersion()
    {
        var controller = new ShooterPureStateSnapshotSyncController(new ShooterPresentationFacade());

        Assert.True(controller.Negotiation.HasValue);
        Assert.True(controller.Negotiation.Value.IsCompatible);
        Assert.Equal(
            ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
            controller.Negotiation.Value.MinimumSchemaVersion);
        Assert.Equal(
            ShooterPureStateSyncCodec.CurrentVersion,
            controller.Negotiation.Value.MaximumSchemaVersion);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerAppliesGatewayFullBaselineToViewModel()
    {
        var source = CreateStartedSourceRuntime();
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));
        var pureState = source.ExportPureStateSnapshot(777ul, isFullBaseline: true);
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);
        var gatewayPayload = CreateGatewayPayload(in pureState, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true);

        var result = controller.TryApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, gatewayPayload);

        Assert.Equal(ShooterPureStateSnapshotApplyResult.AppliedFullBaseline, result);
        Assert.Equal(pureState.Frame, controller.LastAppliedFrame);
        Assert.Equal(pureState.StateHash, controller.LastAppliedStateHash);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, controller.LastAppliedSnapshotKind);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.AppliedFullBaseline, controller.LastDiagnostics.LastApplyResult);
        Assert.Equal(pureState.Frame, controller.LastDiagnostics.SourceFrame);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, controller.LastDiagnostics.SourceSnapshotKind);
        Assert.Equal(pureState.Entities.Length, controller.LastDiagnostics.SourceEntityCount);
        Assert.Equal(pureState.VisibilityHints.Length, controller.LastDiagnostics.SourceVisibilityHintCount);
        Assert.Equal(pureState.BaselineFrame, controller.LastDiagnostics.SourceBaselineFrame);
        Assert.Equal(pureState.BaselineHash, controller.LastDiagnostics.SourceBaselineHash);
        Assert.Equal(pureState.StateHash, controller.LastDiagnostics.SourceStateHash);
        Assert.Equal(pureState.ServerTick, controller.LastDiagnostics.SourceServerTick);
        Assert.Equal(pureState.Frame, controller.LastDiagnostics.AppliedFrame);
        Assert.Equal(pureState.StateHash, controller.LastDiagnostics.AppliedStateHash);
        Assert.False(controller.LastDiagnostics.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.None, controller.LastDiagnostics.LastResyncReason);
        Assert.True(controller.LastDiagnostics.HasSourceSnapshot);
        Assert.True(controller.LastDiagnostics.AppliedPresentation);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.SnapshotReceived && e.Frame == pureState.Frame);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.FullSnapshotApplied && e.Frame == pureState.Frame);
        Assert.Equal(pureState.Frame, presentation.ViewModel.Frame);
        Assert.Equal(ShooterViewSnapshotKind.Full, presentation.ViewModel.Current.SnapshotKind);
        Assert.Equal(ShooterViewBatchSource.AuthoritativeCorrection, presentation.ViewModel.Current.Source);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Kind == ShooterViewEntityKind.Player && change.EntityId == 1);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Kind == ShooterViewEntityKind.Player && change.EntityId == 2);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Kind == ShooterViewEntityKind.Bullet);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerAppliesGatewayDeltaAndIgnoresStaleFrames()
    {
        var source = CreateStartedSourceRuntime();
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));
        var baseline = source.ExportPureStateSnapshot(777ul, isFullBaseline: true);
        source.SubmitInput(source.CurrentFrame, new[] { new ShooterPlayerCommand(2, -1f, 0f, -1f, 0f, false) });
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(777ul, isFullBaseline: false, baselineFrame: baseline.Frame, baselineHash: baseline.StateHash);
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);

        var baselineApplied = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.SnapshotPushed,
            CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));
        var applied = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));
        var stale = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.AppliedFullBaseline, baselineApplied);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.AppliedDelta, applied);
        Assert.False(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot, stale);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot, controller.LastDiagnostics.LastApplyResult);
        Assert.Equal(baseline.Frame, controller.LastDiagnostics.SourceFrame);
        Assert.Equal(delta.Frame, controller.LastDiagnostics.AppliedFrame);
        Assert.Equal(delta.StateHash, controller.LastDiagnostics.AppliedStateHash);
        Assert.Equal(baseline.Frame, controller.LastDiagnostics.LastIgnoredFrame);
        Assert.False(controller.LastDiagnostics.AppliedPresentation);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.SnapshotStale && e.Frame == baseline.Frame);
        Assert.Equal(baseline.Frame, controller.LastIgnoredFrame);
        Assert.Equal(delta.Frame, presentation.ViewModel.Frame);
        Assert.Equal(ShooterViewSnapshotKind.Delta, presentation.ViewModel.Current.SnapshotKind);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerRequestsFullBaselineWhenDeltaArrivesWithoutBaseline()
    {
        var source = CreateStartedSourceRuntime();
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(778ul, isFullBaseline: false, baselineFrame: 99, baselineHash: 123u);
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);

        var result = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, result);
        Assert.True(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.MissingBaseline, controller.LastResyncReason);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, controller.LastDiagnostics.LastApplyResult);
        Assert.Equal(delta.Frame, controller.LastDiagnostics.SourceFrame);
        Assert.Equal(ShooterPureStateSnapshotKinds.Delta, controller.LastDiagnostics.SourceSnapshotKind);
        Assert.Equal(delta.BaselineFrame, controller.LastDiagnostics.SourceBaselineFrame);
        Assert.Equal(delta.BaselineHash, controller.LastDiagnostics.SourceBaselineHash);
        Assert.Equal(0, controller.LastDiagnostics.AppliedFrame);
        Assert.True(controller.LastDiagnostics.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.MissingBaseline, controller.LastDiagnostics.LastResyncReason);
        Assert.Equal(delta.Frame, controller.LastDiagnostics.LastResyncFrame);
        Assert.Equal(delta.StateHash, controller.LastDiagnostics.LastResyncStateHash);
        Assert.False(controller.LastDiagnostics.AppliedPresentation);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.FullSnapshotRequested && e.Frame == delta.Frame);
        Assert.Equal(0, presentation.ViewModel.Frame);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerRequestsFullBaselineWhenDeltaBaselineMismatches()
    {
        var source = CreateStartedSourceRuntime();
        Assert.True(source.Tick(1f / 30f));
        var baseline = source.ExportPureStateSnapshot(779ul, isFullBaseline: true);
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(779ul, isFullBaseline: false, baselineFrame: baseline.Frame + 10, baselineHash: baseline.StateHash + 1u);
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);

        Assert.Equal(
            ShooterPureStateSnapshotApplyResult.AppliedFullBaseline,
            controller.TryApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true)));
        var result = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, result);
        Assert.True(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.BaselineMismatch, controller.LastResyncReason);
        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, controller.LastDiagnostics.LastApplyResult);
        Assert.Equal(delta.Frame, controller.LastDiagnostics.SourceFrame);
        Assert.Equal(baseline.Frame, controller.LastDiagnostics.AppliedFrame);
        Assert.Equal(baseline.StateHash, controller.LastDiagnostics.AppliedStateHash);
        Assert.Equal(ShooterPureStateResyncReason.BaselineMismatch, controller.LastDiagnostics.LastResyncReason);
        Assert.Equal(delta.Frame, controller.LastDiagnostics.LastResyncFrame);
        Assert.Equal(delta.StateHash, controller.LastDiagnostics.LastResyncStateHash);
        Assert.True(controller.LastDiagnostics.NeedsFullBaselineResync);
        Assert.False(controller.LastDiagnostics.AppliedPresentation);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.FullSnapshotRequested && e.Frame == delta.Frame);
        Assert.Equal(baseline.Frame, presentation.ViewModel.Frame);

        var staleResult = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.SnapshotPushed,
            CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot, staleResult);
        Assert.Equal(baseline.Frame, controller.LastIgnoredFrame);
        Assert.Equal(ShooterPureStateResyncReason.BaselineMismatch, controller.LastResyncReason);
        Assert.Equal(delta.Frame, controller.LastResyncFrame);
        Assert.Equal(delta.StateHash, controller.LastResyncStateHash);
        Assert.True(controller.NeedsFullBaselineResync);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerRejectsMissingPublishedDeltaAndRequestsRecovery()
    {
        var source = CreateStartedSourceRuntime();
        Assert.True(source.Tick(1f / 30f));
        var baseline = source.ExportPureStateSnapshot(782ul, isFullBaseline: true);
        var delta = source.ExportPureStateSnapshot(
            782ul,
            isFullBaseline: false,
            settings: new ShooterPureStateSyncSettings(100, 50, 60, 2, 10, 3),
            baselineFrame: baseline.Frame,
            baselineHash: baseline.StateHash);
        delta.Frame = baseline.Frame + 4;
        delta.ServerTick = delta.Frame;

        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);
        Assert.Equal(
            ShooterPureStateSnapshotApplyResult.AppliedFullBaseline,
            controller.TryApplyGatewayPush(
                RoomGatewayOpCodes.SnapshotPushed,
                CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true)));

        var result = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, result);
        Assert.True(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.SequenceGap, controller.LastResyncReason);
        Assert.Equal(baseline.Frame, controller.LastAppliedFrame);
        Assert.Equal(baseline.Frame, presentation.ViewModel.Frame);
        Assert.Contains(controller.LastHealthEvents, e =>
            e.Kind == SyncHealthEventKind.SnapshotGap &&
            e.Severity == SyncHealthSeverity.Error &&
            e.Frame == delta.Frame &&
            e.Value == 3);
        Assert.Contains(controller.LastHealthEvents, e =>
            e.Kind == SyncHealthEventKind.FullSnapshotRequested && e.Frame == delta.Frame);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerRequiresFullBaselineWhenWorldChanges()
    {
        var source = CreateStartedSourceRuntime();
        Assert.True(source.Tick(1f / 30f));
        var baseline = source.ExportPureStateSnapshot(779ul, isFullBaseline: true);
        Assert.True(source.Tick(1f / 30f));
        var foreignDelta = source.ExportPureStateSnapshot(780ul, isFullBaseline: false, baselineFrame: baseline.Frame, baselineHash: baseline.StateHash);
        var foreignBaseline = foreignDelta;
        foreignBaseline.SnapshotKind = ShooterPureStateSnapshotKinds.FullBaseline;
        foreignBaseline.BaselineFrame = foreignBaseline.Frame;
        foreignBaseline.BaselineHash = foreignBaseline.StateHash;
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);

        Assert.Equal(
            ShooterPureStateSnapshotApplyResult.AppliedFullBaseline,
            controller.TryApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, CreateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true)));
        var rejected = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreateGatewayPayload(in foreignDelta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync, rejected);
        Assert.True(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.WorldChanged, controller.LastResyncReason);
        Assert.Equal(foreignDelta.Frame, controller.LastResyncFrame);

        var recovered = controller.TryApplyGatewayPush(
            RoomGatewayOpCodes.SnapshotPushed,
            CreateGatewayPayload(in foreignBaseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));

        Assert.Equal(ShooterPureStateSnapshotApplyResult.AppliedFullBaseline, recovered);
        Assert.False(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.None, controller.LastResyncReason);
        Assert.Equal(-1, controller.LastResyncFrame);
        Assert.Equal(foreignBaseline.Frame, presentation.ViewModel.Frame);
    }

    [Fact]
    public void PureStateSnapshotSyncControllerRejectsUnsupportedVersionWithoutApplyingPresentation()
    {
        var source = CreateStartedSourceRuntime();
        Assert.True(source.Tick(1f / 30f));
        var unsupported = source.ExportPureStateSnapshot(781ul, isFullBaseline: true);
        unsupported.Version = ShooterPureStateSyncCodec.CurrentVersion + 1;
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterPureStateSnapshotSyncController(presentation);
        var payload = CreateGatewayPayload(in unsupported, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true);

        var result = controller.TryApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload);

        Assert.Equal(ShooterPureStateSnapshotApplyResult.UnsupportedVersion, result);
        Assert.False(controller.NeedsFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.UnsupportedVersion, controller.LastResyncReason);
        Assert.Equal(unsupported.Frame, controller.LastResyncFrame);
        Assert.Equal(unsupported.StateHash, controller.LastResyncStateHash);
        Assert.Equal(0, controller.LastAppliedFrame);
        Assert.Equal(0, presentation.ViewModel.Frame);
        Assert.False(controller.LastDiagnostics.AppliedPresentation);
        Assert.Contains(controller.LastHealthEvents, e => e.Kind == SyncHealthEventKind.SnapshotDropped && e.Frame == unsupported.Frame);

        var facade = new ShooterPresentationFacade();
        Assert.False(facade.TryApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload));
        Assert.Equal(0, facade.LastPureStateAppliedFrame);
        Assert.False(facade.NeedsPureStateFullBaselineResync);
        Assert.Equal(ShooterPureStateResyncReason.UnsupportedVersion, facade.LastPureStateResyncReason);
    }

    private static ShooterBattleRuntimePort CreateStartedSourceRuntime()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "pure-state-source",
            30,
            901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            });
        Assert.True(source.StartGame(in start));
        return source;
    }

    private static ArraySegment<byte> CreateGatewayPayload(in ShooterPureStateSnapshotPayload pureState, int payloadOpCode, bool isFullSnapshot)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = pureState.WorldId,
            Frame = pureState.Frame,
            Timestamp = 456.5,
            IsFullSnapshot = isFullSnapshot,
            Actors = null,
            PayloadOpCode = payloadOpCode,
            Payload = ShooterPureStateSyncCodec.Serialize(in pureState),
            ServerTicks = pureState.ServerTick
        };
        return WireRoomGatewayBinary.Serialize(in wire);
    }
}
