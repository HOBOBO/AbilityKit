using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientFrameSyncControllerTests
{
    [Fact]
    public void ClientFrameSyncControllerContinuesTickingAfterAuthoritySnapshotOverwrite()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "frame-sync-source",
            30,
            1901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 6f, 0f)
            });
        Assert.True(source.StartGame(in start));
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false) });
        Assert.True(source.Tick(1f / 30f));

        var local = new ShooterBattleRuntimePort();
        var localStart = new ShooterStartGamePayload(
            "frame-sync-local",
            30,
            1902,
            new[]
            {
                new ShooterStartPlayer(9, "Other", -10f, 0f)
            });
        Assert.True(local.StartGame(in localStart));

        var packed = source.ExportPackedSnapshot(991ul, isFullSnapshot: true, authorityOverride: true);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 789.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        var gatewayPayload = WireRoomGatewayBinary.Serialize(in wire);
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, gatewayPayload);
        var overwrittenFrame = local.CurrentFrame;
        var overwrittenHash = local.ComputeStateHash();
        var acceptedInputs = controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false));
        var tickResult = controller.Tick(1f / 30f);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, applyResult);
        Assert.Equal(1, acceptedInputs);
        Assert.Equal(1, tickResult.Ticks);
        Assert.Equal(overwrittenFrame + 1, tickResult.Frame);
        Assert.Equal(local.CurrentFrame, presentation.ViewModel.Frame);
        Assert.NotEqual(overwrittenHash, tickResult.StateHash);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1)));
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2)));
    }

    [Fact]
    public void ClientFrameSyncControllerReplaysPendingInputsAfterAuthoritySnapshotOverwrite()
    {
        var start = new ShooterStartGamePayload(
            "pending-replay",
            30,
            2901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });
        var commandFrame0 = new ShooterPlayerCommand(1, 0f, 1f, 1f, 0f, false);
        var commandFrame1 = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false);
        var commandFrame2 = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(2991ul, isFullSnapshot: true, authorityOverride: true);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 890.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        var gatewayPayload = WireRoomGatewayBinary.Serialize(in wire);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var publishedDiagnosticsCount = 0;
        var publishedDiagnostics = ShooterClientReconciliationResult.None;
        presentation.ReconciliationDiagnostics.ReconciliationApplied += result =>
        {
            publishedDiagnosticsCount++;
            publishedDiagnostics = result;
        };
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame0));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame1));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame2));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(3, local.CurrentFrame);
        var predictedHashBeforeCorrection = local.ComputeStateHash();

        var expected = new ShooterBattleRuntimePort();
        Assert.True(expected.ImportPackedSnapshot(in packed));
        Assert.Equal(1, expected.SubmitInput(1, new[] { commandFrame1 }));
        Assert.True(expected.Tick(1f / 30f));
        Assert.Equal(1, expected.SubmitInput(2, new[] { commandFrame2 }));
        Assert.True(expected.Tick(1f / 30f));

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, gatewayPayload);
        var reconciliation = controller.LastReconciliationResult;

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, applyResult);
        Assert.Equal(expected.CurrentFrame, local.CurrentFrame);
        Assert.Equal(expected.ComputeStateHash(), local.ComputeStateHash());
        Assert.Equal(expected.CurrentFrame, presentation.ViewModel.Frame);
        Assert.Equal(2, controller.PendingInputFrameCount);
        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, reconciliation.ApplyResult);
        Assert.Equal(3, reconciliation.PredictedFrameBeforeCorrection);
        Assert.Equal(predictedHashBeforeCorrection, reconciliation.PredictedHashBeforeCorrection);
        Assert.Equal(packed.Frame, reconciliation.AuthoritativeFrame);
        Assert.Equal(packed.StateHash, reconciliation.AuthoritativeStateHash);
        Assert.Equal(packed.StateHash, reconciliation.ImportedStateHash);
        Assert.True(reconciliation.AuthoritativeHashMatched);
        Assert.Equal(2, reconciliation.ReplayTicks);
        Assert.Equal(expected.CurrentFrame, reconciliation.FinalFrame);
        Assert.Equal(expected.ComputeStateHash(), reconciliation.FinalStateHash);
        Assert.Equal(3, reconciliation.PendingInputFramesBeforeCorrection);
        Assert.Equal(2, reconciliation.PendingInputFramesAfterTrim);
        Assert.Equal(2, reconciliation.PendingInputFramesAfterReplay);
        Assert.Collection(
            controller.LastReconciliationHealthEvents,
            rollback =>
            {
                Assert.Equal(AbilityKit.Network.Runtime.Sync.SyncHealthEventKind.RollbackStarted, rollback.Kind);
                Assert.Equal(reconciliation.AuthoritativeFrame, rollback.Frame);
                Assert.Equal(reconciliation.ReplayTicks, rollback.Value);
            },
            replay =>
            {
                Assert.Equal(AbilityKit.Network.Runtime.Sync.SyncHealthEventKind.ReplayCompleted, replay.Kind);
                Assert.Equal(reconciliation.FinalFrame, replay.Frame);
                Assert.Equal(reconciliation.ReplayTicks, replay.Value);
            });
        Assert.Equal(1, publishedDiagnosticsCount);
        Assert.Equal(reconciliation.ApplyResult, publishedDiagnostics.ApplyResult);
        Assert.Equal(reconciliation.PredictedFrameBeforeCorrection, publishedDiagnostics.PredictedFrameBeforeCorrection);
        Assert.Equal(reconciliation.AuthoritativeFrame, publishedDiagnostics.AuthoritativeFrame);
        Assert.Equal(reconciliation.FinalFrame, publishedDiagnostics.FinalFrame);
        Assert.Equal(reconciliation.FinalStateHash, publishedDiagnostics.FinalStateHash);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1)));
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2)));
    }

    [Fact]
    public void ClientFrameSyncControllerReconcilesAfterDroppedAuthorityFrames()
    {
        var start = new ShooterStartGamePayload(
            "dropped-authority-frames",
            30,
            3901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });
        var commandFrame0 = new ShooterPlayerCommand(1, 0f, 1f, 1f, 0f, false);
        var commandFrame1 = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false);
        var commandFrame2 = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(3991ul, isFullSnapshot: true, authorityOverride: true);
        var gatewayPayload = CreatePackedPushPayload(in packed, timestamp: 990.5, serverTicks: 990500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame0));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame1));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(1, controller.SubmitLocalInput(commandFrame2));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(3, local.CurrentFrame);

        var expected = new ShooterBattleRuntimePort();
        Assert.True(expected.ImportPackedSnapshot(in packed));
        Assert.Equal(1, expected.SubmitInput(2, new[] { commandFrame2 }));
        Assert.True(expected.Tick(1f / 30f));

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, gatewayPayload);
        var reconciliation = controller.LastReconciliationResult;

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, applyResult);
        Assert.Equal(expected.CurrentFrame, local.CurrentFrame);
        Assert.Equal(expected.ComputeStateHash(), local.ComputeStateHash());
        Assert.Equal(1, controller.PendingInputFrameCount);
        Assert.Equal(3, reconciliation.PredictedFrameBeforeCorrection);
        Assert.Equal(packed.Frame, reconciliation.AuthoritativeFrame);
        Assert.Equal(1, reconciliation.ReplayTicks);
        Assert.Equal(expected.CurrentFrame, reconciliation.FinalFrame);
    }

    [Fact]
    public void ClientFrameSyncControllerImportsFullAuthorityAfterSustainedPrediction()
    {
        var start = new ShooterStartGamePayload(
            "full-authority-after-prediction",
            30,
            3901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f)
            });
        var authority = new ShooterBattleRuntimePort();
        var local = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(local.StartGame(in start));
        var controller = new ShooterClientFrameSyncController(local, new ShooterPresentationFacade(), tickRate: 30);

        for (var frame = 0; frame < 300; frame++)
        {
            var fire = frame == 75 || frame == 81 || frame == 194 || frame == 217 || frame == 223 || frame == 260 || frame == 272;
            var localCommand = new ShooterPlayerCommand(1, frame < 145 ? 1f : 0f, 0f, 1f, 0f, fire);
            Assert.Equal(2, authority.SubmitInput(frame, new[]
            {
                localCommand,
                new ShooterPlayerCommand(2, frame < 145 ? -1f : 0f, 0f, -1f, 0f, fire)
            }));
            Assert.True(authority.Tick(1f / 30f));
            Assert.Equal(1, controller.SubmitLocalInput(localCommand));
            Assert.Equal(1, controller.Tick(1f / 30f).Ticks);

            if (frame % 6 != 5)
            {
                continue;
            }

            var snapshot = authority.ExportPackedSnapshot(3901ul, isFullSnapshot: true, authorityOverride: true);
            var payload = CreatePackedPushPayload(in snapshot, frame, frame);
            Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload));
            var evidence = controller.LastImportedSnapshotEvidence;
            Assert.True(
                evidence.AuthoritativeStateHash == evidence.ImportedStateHash,
                $"Full authority import mismatch at frame {evidence.Frame}; expected=0x{evidence.AuthoritativeStateHash:X8}, actual=0x{evidence.ImportedStateHash:X8}.");
        }
    }

    [Fact]
    public void ClientFrameSyncControllerImportsServerEnemyWavesDuringSustainedPrediction()
    {
        var start = new ShooterStartGamePayload(
            "server-enemy-waves-after-prediction",
            30,
            3902,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        var defaultFlow = ShooterSveltoGameplayBattleFlowConfig.Default;
        var longRunningFlow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: 18000,
            victoryTargetDefeats: int.MaxValue,
            defaultFlow.MaxActiveEnemies,
            defaultFlow.Waves,
            defaultFlow.EnemyLoadoutId,
            defaultFlow.EnemyAttackIntervalFrames,
            defaultFlow.EnemyAttackDamage,
            defaultFlow.EnemyProjectileSpeedScale,
            defaultFlow.EnemyProjectilesPerShot,
            defaultFlow.EnemySpreadDegrees);
        var authority = new ShooterBattleRuntimePort(
            ShooterEntityLimitOptions.Default,
            new ShooterEnemyWaveOptions(true, longRunningFlow));
        var local = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(local.StartGame(in start));
        Assert.True(authority.TryGetPlayer(1, out var authorityPlayerOne));
        Assert.True(authority.TryGetPlayer(2, out var authorityPlayerTwo));
        authorityPlayerOne.Hp = 100000;
        authorityPlayerTwo.Hp = 100000;
        authority.SetPlayer(in authorityPlayerOne);
        authority.SetPlayer(in authorityPlayerTwo);
        var controller = new ShooterClientFrameSyncController(local, new ShooterPresentationFacade(), tickRate: 30);

        for (var frame = 0; frame < 300; frame++)
        {
            var fire = frame is 74 or 80 or 193 or 216 or 222 or 259 or 271;
            var localCommand = new ShooterPlayerCommand(1, frame < 144 ? 1f : 0f, 0f, 1f, 0f, fire);
            Assert.Equal(2, authority.SubmitInput(frame, new[]
            {
                localCommand,
                new ShooterPlayerCommand(2, frame < 144 ? -1f : 0f, 0f, -1f, 0f, fire)
            }));
            Assert.True(authority.Tick(1f / 30f));
            Assert.Equal(1, controller.SubmitLocalInput(localCommand));
            Assert.Equal(1, controller.Tick(1f / 30f).Ticks);

            var snapshot = authority.ExportPackedSnapshot(3902ul, isFullSnapshot: true, authorityOverride: true);
            var payload = CreatePackedPushPayload(in snapshot, frame, frame);
            Assert.Equal(
                ShooterSnapshotApplyResult.AppliedPackedSnapshot,
                controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload));
            var evidence = controller.LastImportedSnapshotEvidence;
            Assert.True(
                evidence.AuthoritativeStateHash == evidence.ImportedStateHash,
                $"Server enemy-wave full import mismatch at frame {evidence.Frame}; " +
                $"expected=0x{evidence.AuthoritativeStateHash:X8}, actual=0x{evidence.ImportedStateHash:X8}.");
        }
    }

    [Fact]
    public void ClientFrameSyncControllerRestoresPredictedSnapshotFromRollbackBuffer()
    {
        var start = new ShooterStartGamePayload(
            "local-rollback-buffer",
            30,
            5901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        Assert.Equal(1, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        var frameOne = local.CurrentFrame;
        var frameOneHash = local.ComputeStateHash();

        Assert.Equal(1, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 0f, 1f, 0f, 1f, true)));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(frameOne + 1, local.CurrentFrame);
        Assert.NotEqual(frameOneHash, local.ComputeStateHash());

        Assert.True(controller.TryRestorePredictedSnapshot(frameOne));
        Assert.Equal(frameOne, local.CurrentFrame);
        Assert.Equal(frameOneHash, local.ComputeStateHash());
        Assert.Equal(frameOne, presentation.ViewModel.Frame);
    }

    [Fact]
    public void ClientFrameSyncControllerTicksAndReplaysWithoutPredictionBuffers()
    {
        var start = new ShooterStartGamePayload(
            "no-prediction-buffers",
            30,
            5911,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });
        var replayedCommand = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(5991ul, isFullSnapshot: true, authorityOverride: true);
        var payload = CreatePackedPushPayload(in packed, timestamp: 591.5, serverTicks: 591500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var controller = new ShooterClientFrameSyncController(
            local,
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            rollbackWorldId: 0ul,
            ShooterClientPredictionBufferOptions.Disabled);
        Assert.Equal(1, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 0f, 1f, 0f, 1f, false)));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(1, controller.SubmitLocalInput(replayedCommand));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);

        var expected = new ShooterBattleRuntimePort();
        Assert.True(expected.ImportPackedSnapshot(in packed));
        Assert.Equal(1, expected.SubmitInput(packed.Frame, new[] { replayedCommand }));
        Assert.True(expected.Tick(1f / 30f));

        Assert.Equal(
            ShooterSnapshotApplyResult.AppliedPackedSnapshot,
            controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload));
        Assert.Equal(expected.CurrentFrame, local.CurrentFrame);
        Assert.Equal(expected.ComputeStateHash(), local.ComputeStateHash());
        Assert.False(controller.HasFrameworkInputHistory);
        Assert.False(controller.HasRollbackSnapshotHistory);
        Assert.False(controller.HasStateHashHistory);
        Assert.False(controller.TryRestorePredictedSnapshot(packed.Frame));
    }

    [Fact]
    public void ClientFrameSyncControllerCanAssembleRollbackSnapshotsOnly()
    {
        var start = new ShooterStartGamePayload(
            "rollback-only",
            30,
            5921,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });
        var options = new ShooterClientPredictionBufferOptions(
            ShooterClientPredictionBufferFeatures.RollbackSnapshots,
            inputHistoryCapacity: 0,
            rollbackSnapshotCapacity: 8,
            stateHashHistoryCapacity: 0);
        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var controller = new ShooterClientFrameSyncController(
            local,
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            rollbackWorldId: 0ul,
            options);

        Assert.Equal(1, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        var capturedFrame = local.CurrentFrame;
        var capturedHash = local.ComputeStateHash();
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);

        Assert.False(controller.HasFrameworkInputHistory);
        Assert.True(controller.HasRollbackSnapshotHistory);
        Assert.False(controller.HasStateHashHistory);
        Assert.True(controller.TryRestorePredictedSnapshot(capturedFrame));
        Assert.Equal(capturedHash, local.ComputeStateHash());
    }

    [Fact]
    public void ClientFrameSyncControllerIgnoresLateStaleAuthoritySnapshot()
    {
        var start = new ShooterStartGamePayload(
            "late-stale-authority",
            30,
            4901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var oldPacked = authority.ExportPackedSnapshot(4991ul, isFullSnapshot: true, authorityOverride: true);
        Assert.True(authority.Tick(1f / 30f));
        var newPacked = authority.ExportPackedSnapshot(4991ul, isFullSnapshot: true, authorityOverride: true);
        var oldPayload = CreatePackedPushPayload(in oldPacked, timestamp: 1000.5, serverTicks: 1000500L);
        var newPayload = CreatePackedPushPayload(in newPacked, timestamp: 1001.5, serverTicks: 1001500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        var newResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, newPayload);
        var frameAfterNewSnapshot = local.CurrentFrame;
        var hashAfterNewSnapshot = local.ComputeStateHash();
        Assert.Equal(1, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)));
        Assert.Equal(1, controller.Tick(1f / 30f).Ticks);
        var predictedFrameBeforeLatePacket = local.CurrentFrame;
        var predictedHashBeforeLatePacket = local.ComputeStateHash();

        var staleResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, oldPayload);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, newResult);
        Assert.Equal(newPacked.Frame, frameAfterNewSnapshot);
        Assert.Equal(newPacked.StateHash, hashAfterNewSnapshot);
        Assert.Equal(ShooterSnapshotApplyResult.IgnoredStaleSnapshot, staleResult);
        Assert.Equal(predictedFrameBeforeLatePacket, local.CurrentFrame);
        Assert.Equal(predictedHashBeforeLatePacket, local.ComputeStateHash());
        Assert.Equal(ShooterClientReconciliationResult.None.ApplyResult, controller.LastReconciliationResult.ApplyResult);
    }

    [Fact]
    public void ClientFrameSyncControllerMarksResyncWhenPackedSnapshotImportFails()
    {
        var start = new ShooterStartGamePayload(
            "import-failed-resync",
            30,
            6901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);
        var invalidPacked = new ShooterPackedSnapshotPayload(
            version: ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 6991ul,
            frame: 1,
            serverTick: 10L,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: Array.Empty<ShooterPackedComponentChunk>());
        var payload = CreatePackedPushPayload(in invalidPacked, timestamp: 1010.5, serverTicks: 1010500L);

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload);

        Assert.Equal(ShooterSnapshotApplyResult.ImportFailed, applyResult);
        Assert.True(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientResyncReason.ImportFailed, controller.LastResyncReason);
        Assert.Equal(ShooterClientRecoveryState.AwaitingFullSnapshot, controller.RecoveryState);
        Assert.Equal(0, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)));
        Assert.Equal(0, controller.Tick(1f / 30f).Ticks);
        Assert.Equal(ShooterClientReconciliationResult.None.ApplyResult, controller.LastReconciliationResult.ApplyResult);
    }

    [Fact]
    public void ClientFrameSyncControllerMarksResyncWhenAuthoritativeHashDoesNotMatchImportedState()
    {
        var start = new ShooterStartGamePayload(
            "hash-mismatch-resync",
            30,
            7901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(7991ul, isFullSnapshot: true, authorityOverride: true);
        var corruptedHashPacked = new ShooterPackedSnapshotPayload(
            packed.Version,
            packed.WorldId,
            packed.Frame,
            packed.ServerTick,
            packed.SnapshotFlags,
            packed.StateHash + 1u,
            packed.EntityCount,
            packed.ExtensionPayload,
            packed.ComponentChunks);
        var payload = CreatePackedPushPayload(in corruptedHashPacked, timestamp: 1020.5, serverTicks: 1020500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload);
        var reconciliation = controller.LastReconciliationResult;

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, applyResult);
        Assert.False(reconciliation.AuthoritativeHashMatched);
        Assert.True(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientRecoveryState.AwaitingFullSnapshot, controller.RecoveryState);
        Assert.Equal(ShooterClientResyncReason.AuthoritativeHashMismatch, controller.LastResyncReason);
        Assert.Equal(0, controller.SubmitLocalInput(new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)));
    }

    [Fact]
    public void ClientFrameSyncControllerClearsResyncAfterMatchedAuthoritySnapshot()
    {
        var start = new ShooterStartGamePayload(
            "clear-resync",
            30,
            8901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(8991ul, isFullSnapshot: true, authorityOverride: true);
        var corruptedHashPacked = new ShooterPackedSnapshotPayload(
            packed.Version,
            packed.WorldId,
            packed.Frame,
            packed.ServerTick,
            packed.SnapshotFlags,
            packed.StateHash + 1u,
            packed.EntityCount,
            packed.ExtensionPayload,
            packed.ComponentChunks);
        var mismatchPayload = CreatePackedPushPayload(in corruptedHashPacked, timestamp: 1030.5, serverTicks: 1030500L);
        Assert.True(authority.Tick(1f / 30f));
        var matchedPacked = authority.ExportPackedSnapshot(8991ul, isFullSnapshot: true, authorityOverride: true);
        var matchedPayload = CreatePackedPushPayload(in matchedPacked, timestamp: 1031.5, serverTicks: 1031500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, mismatchPayload));
        Assert.True(controller.NeedsFullSnapshotResync);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, matchedPayload));

        Assert.False(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientResyncReason.None, controller.LastResyncReason);
        Assert.Equal(ShooterClientRecoveryState.Recovered, controller.RecoveryState);
        Assert.True(controller.LastReconciliationResult.AuthoritativeHashMatched);
    }

    [Fact]
    public void ClientFrameSyncControllerUsesCatchUpStateForSmallFrameGap()
    {
        var start = new ShooterStartGamePayload(
            "small-catch-up",
            30,
            9901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var policy = new ShooterClientDriftRecoveryPolicy(
            smallCatchUpThreshold: 4,
            replayThreshold: 120,
            maxCatchUpTicksPerUpdate: 2,
            snapshotTimeoutTicks: 0L);
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30, decoder: null, rollbackWorldId: 0ul, rollbackBufferFrames: 240, policy);

        Assert.True(controller.TryEnterCatchUp(3));
        Assert.Equal(ShooterClientRecoveryState.CatchUp, controller.RecoveryState);

        var firstTick = controller.Tick(1f / 30f);
        Assert.Equal(2, firstTick.Ticks);
        Assert.Equal(2, firstTick.Frame);
        Assert.Equal(ShooterClientRecoveryState.CatchUp, controller.RecoveryState);

        var secondTick = controller.Tick(1f / 30f);
        Assert.Equal(1, secondTick.Ticks);
        Assert.Equal(3, secondTick.Frame);
        Assert.Equal(ShooterClientRecoveryState.Normal, controller.RecoveryState);
    }

    [Fact]
    public void ClientFrameSyncControllerRequestsFullSnapshotWhenCatchUpGapIsTooLarge()
    {
        var start = new ShooterStartGamePayload(
            "large-catch-up",
            30,
            9902,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var policy = new ShooterClientDriftRecoveryPolicy(
            smallCatchUpThreshold: 2,
            replayThreshold: 120,
            maxCatchUpTicksPerUpdate: 2,
            snapshotTimeoutTicks: 0L);
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30, decoder: null, rollbackWorldId: 0ul, rollbackBufferFrames: 240, policy);

        Assert.False(controller.TryEnterCatchUp(5));
        Assert.True(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientRecoveryState.AwaitingFullSnapshot, controller.RecoveryState);
        Assert.Equal(ShooterClientResyncReason.FrameTooFarBehind, controller.LastResyncReason);
    }

    private static ArraySegment<byte> CreatePackedPushPayload(in ShooterPackedSnapshotPayload packed, double timestamp, long serverTicks)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = timestamp,
            ServerTicks = serverTicks,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        return WireRoomGatewayBinary.Serialize(in wire);
    }
}
