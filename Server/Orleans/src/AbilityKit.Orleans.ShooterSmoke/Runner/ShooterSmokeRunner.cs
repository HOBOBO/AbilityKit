using System.Net.Sockets;
using AbilityKit.Demo.Shooter;
using AbilityKit.GameFramework.Network;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Orleans;
internal static class ShooterSmokeRunner
{
    public static async Task<ShooterSmokeResult> RunAsync(IClusterClient clusterClient, string host, int port, string inputLogicReplayOutputPath = "")
    {
        if (clusterClient == null) throw new ArgumentNullException(nameof(clusterClient));

        using var channel = new SmokeTcpGameFrameworkNetworkChannel("ShooterSmokeGateway");
        using var connection = GameFrameworkGatewayConnectionFactory.Wrap(channel);
        using var launcher = new ShooterClientNetworkLauncher(connection);

        connection.Open(host, port);
        connection.Tick(0f);

        var primaryAccountId = $"shooter-smoke-account-{Guid.NewGuid():N}";
        var login = await LoginAccountAsync(connection, primaryAccountId, kickExisting: true);
        using var presentationContext = ShooterSmokeScenarioBase.CreatePresentationContext();
        var runtime = presentationContext.Runtime;
        var presentation = presentationContext.Presentation;
        var projectedRecorder = presentationContext.Recorder;
        var presentationSession = presentationContext.Session;
        var start = new ShooterStartGamePayload(
            "shooter-smoke-client",
            ShooterGameplay.DefaultTickRate,
            20260610,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 5f, 0f)
            });

        var pushWait = new TaskCompletionSource<ShooterSnapshotPushSmokeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.GatewayConnection.SnapshotPushDispatched += (opCode, payload, result) =>
        {
            try
            {
                if (TryCapturePackedSnapshotPush(opCode, payload, result, out var pushResult))
                {
                    pushWait.TrySetResult(pushResult);
                }
            }
            catch (Exception ex)
            {
                pushWait.TrySetException(ex);
            }
        };

        var launched = await launcher.CreateReadyStartAndSubscribeAsync(
            host,
            port,
            runtime,
            presentationSession,
            start,
            login.SessionToken,
            ShooterRoomLaunchSpec.CreateDefault("shooter-smoke-client"),
            playerId: 1u,
            timeout: TimeSpan.FromSeconds(10));

        ValidateLaunch(launched);

        var snapshotPush = await pushWait.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (snapshotPush.ApplyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot)
        {
            throw new InvalidOperationException($"Shooter packed snapshot push was not applied. Result={snapshotPush.ApplyResult}");
        }

        if (presentation.ViewModel.Frame <= 0 || runtime.CurrentFrame <= 0)
        {
            throw new InvalidOperationException("Shooter client runtime/presentation did not advance after snapshot push.");
        }

        if (runtime.CurrentFrame < snapshotPush.PackedFrame || presentation.ViewModel.Frame < snapshotPush.PackedFrame)
        {
            throw new InvalidOperationException($"Shooter packed snapshot frame regressed behind packed state. Packed={snapshotPush.PackedFrame}, Runtime={runtime.CurrentFrame}, Presentation={presentation.ViewModel.Frame}");
        }

        var runtimeStateHash = runtime.ComputeStateHash();
        if (runtimeStateHash == 0u)
        {
            throw new InvalidOperationException("Shooter packed snapshot runtime state hash was not produced.");
        }

        var inputResults = await SubmitSmokeInputsAsync(launched, TimeSpan.FromSeconds(10));

        var frameBeforeStaleSnapshot = presentation.ViewModel.Frame;
        var staleSnapshotResult = ApplyStaleSnapshotPush(launched.Session, launched.Flow.WorldId, frameBeforeStaleSnapshot);
        if (staleSnapshotResult != ShooterSnapshotApplyResult.IgnoredStaleSnapshot)
        {
            throw new InvalidOperationException($"Shooter stale snapshot was not ignored. Result={staleSnapshotResult}");
        }

        if (presentation.ViewModel.Frame != frameBeforeStaleSnapshot)
        {
            throw new InvalidOperationException("Shooter presentation frame changed after stale snapshot push.");
        }

        var playerCount = CountCurrentEntities(presentation.ViewModel.Current, ShooterViewEntityKind.Player);
        if (playerCount == 0)
        {
            throw new InvalidOperationException("Shooter presentation has no players after snapshot push.");
        }

        var projectionResult = ValidateProjectedPresentation(projectedRecorder, playerCount, "primary client", requireFullSync: true);
        var lateJoin = await RunLateJoinProjectionSmokeAsync(
            host,
            port,
            launched.Flow.RoomId,
            start,
            playerCount,
            TimeSpan.FromSeconds(10));
        var reconnect = await RunReconnectProjectionSmokeAsync(
            host,
            port,
            launched.Flow.RoomId,
            start,
            login.AccountId,
            login.SessionToken,
            playerCount,
            TimeSpan.FromSeconds(10));

        using var inputLogicReplay = ShooterSmokeReplayRecordScope.CreateInputLogicReplay(
            inputLogicReplayOutputPath,
            launched.Flow.RoomId,
            launched.Flow.BattleId,
            launched.Flow.WorldId,
            20260610);
        var gameplayLoop = await RunCompleteGameplayLoopAsync(clusterClient, launched.Flow.BattleId, launched.Flow.WorldId, TimeSpan.FromSeconds(15), inputLogicReplay);
        var inputLogicReplayPath = inputLogicReplay?.Save() ?? string.Empty;
        var minimizedInputLogicReplayPath = inputLogicReplay?.MinimizedOutputPath ?? string.Empty;
        var inputLogicReplayValidation = ShooterSmokeReplayValidation.ValidateReplay(minimizedInputLogicReplayPath);

        var lastInput = inputResults[inputResults.Count - 1];
        var result = new ShooterSmokeResult(
            login.AccountId,
            launched.Flow.RoomId,
            launched.Flow.BattleId,
            launched.Flow.WorldId,
            launched.Flow.TargetFrame,
            inputResults.Count,
            lastInput.Local.RequestedFrame,
            lastInput.Remote.AcceptedFrame,
            lastInput.Remote.CurrentFrame,
            lastInput.Remote.Status,
            lastInput.Remote.ServerTicks,
            lastInput.Remote.ShouldResync,
            presentation.ViewModel.Frame,
            playerCount,
            runtime.ComputeStateHash(),
            snapshotPush.ApplyResult,
            snapshotPush.WireFrame,
            snapshotPush.WireServerTicks,
            snapshotPush.PayloadOpCode,
            snapshotPush.PackedFrame,
            snapshotPush.PackedServerTick,
            snapshotPush.PackedStateHash,
            snapshotPush.PackedEntityCount,
            staleSnapshotResult,
            projectedRecorder.ApplyCount,
            projectedRecorder.FullSyncApplyCount,
            projectionResult.AddedEntities,
            projectionResult.RemovedEntities,
            projectionResult.ComponentUpdates,
            projectionResult.FinalEntityCount,
            projectionResult.FinalPlayerCount,
            projectionResult.FinalBulletCount,
            lateJoin.AccountId,
            lateJoin.EntryKind,
            lateJoin.TargetFrame,
            lateJoin.ProjectionApplyCount,
            lateJoin.ProjectionFullSyncApplyCount,
            lateJoin.ProjectionAddedEntities,
            lateJoin.ProjectionRemovedEntities,
            lateJoin.ProjectionComponentUpdates,
            lateJoin.ProjectionFinalEntityCount,
            lateJoin.ProjectionFinalPlayerCount,
            lateJoin.ProjectionFinalBulletCount,
            reconnect.EntryKind,
            reconnect.TargetFrame,
            reconnect.ProjectionApplyCount,
            reconnect.ProjectionFullSyncApplyCount,
            reconnect.ProjectionAddedEntities,
            reconnect.ProjectionRemovedEntities,
            reconnect.ProjectionComponentUpdates,
            reconnect.ProjectionFinalEntityCount,
            reconnect.ProjectionFinalPlayerCount,
            reconnect.ProjectionFinalBulletCount,
            gameplayLoop.StartFrame,
            gameplayLoop.FinalFrame,
            gameplayLoop.FinalMatchState,
            gameplayLoop.MatchFinal,
            gameplayLoop.MatchVictory,
            gameplayLoop.MatchCompletedFrame,
            gameplayLoop.DefeatedEnemies,
            gameplayLoop.VictoryTargetDefeats,
            gameplayLoop.TimeLimitFrames,
            gameplayLoop.RemainingTimeFrames,
            gameplayLoop.Moved,
            gameplayLoop.Fired,
            gameplayLoop.DefeatedEnemy,
            inputLogicReplayPath,
            minimizedInputLogicReplayPath,
            inputLogicReplayValidation);

        ValidateSmokeResult(result);

        await CleanupBattleAsync(clusterClient, result);
        return result;
    }

    public static Task WaitForTcpAsync(string host, int port, TimeSpan timeout) =>
        ShooterSmokeScenarioBase.WaitForTcpAsync(host, port, timeout);

    private static Task<ShooterSmokeLogin> LoginGuestAsync(AbilityKit.Network.Abstractions.IConnection connection) =>
        ShooterSmokeScenarioBase.LoginGuestAsync(connection);

    private static Task<ShooterSmokeLogin> LoginAccountAsync(AbilityKit.Network.Abstractions.IConnection connection, string accountId, bool kickExisting = true, int expireSeconds = 0) =>
        ShooterSmokeScenarioBase.LoginAccountAsync(connection, accountId, kickExisting, expireSeconds);

    private sealed record ShooterCompleteGameplayLoopSmokeResult(
        int StartFrame,
        int FinalFrame,
        ShooterBattleMatchState FinalMatchState,
        bool MatchFinal,
        bool MatchVictory,
        int MatchCompletedFrame,
        int DefeatedEnemies,
        int VictoryTargetDefeats,
        int TimeLimitFrames,
        int RemainingTimeFrames,
        bool Moved,
        bool Fired,
        bool DefeatedEnemy);

    private static async Task<ShooterCompleteGameplayLoopSmokeResult> RunCompleteGameplayLoopAsync(
        IClusterClient clusterClient,
        string battleId,
        ulong worldId,
        TimeSpan timeout,
        ShooterSmokeReplayRecordScope? replay)
    {
        var battle = clusterClient.GetGrain<IBattleLogicHostGrain>(battleId);
        var initialSnapshot = await battle.GetSnapshotAsync();
        if (initialSnapshot == null)
        {
            throw new InvalidOperationException("Shooter gameplay loop could not read initial battle snapshot.");
        }

        var startFrame = initialSnapshot.Frame;
        var initialPlayer = initialSnapshot.Actors.FirstOrDefault(actor => actor.ActorId == 1);
        if (initialPlayer == null)
        {
            throw new InvalidOperationException("Shooter gameplay loop could not find player actor 1 in initial battle snapshot.");
        }

        replay?.RecordServerSnapshot(initialSnapshot);

        var moved = false;
        var fired = false;
        ulong commandSequence = 0;
        var deadline = DateTime.UtcNow + timeout;
        BattleSnapshot? finalSnapshot = initialSnapshot;

        while (DateTime.UtcNow < deadline)
        {
            var currentFrame = await battle.GetCurrentFrameAsync();
            var command = CreateGameplayLoopCommand(currentFrame);
            var inputPayload = ShooterInputCodec.Serialize(new[] { command });
            var submit = await battle.SubmitInputAsync(
                worldId,
                currentFrame,
                new BattleInputItem
                {
                    PlayerId = 1,
                    OpCode = ShooterOpCodes.Input.PlayerCommand,
                    Payload = inputPayload,
                    CommandSequence = ++commandSequence
                });

            if (!submit.Accepted)
            {
                throw new InvalidOperationException($"Shooter gameplay loop input was rejected. RequestedFrame={submit.RequestedFrame}, Status={submit.Status}, Message={submit.Message}");
            }

            replay?.RecordServerInput(submit.AcceptedFrame, 1u, ShooterOpCodes.Input.PlayerCommand, inputPayload);
            fired |= command.Fire;
            await Task.Delay(35);
            finalSnapshot = await battle.GetSnapshotAsync();
            replay?.RecordServerSnapshot(finalSnapshot);
            if (finalSnapshot == null)
            {
                continue;
            }

            var currentPlayer = finalSnapshot.Actors.FirstOrDefault(actor => actor.ActorId == 1);
            if (currentPlayer == null)
            {
                throw new InvalidOperationException($"Shooter gameplay loop could not find player actor 1 in battle snapshot. Frame={finalSnapshot.Frame}");
            }

            moved |= Math.Abs(currentPlayer.X - initialPlayer.X) > 0.01f || Math.Abs(currentPlayer.Z - initialPlayer.Z) > 0.01f;
            if (finalSnapshot.MatchFinal)
            {
                break;
            }
        }

        if (finalSnapshot == null)
        {
            throw new InvalidOperationException("Shooter gameplay loop did not produce a final battle snapshot.");
        }

        return new ShooterCompleteGameplayLoopSmokeResult(
            startFrame,
            finalSnapshot.Frame,
            (ShooterBattleMatchState)finalSnapshot.MatchState,
            finalSnapshot.MatchFinal,
            finalSnapshot.MatchVictory,
            finalSnapshot.MatchCompletedFrame,
            finalSnapshot.DefeatedEnemies,
            finalSnapshot.VictoryTargetDefeats,
            finalSnapshot.TimeLimitFrames,
            finalSnapshot.RemainingTimeFrames,
            moved,
            fired,
            finalSnapshot.DefeatedEnemies > 0);
    }

    private static ShooterPlayerCommand CreateGameplayLoopCommand(int frame)
    {
        const float firstEnemyX = -0.12186934f;
        const float firstEnemyY = 0.99254614f;
        var moveX = frame % 20 < 10 ? 0.35f : -0.2f;
        var moveY = frame % 30 < 15 ? 0.15f : -0.1f;
        return new ShooterPlayerCommand(1, moveX, moveY, firstEnemyX, firstEnemyY, fire: true);
    }

    private static async Task<List<ShooterClientGatewayInputSubmitResult>> SubmitSmokeInputsAsync(
        ShooterClientNetworkLaunchResult launched,
        TimeSpan timeout)
    {
        var results = new List<ShooterClientGatewayInputSubmitResult>(capacity: 3);
        var inputs = new[]
        {
            (MoveX: 1f, MoveY: 0f, AimX: 1f, AimY: 0f, Fire: true),
            (MoveX: 0.5f, MoveY: 0.25f, AimX: 1f, AimY: 0.1f, Fire: false),
            (MoveX: 0f, MoveY: 1f, AimX: 0.25f, AimY: 1f, Fire: true)
        };

        foreach (var input in inputs)
        {
            var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(
                input.MoveX,
                input.MoveY,
                input.AimX,
                input.AimY,
                input.Fire,
                timeout: timeout);

            if (!submit.Remote.Success)
            {
                throw new InvalidOperationException($"Shooter gateway input was rejected. RequestedFrame={submit.Local.RequestedFrame}, Status={submit.Remote.Status}, Message={submit.Remote.Message}");
            }

            if (submit.Remote.AcceptedFrame < submit.Local.RequestedFrame)
            {
                throw new InvalidOperationException($"Shooter gateway accepted frame regressed. RequestedFrame={submit.Local.RequestedFrame}, AcceptedFrame={submit.Remote.AcceptedFrame}");
            }

            if (submit.Remote.CurrentFrame < 0)
            {
                throw new InvalidOperationException("Shooter gateway input response returned invalid current frame.");
            }

            if (string.IsNullOrWhiteSpace(submit.Remote.Status))
            {
                throw new InvalidOperationException("Shooter gateway input response returned empty status.");
            }

            if (submit.Remote.ServerTicks <= 0)
            {
                throw new InvalidOperationException("Shooter gateway input response returned invalid server ticks.");
            }

            results.Add(submit);
        }

        return results;
    }

    private static ShooterSnapshotApplyResult ApplyStaleSnapshotPush(ShooterClientSession session, ulong worldId, int lastAppliedFrame)
    {
        var staleFrame = Math.Max(0, lastAppliedFrame - 1);
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId,
            staleFrame,
            DateTime.UtcNow.Ticks,
            ShooterPackedSnapshotFlags.Full | ShooterPackedSnapshotFlags.AuthorityOverride,
            0,
            0,
            Array.Empty<byte>(),
            Array.Empty<ShooterPackedComponentChunk>());
        var packedPayload = ShooterPackedSnapshotCodec.Serialize(in packed);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = worldId,
            Frame = staleFrame,
            Timestamp = 0d,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = packedPayload,
            ServerTicks = packed.ServerTick
        };
        var payload = WireRoomGatewayBinary.Serialize(in wire);
        return session.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload);
    }

    private static bool TryCapturePackedSnapshotPush(
        uint opCode,
        ArraySegment<byte> payload,
        ShooterSnapshotApplyResult applyResult,
        out ShooterSnapshotPushSmokeResult result)
    {
        result = default;
        if (applyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot)
        {
            return false;
        }

        if (opCode != RoomGatewayOpCodes.SnapshotPushed)
        {
            return false;
        }

        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
        if (wire.ServerTicks <= 0)
        {
            throw new InvalidOperationException("Shooter packed snapshot push returned invalid server ticks.");
        }

        if (wire.PayloadOpCode != ShooterOpCodes.Snapshot.PackedState)
        {
            throw new InvalidOperationException($"Shooter snapshot push returned unexpected payload opCode. Actual={wire.PayloadOpCode}");
        }

        if (wire.Payload == null || wire.Payload.Length == 0)
        {
            throw new InvalidOperationException("Shooter packed snapshot push returned empty payload.");
        }

        var packed = ShooterPackedSnapshotCodec.Deserialize(wire.Payload);
        if (packed.WorldId != wire.WorldId)
        {
            throw new InvalidOperationException($"Shooter packed snapshot world id mismatch. Wire={wire.WorldId}, Packed={packed.WorldId}");
        }

        if (packed.Frame != wire.Frame)
        {
            throw new InvalidOperationException($"Shooter packed snapshot frame mismatch. Wire={wire.Frame}, Packed={packed.Frame}");
        }

        if (packed.ServerTick <= 0)
        {
            throw new InvalidOperationException("Shooter packed snapshot returned invalid packed server tick.");
        }

        result = new ShooterSnapshotPushSmokeResult(
            applyResult,
            wire.WorldId,
            wire.Frame,
            wire.ServerTicks,
            wire.PayloadOpCode,
            packed.WorldId,
            packed.Frame,
            packed.ServerTick,
            packed.StateHash,
            packed.EntityCount,
            0,
            false,
            false,
            0,
            0,
            0,
            0,
            0);
        return true;
    }

    private static int CountCurrentEntities(ShooterSnapshotViewBatch batch, ShooterViewEntityKind kind)
    {
        var count = 0;
        var changes = batch.EntityChanges;
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i].Kind == kind && changes[i].Alive)
            {
                count++;
            }
        }

        return count;
    }

    private static ShooterViewProjectionApplyResult ValidateProjectedPresentation(
        RecordingProjectedViewSink recorder,
        int expectedPlayerCount,
        string label,
        bool requireFullSync,
        bool exactPlayerCount = true) =>
        ShooterSmokeScenarioBase.ValidateProjectedPresentation(
            recorder,
            expectedPlayerCount,
            label,
            requireFullSync,
            exactPlayerCount);

    private static async Task<ShooterLateJoinSmokeResult> RunLateJoinProjectionSmokeAsync(
        string host,
        int port,
        string roomId,
        ShooterStartGamePayload start,
        int expectedPlayerCount,
        TimeSpan timeout)
    {
        using var channel = new SmokeTcpGameFrameworkNetworkChannel("ShooterSmokeGatewayLateJoin");
        using var connection = GameFrameworkGatewayConnectionFactory.Wrap(channel);
        using var launcher = new ShooterClientNetworkLauncher(connection);

        connection.Open(host, port);
        connection.Tick(0f);

        var login = await LoginGuestAsync(connection);
        using var presentationContext = ShooterSmokeScenarioBase.CreatePresentationContext();
        var runtime = presentationContext.Runtime;
        var presentation = presentationContext.Presentation;
        var projectedRecorder = presentationContext.Recorder;
        var presentationSession = presentationContext.Session;

        var pushWait = new TaskCompletionSource<ShooterSnapshotApplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.GatewayConnection.SnapshotPushDispatched += (_, _, result) =>
        {
            if (result == ShooterSnapshotApplyResult.AppliedPackedSnapshot || result == ShooterSnapshotApplyResult.AppliedActorSnapshot)
            {
                pushWait.TrySetResult(result);
            }
        };

        var launched = await launcher.JoinReadyStartAndSubscribeAsync(
            host,
            port,
            runtime,
            presentationSession,
            start,
            login.SessionToken,
            roomId,
            ShooterRoomLaunchSpec.CreateDefault("shooter-smoke-late-join-client"),
            playerId: 2u,
            timeout: timeout);

        ValidateLaunch(launched);
        if (launched.Flow.EntryKind == ShooterRoomGatewayEntryKind.TeamLobby)
        {
            throw new InvalidOperationException("Shooter late join unexpectedly entered team lobby instead of a running battle.");
        }

        var applyResult = await pushWait.Task.WaitAsync(timeout);
        if (applyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot && applyResult != ShooterSnapshotApplyResult.AppliedActorSnapshot)
        {
            throw new InvalidOperationException($"Shooter late join snapshot push was not applied. Result={applyResult}");
        }

        if (presentation.ViewModel.Frame <= 0 || runtime.CurrentFrame <= 0)
        {
            throw new InvalidOperationException("Shooter late join runtime/presentation did not advance after snapshot push.");
        }

        var projectionResult = ValidateProjectedPresentation(projectedRecorder, expectedPlayerCount, "late join client", requireFullSync: false, exactPlayerCount: false);
        return new ShooterLateJoinSmokeResult(
            login.AccountId,
            launched.Flow.EntryKind,
            launched.Flow.TargetFrame,
            projectedRecorder.ApplyCount,
            projectedRecorder.FullSyncApplyCount,
            projectionResult.AddedEntities,
            projectionResult.RemovedEntities,
            projectionResult.ComponentUpdates,
            projectionResult.FinalEntityCount,
            projectionResult.FinalPlayerCount,
            projectionResult.FinalBulletCount);
    }

    private static async Task<ShooterReconnectSmokeResult> RunReconnectProjectionSmokeAsync(
        string host,
        int port,
        string roomId,
        ShooterStartGamePayload start,
        string accountId,
        string sessionToken,
        int expectedPlayerCount,
        TimeSpan timeout)
    {
        using var channel = new SmokeTcpGameFrameworkNetworkChannel("ShooterSmokeGatewayReconnect");
        using var connection = GameFrameworkGatewayConnectionFactory.Wrap(channel);
        using var launcher = new ShooterClientNetworkLauncher(connection);

        connection.Open(host, port);
        connection.Tick(0f);

        using var presentationContext = ShooterSmokeScenarioBase.CreatePresentationContext();
        var runtime = presentationContext.Runtime;
        var presentation = presentationContext.Presentation;
        var projectedRecorder = presentationContext.Recorder;
        var presentationSession = presentationContext.Session;

        var pushWait = new TaskCompletionSource<ShooterSnapshotApplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.GatewayConnection.SnapshotPushDispatched += (_, _, result) =>
        {
            if (result == ShooterSnapshotApplyResult.AppliedPackedSnapshot || result == ShooterSnapshotApplyResult.AppliedActorSnapshot)
            {
                pushWait.TrySetResult(result);
            }
        };

        var launched = await ReloginAndRestoreRoomAsync(
            launcher,
            connection,
            host,
            port,
            runtime,
            presentationSession,
            start,
            accountId,
            sessionToken,
            roomId,
            timeout);

        var applyResult = await pushWait.Task.WaitAsync(timeout);
        if (applyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot && applyResult != ShooterSnapshotApplyResult.AppliedActorSnapshot)
        {
            throw new InvalidOperationException($"Shooter reconnect snapshot push was not applied. Result={applyResult}");
        }

        if (presentation.ViewModel.Frame <= 0 || runtime.CurrentFrame <= 0)
        {
            throw new InvalidOperationException("Shooter reconnect runtime/presentation did not advance after snapshot push.");
        }

        var projectionResult = ValidateProjectedPresentation(projectedRecorder, expectedPlayerCount, "reconnect client", requireFullSync: false, exactPlayerCount: false);
        return new ShooterReconnectSmokeResult(
            accountId,
            launched.Flow.EntryKind,
            launched.Flow.TargetFrame,
            projectedRecorder.ApplyCount,
            projectedRecorder.FullSyncApplyCount,
            projectionResult.AddedEntities,
            projectionResult.RemovedEntities,
            projectionResult.ComponentUpdates,
            projectionResult.FinalEntityCount,
            projectionResult.FinalPlayerCount,
            projectionResult.FinalBulletCount);
    }

    private static async Task<ShooterClientNetworkRestoreResult> ReloginAndRestoreRoomAsync(
        ShooterClientNetworkLauncher launcher,
        AbilityKit.Network.Abstractions.IConnection connection,
        string host,
        int port,
        IShooterBattleRuntimePort runtime,
        ShooterPresentationSessionContext presentationSession,
        ShooterStartGamePayload start,
        string accountId,
        string previousSessionToken,
        string expectedRoomId,
        TimeSpan timeout)
    {
        var relogin = await LoginAccountAsync(connection, accountId, kickExisting: true);
        if (string.Equals(relogin.SessionToken, previousSessionToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Shooter reconnect account login did not rotate session token.");
        }

        var launchSpec = ShooterRoomLaunchSpec.CreateDefault("shooter-smoke-reconnect-client");
        var launched = await launcher.RestoreRoomAsync(
            host,
            port,
            runtime,
            presentationSession,
            start,
            relogin.SessionToken,
            launchSpec.Region,
            launchSpec.ServerId,
            launchSpec,
            playerId: 1u,
            timeout: timeout);

        ValidateLaunch(launched);
        if (launched.Flow.EntryKind != ShooterRoomGatewayEntryKind.Reconnect)
        {
            throw new InvalidOperationException($"Shooter reconnect expected reconnect entry kind. Actual={launched.Flow.EntryKind}");
        }

        if (!string.Equals(launched.Flow.RoomId, expectedRoomId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Shooter reconnect restored unexpected room. Expected={expectedRoomId}, Actual={launched.Flow.RoomId}");
        }

        return launched;
    }

    private static void ValidateSmokeResult(ShooterSmokeResult result) =>
        ShooterSmokeScenarioBase.ValidateSmokeResult(result);

    private static Task CleanupBattleAsync(IClusterClient clusterClient, ShooterSmokeResult result) =>
        ShooterSmokeScenarioBase.CleanupBattleAsync(clusterClient, result);

    private static void ValidateLaunch(ShooterClientNetworkLaunchResult launched)
    {
        if (!launched.Flow.Started)
        {
            throw new InvalidOperationException("Shooter gateway flow did not start battle.");
        }

        if (!launched.Flow.Subscribed)
        {
            throw new InvalidOperationException("Shooter gateway flow did not subscribe state sync.");
        }

        if (string.IsNullOrWhiteSpace(launched.Flow.BattleId))
        {
            throw new InvalidOperationException("Shooter gateway flow returned empty battle id.");
        }

        if (launched.Flow.WorldId == 0)
        {
            throw new InvalidOperationException("Shooter gateway flow returned zero world id.");
        }

        if (!launched.Flow.WorldStartAnchor.IsValid)
        {
            throw new InvalidOperationException("Shooter gateway flow returned invalid world start anchor.");
        }

        if (launched.Flow.ServerNowTicks <= 0)
        {
            throw new InvalidOperationException("Shooter gateway flow returned invalid server ticks.");
        }
    }
}
