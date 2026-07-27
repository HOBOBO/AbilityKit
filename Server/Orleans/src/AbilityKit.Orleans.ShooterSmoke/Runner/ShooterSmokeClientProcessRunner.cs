using System.Globalization;
using AbilityKit.Demo.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.LagCompensation;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

internal static class ShooterSmokeClientProcessRunner
{
    public static async Task<ShooterSmokeClientProcessResult> RunAsync(ShooterSmokeClientProcessOptions options)
    {
        if (options.Mode == ShooterSmokeClientProcessMode.Join && string.IsNullOrWhiteSpace(options.RoomId))
        {
            throw new ArgumentException("roomId is required for join client mode.", nameof(options));
        }

        await ShooterSmokeScenarioBase.WaitForTcpAsync(options.Host, options.Port, options.Timeout);

        using var replay = ShooterSmokeReplayRecordScope.CreateInputStateReplay(options.InputStateReplayOutputPath, in options);
        using var channel = new SmokeTcpGameFrameworkNetworkChannel($"ShooterSmokeGateway-{options.ClientId}", options.NetworkCondition.Normalize());
        using var soakTelemetry = new ShooterSmokeSoakTelemetry(
            options.ClientId,
            options.RunRootPath,
            options.NetworkControlPath,
            options.MetricsOutputPath);
        using var connection = GameFrameworkGatewayConnectionFactory.Wrap(channel);
        using var launcher = new ShooterClientNetworkLauncher(connection);

        connection.Open(options.Host, options.Port);
        connection.Tick(0f);

        var login = await ShooterSmokeScenarioBase.LoginGuestAsync(connection);
        var presentationContext = ShooterSmokeScenarioBase.CreatePresentationContext();
        var runtime = presentationContext.Runtime;
        var presentation = presentationContext.Presentation;
        var session = presentationContext.Session;
        var start = CreateStartGame(options.Seed);

        var pushWait = new TaskCompletionSource<ShooterSnapshotPushSmokeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pushCount = 0;
        var lastPush = default(ShooterSnapshotPushSmokeResult);
        var latestComparableSnapshotFrame = 0;
        var latestComparableAuthoritativeHash = 0u;
        var latestComparableClientHash = 0u;
        var firstAppliedSnapshotHashMatched = true;
        var fullSnapshotsApplied = 0;
        var pureStateFullBaselinesApplied = 0;
        var pureStateDeltasApplied = 0;
        var pureStateResyncRequests = 0;
        launcher.GatewayConnection.SnapshotPushDispatched += (opCode, payload, result) =>
        {
            try
            {
                if (TryCaptureSnapshotPush(opCode, payload, result, out var pushResult))
                {
                    replay?.RecordSnapshot(in pushResult, payload);
                    pushCount++;
                    lastPush = pushResult;
                    if (pushResult.IsFullSnapshot && IsAppliedSnapshotResult(pushResult.ApplyResult))
                    {
                        fullSnapshotsApplied++;
                    }

                    if (pushResult.ApplyResult == ShooterSnapshotApplyResult.AppliedActorSnapshot)
                    {
                        if (pushResult.PureStateSnapshotKind == ShooterPureStateSnapshotKinds.FullBaseline)
                        {
                            pureStateFullBaselinesApplied++;
                        }
                        else if (pushResult.PureStateSnapshotKind == ShooterPureStateSnapshotKinds.Delta
                            || pushResult.PureStateSnapshotKind == ShooterPureStateSnapshotKinds.LowFrequency)
                        {
                            pureStateDeltasApplied++;
                        }
                    }
                    else if (pushResult.ApplyResult == ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded)
                    {
                        pureStateResyncRequests++;
                    }

                    var comparableClientHash = 0u;
                    var sampleSource = "none";
                    var importedEvidence = launcher.GatewayConnection.CurrentSession?.FrameSync.LastImportedSnapshotEvidence
                        ?? ShooterClientImportedSnapshotEvidence.None;
                    if (pushResult.ApplyResult == ShooterSnapshotApplyResult.AppliedPackedSnapshot
                        && pushResult.PackedStateHash != 0u)
                    {
                        if (importedEvidence.Frame == pushResult.PackedFrame
                            && importedEvidence.AuthoritativeStateHash == pushResult.PackedStateHash
                            && importedEvidence.ImportedStateHash != 0u)
                        {
                            comparableClientHash = importedEvidence.ImportedStateHash;
                            sampleSource = "imported";
                        }
                        else if (runtime.CurrentFrame == pushResult.PackedFrame)
                        {
                            comparableClientHash = runtime.ComputeStateHash();
                            sampleSource = "runtime";
                        }
                    }
                    else if (TryCapturePureStateComparableHash(
                        in pushResult,
                        presentation.LastPureStateAppliedFrame,
                        presentation.LastPureStateAppliedStateHash,
                        out comparableClientHash))
                    {
                        sampleSource = "pure-state";
                    }

                    if (comparableClientHash != 0u)
                    {
                        latestComparableSnapshotFrame = pushResult.PackedFrame;
                        latestComparableAuthoritativeHash = pushResult.PackedStateHash;
                        latestComparableClientHash = comparableClientHash;
                    }

                    if (pushResult.PackedStateHash != 0u)
                    {
                        Console.WriteLine(
                            $"SHOOTER_MP_HASH_SAMPLE status={(comparableClientHash != 0u ? "accepted" : "rejected")} " +
                            $"source={sampleSource} pushFrame={pushResult.PackedFrame} runtimeFrame={runtime.CurrentFrame} " +
                            $"evidenceFrame={importedEvidence.Frame} authoritativeHash=0x{pushResult.PackedStateHash:X8} " +
                            $"evidenceAuthoritativeHash=0x{importedEvidence.AuthoritativeStateHash:X8} " +
                            $"clientHash=0x{comparableClientHash:X8}");
                    }

                    if (IsAppliedSnapshotResult(result)
                        && pushWait.TrySetResult(pushResult)
                        && comparableClientHash != 0u)
                    {
                        firstAppliedSnapshotHashMatched = comparableClientHash == pushResult.PackedStateHash;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SHOOTER_MP_SNAPSHOT_CALLBACK_FAILURE threadId={Environment.CurrentManagedThreadId} " +
                    $"runtimeFrame={runtime.CurrentFrame} pushes={pushCount} exception={ex}");
                pushWait.TrySetException(ex);
            }
        };

        var launchSpec = CreateRoomLaunchSpec(
            options.ClientId,
            options.RoomMaxPlayers,
            options.BattleDurationFrames,
            options.BattleVictoryTargetDefeats,
            options.ContinueAfterAllPlayersDefeated);
        if (options.Mode == ShooterSmokeClientProcessMode.Create)
        {
            launchSpec.Tags.TryGetValue(ShooterRoomLaunchTagKeys.DurationFrames, out var durationFramesTag);
            launchSpec.Tags.TryGetValue(ShooterRoomLaunchTagKeys.VictoryTargetDefeats, out var victoryTargetDefeatsTag);
            Console.WriteLine(
                $"SHOOTER_MP_LAUNCH_SPEC clientId=\"{Escape(options.ClientId)}\" " +
                $"battleDurationFrames={options.BattleDurationFrames} " +
                $"battleVictoryTargetDefeats={options.BattleVictoryTargetDefeats} " +
                $"durationFramesTag=\"{Escape(durationFramesTag ?? string.Empty)}\" " +
                $"victoryTargetDefeatsTag=\"{Escape(victoryTargetDefeatsTag ?? string.Empty)}\"");
        }

        var launched = options.Mode == ShooterSmokeClientProcessMode.Create
            ? await launcher.CreateReadyStartAndSubscribeAsync(
                options.Host,
                options.Port,
                runtime,
                session,
                start,
                login.SessionToken,
                launchSpec,
                options.PlayerId,
                timeout: options.Timeout)
            : await launcher.JoinReadyStartAndSubscribeAsync(
                options.Host,
                options.Port,
                runtime,
                session,
                start,
                login.SessionToken,
                options.RoomId,
                launchSpec,
                options.PlayerId,
                timeout: options.Timeout);

        ValidateLaunch(launched);
        replay?.RecordLaunch(login.AccountId, launched);
        Console.WriteLine(FormatReady(options, login.AccountId, launched));

        var resultTimeout = CreateResultTimeout(options.Timeout);
        if (ShouldRequestInitialFullStateSync(launched.Flow.EntryKind))
        {
            await RequestInitialFullStateSyncWhileTickingAsync(
                launched,
                launcher,
                () => fullSnapshotsApplied,
                resultTimeout);
        }

        var push = await WaitForPushWhileTickingAsync(
            pushWait.Task,
            launcher,
            resultTimeout,
            () => BuildPushWaitDiagnostics(pushCount, in lastPush, channel, connection));
        ValidateAppliedSnapshot(push, runtime, presentation);

        var inputResults = await SubmitInputsAsync(launched, options.InputCount, resultTimeout, replay);
        var reconnectResult = default(ShooterSmokeReconnectProcessResult);
        if (options.ReconnectCount > 0)
        {
            await WaitForReconnectReleaseAsync(options, inputResults.Count, resultTimeout).ConfigureAwait(false);
            reconnectResult = await ReconnectAsync(
                connection,
                launcher,
                runtime,
                session,
                start,
                login.SessionToken,
                launchSpec,
                options,
                () => pushCount,
                nextPushWait => pushWait = nextPushWait,
                resultTimeout);
            launched = reconnectResult.Launched;
        }

        replay?.RecordReconnect(in reconnectResult);
        await WaitForCompletionReleaseWhileTickingAsync(
            options,
            launched,
            launcher,
            channel,
            connection,
            login.SessionToken,
            launched.Flow.RoomId,
            launched.Flow.BattleId,
            soakTelemetry,
            () => pushCount,
            () => fullSnapshotsApplied,
            () => pureStateFullBaselinesApplied,
            () => pureStateResyncRequests,
            () => latestComparableSnapshotFrame,
            () => latestComparableAuthoritativeHash != 0u
                && latestComparableClientHash == latestComparableAuthoritativeHash,
            resultTimeout).ConfigureAwait(false);

        var deliveryMetrics = await GetStateSyncDeliveryMetricsAsync(
            launched.GatewayConnection,
            login.SessionToken,
            launched.Flow.RoomId,
            launched.Flow.BattleId,
            resultTimeout).ConfigureAwait(false);

        if (options.WaitForMatchEnd)
        {
            await WaitForMatchEndAsync(launcher, launched, runtime, inputResults, options, () => lastPush, replay);
        }

        var hasInput = inputResults.Count > 0;
        var lastInput = hasInput ? inputResults[inputResults.Count - 1] : default;
        var matchResult = lastPush;

        connection.Tick(0f);
        var reconciliation = launched.Session.LastReconciliationResult;
        var snapshotHashMatched = ValidateLatestAuthoritativeSnapshot(
            push,
            reconciliation,
            firstAppliedSnapshotHashMatched);
        var remoteAnchor = launched.Flow.RemoteTimeAnchorProjection;
        var lagCompensation = EvaluateLagCompensationSmoke(runtime, launched.Flow.PlayerId);
        var finalRuntimeFrame = runtime.CurrentFrame;
        var finalViewFrame = presentation.ViewModel.Frame;
        var finalStateHash = runtime.ComputeStateHash();

        var result = new ShooterSmokeClientProcessResult(
            options.Mode,
            options.StateSyncPayloadMode,
            options.ClientId,
            login.AccountId,
            launched.Flow.RoomId,
            launched.Flow.BattleId,
            launched.Flow.WorldId,
            launched.Flow.PlayerId,
            launched.Flow.EntryKind,
            launched.Flow.TargetFrame,
            finalRuntimeFrame,
            finalViewFrame,
            finalStateHash,
            push.ApplyResult,
            push.PackedFrame,
            push.PayloadOpCode,
            push.PureStateSnapshotKind,
            push.PureStateBaselineFrame,
            push.PureStateBaselineHash,
            push.PackedStateHash,
            push.PackedEntityCount,
            push.PureStateVisibilityHintCount,
            pureStateFullBaselinesApplied,
            pureStateDeltasApplied,
            pureStateResyncRequests,
            lastPush.ApplyResult == ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded,
            snapshotHashMatched,
            reconciliation.PredictedFrameBeforeCorrection,
            reconciliation.PredictedHashBeforeCorrection,
            reconciliation.AuthoritativeFrame,
            reconciliation.AuthoritativeStateHash,
            reconciliation.ImportedStateHash,
            reconciliation.AuthoritativeHashMatched,
            reconciliation.ReplayTicks,
            reconciliation.FinalFrame,
            reconciliation.FinalStateHash,
            reconciliation.PendingInputFramesBeforeCorrection,
            reconciliation.PendingInputFramesAfterTrim,
            reconciliation.PendingInputFramesAfterReplay,
            inputResults.Count,
            !hasInput || lastInput.Remote.Success,
            hasInput ? lastInput.Local.RequestedFrame : 0,
            hasInput ? lastInput.Remote.AcceptedFrame : 0,
            hasInput ? lastInput.Remote.CurrentFrame : 0,
            hasInput ? lastInput.Remote.Status : "none",
            hasInput ? lastInput.Remote.ServerTicks : 0L,
            hasInput && lastInput.Remote.ShouldResync,
            pushCount,
            lastPush.PackedFrame,
            matchResult.MatchState,
            matchResult.MatchFinal,
            matchResult.MatchVictory,
            matchResult.MatchCompletedFrame,
            matchResult.DefeatedEnemies,
            matchResult.VictoryTargetDefeats,
            matchResult.TimeLimitFrames,
            matchResult.RemainingTimeFrames,
            reconnectResult.ReconnectCount,
            reconnectResult.EntryKind,
            reconnectResult.TargetFrame,
            reconnectResult.PushesBefore,
            reconnectResult.PushesAfter,
            reconnectResult.RetryAttemptCount,
            reconnectResult.InjectedFailureCount,
            channel.NetworkCondition.InboundLatencyMs,
            channel.NetworkCondition.InboundJitterMs,
            channel.NetworkCondition.InboundPacketLossRate,
            channel.ConditionInboundReceived,
            channel.ConditionInboundDelayed,
            channel.ConditionInboundDropped,
            remoteAnchor.AnchorValid,
            remoteAnchor.TargetFrame,
            remoteAnchor.CatchUpFrames,
            remoteAnchor.ElapsedSeconds,
            remoteAnchor.ServerNowTicks,
            push.WireServerTicks,
            lastPush.WireServerTicks,
            lastPush.PackedServerTick,
            finalRuntimeFrame,
            finalViewFrame,
            lagCompensation.Accepted,
            lagCompensation.Reason,
            lagCompensation.RequestedFrame,
            lagCompensation.ResolvedFrame,
            lagCompensation.HitEntityId,
            lagCompensation.Distance,
            string.Empty,
            string.Empty,
            default,
            options.RunId,
            options.CorrelationId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        replay?.RecordResult(in result);
        var replayPath = replay?.Save() ?? string.Empty;
        var minimizedReplayPath = replay?.MinimizedOutputPath ?? string.Empty;
        var replayValidation = ShooterSmokeReplayValidation.ValidateReplay(minimizedReplayPath);
        var correlation = new SyncCorrelationContext(
            options.CorrelationId,
            runId: options.RunId,
            sessionId: options.CorrelationId,
            accountId: login.AccountId,
            playerId: launched.Flow.PlayerId.ToString(CultureInfo.InvariantCulture),
            roomId: launched.Flow.RoomId,
            battleId: launched.Flow.BattleId,
            worldId: launched.Flow.WorldId.ToString(CultureInfo.InvariantCulture),
            observerId: $"{login.AccountId}:{launched.Flow.RoomId}",
            syncMode: options.StateSyncPayloadMode,
            tick: lastPush.PackedServerTick,
            commandSequence: hasInput ? lastInput.Remote.CommandSequence : 0UL,
            snapshotSequence: lastPush.PackedFrame,
            snapshotBaseline: lastPush.PureStateBaselineFrame,
            reliableEventSequence: launched.Session.LastReliableEventAck,
            reliableEventEpoch: launched.Session.ReliableEventEpoch);
        var hasComparableReconciliation =
            reconciliation.ApplyResult == ShooterSnapshotApplyResult.AppliedPackedSnapshot
            && reconciliation.AuthoritativeFrame > 0
            && reconciliation.AuthoritativeStateHash != 0u
            && reconciliation.ImportedStateHash != 0u;
        var hasComparableAppliedSnapshot =
            latestComparableSnapshotFrame > 0
            && latestComparableAuthoritativeHash != 0u
            && latestComparableClientHash != 0u;
        var authoritativeFrame = hasComparableReconciliation
            ? reconciliation.AuthoritativeFrame
            : hasComparableAppliedSnapshot ? latestComparableSnapshotFrame : 0;
        var authoritativeHash = hasComparableReconciliation
            ? reconciliation.AuthoritativeStateHash
            : hasComparableAppliedSnapshot ? latestComparableAuthoritativeHash : 0u;
        var clientFrame = authoritativeFrame;
        var clientHash = hasComparableReconciliation
            ? reconciliation.ImportedStateHash
            : hasComparableAppliedSnapshot ? latestComparableClientHash : 0u;
        var capture = new ShooterSmokeDiagnosticCapture(
            correlation,
            launched.Session.LastFastReconnectHealthEvents,
            pushCount,
            channel.ConditionInboundReceived,
            channel.ConditionInboundDropped,
            pureStateFullBaselinesApplied,
            pureStateDeltasApplied,
            pureStateResyncRequests,
            deliveryMetrics.QueueLength,
            deliveryMetrics.DroppedBytes,
            deliveryMetrics.MergedBytes,
            deliveryMetrics.ResyncCount,
            launched.Session.ReliableEventEpoch,
            launched.Session.LastReliableEventAck,
            launched.Session.NeedsReliableEventResync,
            replayPath,
            minimizedReplayPath,
            authoritativeFrame,
            authoritativeHash,
            clientFrame,
            clientHash);
        var diagnostics = ShooterSmokeDiagnosticArtifactWriter.Write(
            options.DiagnosticOutputPath,
            options.RunRootPath,
            in capture);
        return result with
        {
            InputStateReplayPath = replayPath,
            MinimizedInputStateReplayPath = minimizedReplayPath,
            InputStateReplayValidation = replayValidation,
            DiagnosticArtifactPath = diagnostics.ArtifactPath,
            DiagnosticArtifactSha256 = diagnostics.ArtifactSha256,
            DiffPath = diagnostics.DiffPath,
            DiffSha256 = diagnostics.DiffSha256,
            DiffStatus = diagnostics.DiffStatus,
        };
    }

    public static string FormatReady(
        ShooterSmokeClientProcessOptions options,
        string accountId,
        ShooterClientNetworkLaunchResult launched)
    {
        return "SHOOTER_MP_CLIENT_READY " +
            $"mode={options.Mode.ToString().ToLowerInvariant()} " +
            $"payloadMode={options.StateSyncPayloadMode} " +
            $"clientId=\"{Escape(options.ClientId)}\" " +
            $"accountId=\"{Escape(accountId)}\" " +
            $"roomId=\"{Escape(launched.Flow.RoomId)}\" " +
            $"battleId=\"{Escape(launched.Flow.BattleId)}\" " +
            $"worldId={launched.Flow.WorldId} " +
            $"playerId={launched.Flow.PlayerId} " +
            $"entryKind={launched.Flow.EntryKind} " +
            $"targetFrame={launched.Flow.TargetFrame}";
    }

    public static string FormatResult(in ShooterSmokeClientProcessResult result)
    {
        return "SHOOTER_MP_CLIENT_RESULT " +
            $"status=pass mode={result.Mode.ToString().ToLowerInvariant()} " +
            $"payloadMode={result.StateSyncPayloadMode} " +
            $"clientId=\"{Escape(result.ClientId)}\" " +
            $"runId=\"{Escape(result.RunId)}\" " +
            $"correlationId=\"{Escape(result.CorrelationId)}\" " +
            $"accountId=\"{Escape(result.AccountId)}\" " +
            $"roomId=\"{Escape(result.RoomId)}\" " +
            $"battleId=\"{Escape(result.BattleId)}\" " +
            $"worldId={result.WorldId} " +
            $"playerId={result.PlayerId} " +
            $"entryKind={result.EntryKind} " +
            $"targetFrame={result.TargetFrame} " +
            $"remoteAnchorValid={result.RemoteAnchorValid} " +
            $"remoteTargetFrame={result.RemoteTargetFrame} " +
            $"remoteCatchUpFrames={result.RemoteCatchUpFrames} " +
            $"remoteElapsedSeconds={result.RemoteElapsedSeconds.ToString(CultureInfo.InvariantCulture)} " +
            $"remoteServerTicks={result.RemoteServerTicks} " +
            $"runtimeFrame={result.RuntimeFrame} " +
            $"viewFrame={result.ViewFrame} " +
            $"localRuntimeFrame={result.LocalRuntimeFrame} " +
            $"localViewFrame={result.LocalViewFrame} " +
            $"stateHash=0x{result.StateHash:X8} " +
            $"snapshot={result.SnapshotApplyResult}@{result.SnapshotFrame} " +
            $"payloadOpCode={result.SnapshotPayloadOpCode} " +
            $"payloadKind={result.SnapshotPayloadKind} " +
            $"sourceFrame={result.SnapshotFrame} " +
            $"baselineFrame={result.SnapshotBaselineFrame} " +
            $"baselineHash=0x{result.SnapshotBaselineHash:X8} " +
            $"snapshotHash=0x{result.SnapshotStateHash:X8} " +
            $"snapshotHashMatched={result.SnapshotHashMatched} " +
            $"entities={result.SnapshotEntityCount} " +
            $"visibilityHints={result.SnapshotVisibilityHintCount} " +
            $"pureStateFullBaselinesApplied={result.PureStateFullBaselinesApplied} " +
            $"pureStateDeltasApplied={result.PureStateDeltasApplied} " +
            $"pureStateResyncRequests={result.PureStateResyncRequests} " +
            $"pureStateLastResyncNeeded={result.PureStateLastResyncNeeded} " +
            $"reconcilePredictedFrame={result.ReconcilePredictedFrame} " +
            $"reconcilePredictedHash=0x{result.ReconcilePredictedHash:X8} " +
            $"reconcileAuthoritativeFrame={result.ReconcileAuthoritativeFrame} " +
            $"reconcileAuthoritativeHash=0x{result.ReconcileAuthoritativeHash:X8} " +
            $"reconcileImportedHash=0x{result.ReconcileImportedHash:X8} " +
            $"reconcileAuthoritativeHashMatched={result.ReconcileAuthoritativeHashMatched} " +
            $"reconcileReplayTicks={result.ReconcileReplayTicks} " +
            $"reconcileFinalFrame={result.ReconcileFinalFrame} " +
            $"reconcileFinalHash=0x{result.ReconcileFinalHash:X8} " +
            $"reconcilePendingBefore={result.ReconcilePendingBefore} " +
            $"reconcilePendingAfterTrim={result.ReconcilePendingAfterTrim} " +
            $"reconcilePendingAfterReplay={result.ReconcilePendingAfterReplay} " +
            $"inputs={result.InputCount} " +
            $"lastInputSuccess={result.LastInputSuccess} " +
            $"lastRequestedFrame={result.LastRequestedFrame} " +
            $"lastAcceptedFrame={result.LastAcceptedFrame} " +
            $"lastCurrentFrame={result.LastCurrentFrame} " +
            $"lastInputStatus=\"{Escape(result.LastInputStatus)}\" " +
            $"lastServerTicks={result.LastServerTicks} " +
            $"shouldResync={result.ShouldResync} " +
            $"pushes={result.PushCount} " +
            $"snapshotServerTicks={result.SnapshotServerTicks} " +
            $"lastPushServerTicks={result.LastPushServerTicks} " +
            $"lastPushPackedServerTick={result.LastPushPackedServerTick} " +
            $"lastPushFrame={result.LastPushFrame} " +
            $"lagCompAccepted={result.LagCompAccepted} " +
            $"lagCompReason={result.LagCompReason} " +
            $"lagCompRequestedFrame={result.LagCompRequestedFrame} " +
            $"lagCompResolvedFrame={result.LagCompResolvedFrame} " +
            $"lagCompHitEntityId={result.LagCompHitEntityId} " +
            $"lagCompDistance={result.LagCompDistance.ToString(CultureInfo.InvariantCulture)} " +
            $"matchState={result.MatchState} " +
            $"matchFinal={result.MatchFinal} " +
            $"matchVictory={result.MatchVictory} " +
            $"matchCompletedFrame={result.MatchCompletedFrame} " +
            $"defeatedEnemies={result.DefeatedEnemies} " +
            $"victoryTargetDefeats={result.VictoryTargetDefeats} " +
            $"timeLimitFrames={result.TimeLimitFrames} " +
            $"remainingTimeFrames={result.RemainingTimeFrames} " +
            $"reconnectCount={result.ReconnectCount} " +
            $"reconnectEntryKind={result.ReconnectEntryKind} " +
            $"reconnectTargetFrame={result.ReconnectTargetFrame} " +
            $"reconnectPushesBefore={result.ReconnectPushesBefore} " +
            $"reconnectPushesAfter={result.ReconnectPushesAfter} " +
            $"retryAttemptCount={result.RetryAttemptCount} " +
            $"injectedFailureCount={result.InjectedFailureCount} " +
            $"conditionLatencyMs={result.ConditionLatencyMs} " +
            $"conditionJitterMs={result.ConditionJitterMs} " +
            $"conditionPacketLossRate={result.ConditionPacketLossRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
            $"conditionInboundReceived={result.ConditionInboundReceived} " +
            $"conditionInboundDelayed={result.ConditionInboundDelayed} " +
            $"conditionInboundDropped={result.ConditionInboundDropped} " +
            $"inputStateReplayPath=\"{Escape(result.InputStateReplayPath)}\" " +
            $"minimizedInputStateReplayPath=\"{Escape(result.MinimizedInputStateReplayPath)}\" " +
            $"inputStateReplayConsumed={result.InputStateReplayValidation.Consumed} " +
            $"inputStateReplayInputs={result.InputStateReplayValidation.InputCount} " +
            $"inputStateReplaySnapshots={result.InputStateReplayValidation.SnapshotCount} " +
            $"inputStateReplayHashes={result.InputStateReplayValidation.StateHashCount} " +
            $"inputStateReplayFirstFrame={result.InputStateReplayValidation.Summary.FirstFrame} " +
            $"inputStateReplayLastFrame={result.InputStateReplayValidation.Summary.LastFrame} " +
            $"inputStateReplaySnapshotOpCodes=\"{Escape(result.InputStateReplayValidation.Summary.SnapshotOpCodeDistribution)}\" " +
            $"inputStateReplayPureStateSnapshots={result.InputStateReplayValidation.Summary.PureStateRelatedSnapshotCount} " +
            $"inputStateReplayPackedStateSnapshots={result.InputStateReplayValidation.Summary.PackedStateRelatedSnapshotCount} " +
            $"clientStateReplayPath=\"{Escape(result.InputStateReplayPath)}\" " +
            $"minimizedClientStateReplayPath=\"{Escape(result.MinimizedInputStateReplayPath)}\" " +
            $"clientStateReplayConsumed={result.InputStateReplayValidation.Consumed} " +
            $"clientStateReplayInputs={result.InputStateReplayValidation.InputCount} " +
            $"clientStateReplaySnapshots={result.InputStateReplayValidation.SnapshotCount} " +
            $"clientStateReplayHashes={result.InputStateReplayValidation.StateHashCount} " +
            $"clientStateReplayFirstFrame={result.InputStateReplayValidation.Summary.FirstFrame} " +
            $"clientStateReplayLastFrame={result.InputStateReplayValidation.Summary.LastFrame} " +
            $"clientStateReplaySnapshotOpCodes=\"{Escape(result.InputStateReplayValidation.Summary.SnapshotOpCodeDistribution)}\" " +
            $"clientStateReplayPureStateSnapshots={result.InputStateReplayValidation.Summary.PureStateRelatedSnapshotCount} " +
            $"clientStateReplayPackedStateSnapshots={result.InputStateReplayValidation.Summary.PackedStateRelatedSnapshotCount} " +
            $"diagnosticArtifactPath=\"{Escape(result.DiagnosticArtifactPath)}\" " +
            $"diagnosticArtifactSha256=\"{Escape(result.DiagnosticArtifactSha256)}\" " +
            $"diffPath=\"{Escape(result.DiffPath)}\" " +
            $"diffSha256=\"{Escape(result.DiffSha256)}\" " +
            $"diffStatus=\"{Escape(result.DiffStatus)}\"";
    }

    public static string FormatFailure(in ShooterSmokeClientProcessOptions options, Exception exception)
    {
        var diagnostics = TryWriteFailureDiagnostics(in options);
        return "SHOOTER_MP_CLIENT_RESULT " +
            $"status=fail mode={options.Mode.ToString().ToLowerInvariant()} " +
            $"payloadMode={options.StateSyncPayloadMode} " +
            $"clientId=\"{Escape(options.ClientId)}\" " +
            $"runId=\"{Escape(options.RunId)}\" " +
            $"correlationId=\"{Escape(options.CorrelationId)}\" " +
            $"diagnosticArtifactPath=\"{Escape(diagnostics.ArtifactPath)}\" " +
            $"diagnosticArtifactSha256=\"{Escape(diagnostics.ArtifactSha256)}\" " +
            $"diffPath=\"{Escape(diagnostics.DiffPath)}\" " +
            $"diffSha256=\"{Escape(diagnostics.DiffSha256)}\" " +
            $"diffStatus=\"{Escape(diagnostics.DiffStatus)}\" " +
            $"error=\"{Escape(GetExceptionMessage(exception))}\"";
    }

    private static ShooterSmokeDiagnosticWriteResult TryWriteFailureDiagnostics(
        in ShooterSmokeClientProcessOptions options)
    {
        try
        {
            var context = new SyncCorrelationContext(
                options.CorrelationId,
                runId: options.RunId,
                sessionId: options.CorrelationId,
                playerId: options.PlayerId.ToString(CultureInfo.InvariantCulture),
                roomId: options.RoomId,
                syncMode: options.StateSyncPayloadMode);
            var capture = new ShooterSmokeDiagnosticCapture(
                context,
                Array.Empty<SyncHealthEvent>(),
                SnapshotPushes: 0,
                NetworkInboundReceived: 0,
                NetworkInboundDropped: 0,
                PureStateFullBaselinesApplied: 0,
                PureStateDeltasApplied: 0,
                BaselineResyncRequests: 0,
                ServerQueueLength: null,
                ServerDroppedItems: null,
                ServerCoalescedItems: null,
                ServerBaselineInvalidations: null,
                ReliableEventEpoch: string.Empty,
                LastReliableEventAck: 0L,
                NeedsReliableEventResync: false,
                ReplayPath: options.InputStateReplayOutputPath,
                MinimizedReplayPath: string.Empty,
                AuthoritativeFrame: 0,
                AuthoritativeStateHash: 0u,
                ClientFrame: 0,
                ClientStateHash: 0u);
            return ShooterSmokeDiagnosticArtifactWriter.Write(
                options.DiagnosticOutputPath,
                options.RunRootPath,
                in capture);
        }
        catch
        {
            return default;
        }
    }

    private static ShooterRoomLaunchSpec CreateRoomLaunchSpec(
        string clientId,
        int roomMaxPlayers,
        int battleDurationFrames,
        int battleVictoryTargetDefeats,
        bool continueAfterAllPlayersDefeated)
    {
        var defaults = ShooterRoomLaunchSpec.CreateDefault(clientId);
        if (roomMaxPlayers <= 0
            && battleDurationFrames <= 0
            && battleVictoryTargetDefeats <= 0
            && !continueAfterAllPlayersDefeated)
        {
            return defaults;
        }

        var tags = new Dictionary<string, string>(defaults.Tags);
        if (battleDurationFrames > 0)
        {
            tags[ShooterRoomLaunchTagKeys.DurationFrames] = battleDurationFrames.ToString(CultureInfo.InvariantCulture);
        }
        if (battleVictoryTargetDefeats > 0)
        {
            tags[ShooterRoomLaunchTagKeys.VictoryTargetDefeats] = battleVictoryTargetDefeats.ToString(CultureInfo.InvariantCulture);
        }
        if (continueAfterAllPlayersDefeated)
        {
            tags[ShooterRoomLaunchTagKeys.ContinueAfterAllPlayersDefeated] = bool.TrueString;
        }

        return new ShooterRoomLaunchSpec(
            defaults.Region,
            defaults.ServerId,
            defaults.RoomTitle,
            roomMaxPlayers > 0 ? roomMaxPlayers : defaults.MaxPlayers,
            defaults.GameplayId,
            defaults.RuleSetId,
            defaults.ConfigVersion,
            defaults.ProtocolVersion,
            defaults.WorldType,
            defaults.ClientId,
            tags,
            defaults.SyncTemplateId,
            defaults.SyncModel,
            defaults.NetworkEnvironmentId,
            defaults.CarrierName,
            defaults.EnableAuthoritativeWorld,
            defaults.InterpolationEnabled,
            defaults.InputDelayFrames);
    }

    private static ShooterStartGamePayload CreateStartGame(int seed)
    {
        return new ShooterStartGamePayload(
            "shooter-multiprocess-client",
            ShooterGameplay.DefaultTickRate,
            seed,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 5f, 0f),
                new ShooterStartPlayer(3, "P3", -5f, 0f),
                new ShooterStartPlayer(4, "P4", 0f, 5f)
            });
    }

    private static async Task WaitForMatchEndAsync(
        ShooterClientNetworkLauncher launcher,
        ShooterClientNetworkLaunchResult launched,
        ShooterBattleRuntimePort runtime,
        List<ShooterClientGatewayInputSubmitResult> inputResults,
        ShooterSmokeClientProcessOptions options,
        Func<ShooterSnapshotPushSmokeResult> getLastPush,
        ShooterSmokeReplayRecordScope? replay)
    {
        var deadline = DateTime.UtcNow + CreateResultTimeout(options.Timeout);
        var lastFrame = runtime.CurrentFrame;
        while (DateTime.UtcNow < deadline)
        {
            launcher.Tick(1f / ShooterGameplay.DefaultTickRate);
            var lastPush = getLastPush();
            if (lastPush.MatchFinal)
            {
                return;
            }

            if (options.Mode == ShooterSmokeClientProcessMode.Create)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var requestTimeout = remaining < TimeSpan.FromSeconds(2) ? remaining : TimeSpan.FromSeconds(2);
                var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(
                    CreateGameplayLoopCommand(runtime.CurrentFrame),
                    timeout: requestTimeout);
                ValidateInput(submit);
                replay?.RecordInput(in submit);
                inputResults.Add(submit);
            }

            await Task.Delay(35);
            lastPush = getLastPush();
            if (lastPush.PackedFrame > lastFrame)
            {
                lastFrame = lastPush.PackedFrame;
            }
        }

        var current = getLastPush();
        throw new InvalidOperationException($"Shooter multiprocess client did not observe final match state. Mode={options.Mode}, ClientId={options.ClientId}, State={current.MatchState}, Final={current.MatchFinal}, RuntimeFrame={runtime.CurrentFrame}, LastPushFrame={lastFrame}, CompletedFrame={current.MatchCompletedFrame}");
    }

    private static TimeSpan CreateResultTimeout(TimeSpan timeout)
    {
        var safetyMargin = TimeSpan.FromSeconds(5);
        var minimum = TimeSpan.FromSeconds(1);
        return timeout > safetyMargin + minimum ? timeout - safetyMargin : minimum;
    }

    private static bool ShouldRequestInitialFullStateSync(ShooterRoomGatewayEntryKind entryKind)
    {
        return entryKind == ShooterRoomGatewayEntryKind.LateJoin
            || entryKind == ShooterRoomGatewayEntryKind.Reconnect;
    }

    private static async Task<WireGetStateSyncDeliveryMetricsRes> GetStateSyncDeliveryMetricsAsync(
        IShooterRoomGatewayRequestTransport transport,
        string sessionToken,
        string roomId,
        string battleId,
        TimeSpan timeout)
    {
        var request = new WireGetStateSyncDeliveryMetricsReq
        {
            SessionToken = sessionToken,
            RoomId = roomId,
            BattleId = battleId
        };
        var payload = WireRoomGatewayBinary.Serialize(in request);
        var responsePayload = await transport.SendRequestAsync(
            RoomGatewayOpCodes.GetStateSyncDeliveryMetrics,
            payload,
            timeout).ConfigureAwait(false);
        var response = WireRoomGatewayBinary.Deserialize<WireGetStateSyncDeliveryMetricsRes>(responsePayload);
        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"State-sync delivery metrics request failed. RoomId={roomId}, BattleId={battleId}, Message={response.Message}");
        }

        return response;
    }

    private static Task RequestInitialFullStateSyncWhileTickingAsync(
        ShooterClientNetworkLaunchResult launched,
        ShooterClientNetworkLauncher launcher,
        Func<int> getFullSnapshotsApplied,
        TimeSpan timeout)
    {
        return RequestFullStateSyncWhileTickingAsync(
            launched,
            launcher,
            getFullSnapshotsApplied,
            timeout,
            ShooterClientResyncReason.None.ToString(),
            "initial");
    }

    private static Task RequestRecoveryFullStateSyncWhileTickingAsync(
        ShooterClientNetworkLaunchResult launched,
        ShooterClientNetworkLauncher launcher,
        Func<int> getFullSnapshotsApplied,
        TimeSpan timeout)
    {
        return RequestFullStateSyncWhileTickingAsync(
            launched,
            launcher,
            getFullSnapshotsApplied,
            timeout,
            "SoakRecovery",
            "recovery");
    }

    private static async Task RequestFullStateSyncWhileTickingAsync(
        ShooterClientNetworkLaunchResult launched,
        ShooterClientNetworkLauncher launcher,
        Func<int> getFullSnapshotsApplied,
        TimeSpan timeout,
        string reason,
        string requestKind)
    {
        var fullSnapshotsBeforeRequest = getFullSnapshotsApplied();
        var request = launched.Battle.RequestFullSnapshotBaselineAsync(reason, timeout);
        var deadline = DateTime.UtcNow + timeout;
        while (!request.IsCompleted)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out requesting {requestKind} Shooter full-state sync.");
            }

            launcher.Tick(1f / ShooterGameplay.DefaultTickRate);
            await Task.Delay(10).ConfigureAwait(false);
        }

        var result = await request.ConfigureAwait(false);
        if (!result.Success || !result.Accepted)
        {
            throw new InvalidOperationException($"{requestKind} Shooter full-state sync request was rejected. Success={result.Success}, Accepted={result.Accepted}, Message={result.Message}");
        }

        while (getFullSnapshotsApplied() <= fullSnapshotsBeforeRequest)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for {requestKind} Shooter full-state snapshot to apply.");
            }

            launcher.Tick(1f / ShooterGameplay.DefaultTickRate);
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private static async Task<ShooterSnapshotPushSmokeResult> WaitForPushWhileTickingAsync(
        Task<ShooterSnapshotPushSmokeResult> task,
        ShooterClientNetworkLauncher launcher,
        TimeSpan timeout,
        Func<string>? diagnostics = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow >= deadline)
            {
                var details = diagnostics == null ? string.Empty : " " + diagnostics();
                throw new TimeoutException($"Timed out waiting for Shooter snapshot push.{details}");
            }

            launcher.Tick(1f / ShooterGameplay.DefaultTickRate);
            await Task.Delay(10).ConfigureAwait(false);
        }

        return await task.ConfigureAwait(false);
    }

    private static string BuildPushWaitDiagnostics(
        int pushCount,
        in ShooterSnapshotPushSmokeResult lastPush,
        SmokeTcpGameFrameworkNetworkChannel channel,
        AbilityKit.Network.Abstractions.IConnection connection)
    {
        return $"pushCount={pushCount}, lastApply={lastPush.ApplyResult}, lastPayloadOpCode={lastPush.PayloadOpCode}, lastPushFrame={lastPush.PackedFrame}, channelReceived={channel.ReceivedPacketCount}, channelSent={channel.SentPacketCount}, inboundReceived={channel.ConditionInboundReceived}, inboundDropped={channel.ConditionInboundDropped}, connected={connection.IsConnected}";
    }

    private static ShooterSmokeLagCompensationProcessResult EvaluateLagCompensationSmoke(
        ShooterBattleRuntimePort runtime,
        uint playerId)
    {
        var snapshot = runtime.GetSnapshot();
        var players = snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>();
        ShooterPlayerSnapshot? shooter = null;
        ShooterPlayerSnapshot? target = null;
        for (var i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (!player.Alive)
            {
                continue;
            }

            if (player.PlayerId == playerId)
            {
                shooter = player;
                continue;
            }

            target ??= player;
        }

        if (!shooter.HasValue || !target.HasValue)
        {
            return new ShooterSmokeLagCompensationProcessResult(
                false,
                LagCompensationResultReason.HistoryUnavailable.ToString(),
                snapshot.Frame,
                -1,
                0,
                0f);
        }

        var shooterValue = shooter.Value;
        var targetValue = target.Value;
        var dx = targetValue.X - shooterValue.X;
        var dy = targetValue.Y - shooterValue.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.0001f)
        {
            dx = 1f;
            dy = 0f;
            distance = 1f;
        }

        var service = new ShooterLagCompensationService(new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 8,
            maxRewindFrames: 8));
        service.RecordFrame(in snapshot);
        var shot = new ShooterLagCompensationShot(
            (int)playerId,
            shooterValue.X,
            shooterValue.Y,
            dx / distance,
            dy / distance,
            MathF.Max(distance + 1f, 2f),
            snapshot.Frame,
            runtime.CurrentFrame);

        service.TryEvaluateShot(in shot, out var hit);
        return new ShooterSmokeLagCompensationProcessResult(
            hit.Accepted,
            hit.Reason.ToString(),
            hit.RequestedFrame,
            hit.EvaluatedFrame,
            hit.HitEntityId,
            hit.Distance);
    }

    private static ShooterPlayerCommand CreateGameplayLoopCommand(int frame)
    {
        const float firstEnemyX = -0.12186934f;
        const float firstEnemyY = 0.99254614f;
        var moveX = frame % 20 < 10 ? 0.35f : -0.2f;
        var moveY = frame % 30 < 15 ? 0.15f : -0.1f;
        return new ShooterPlayerCommand(1, moveX, moveY, firstEnemyX, firstEnemyY, fire: true);
    }

    private static async Task WaitForCompletionReleaseWhileTickingAsync(
        ShooterSmokeClientProcessOptions options,
        ShooterClientNetworkLaunchResult launched,
        ShooterClientNetworkLauncher launcher,
        SmokeTcpGameFrameworkNetworkChannel channel,
        AbilityKit.Network.Abstractions.IConnection connection,
        string sessionToken,
        string roomId,
        string battleId,
        ShooterSmokeSoakTelemetry soakTelemetry,
        Func<int> getPushCount,
        Func<int> getFullSnapshotsApplied,
        Func<int> getFullBaselinesApplied,
        Func<int> getResyncRequests,
        Func<int> getComparableFrame,
        Func<bool> getComparableHashMatched,
        TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(options.CompletionReleasePath))
        {
            return;
        }

        Console.WriteLine($"SHOOTER_MP_CLIENT_COMPLETION_READY clientId=\"{Escape(options.ClientId)}\"");
        soakTelemetry.StartCommandPolling(
            channel,
            getFullBaselinesApplied,
            getResyncRequests);
        var deadline = DateTime.UtcNow + timeout;
        var sampleInterval = TimeSpan.FromMilliseconds(Math.Max(100, options.MetricsSampleIntervalMs));
        var nextSampleAtUtc = DateTime.MinValue;
        Task<WireGetStateSyncDeliveryMetricsRes>? metricsTask = null;
        while (!File.Exists(options.CompletionReleasePath))
        {
            var now = DateTime.UtcNow;
            if (now >= deadline)
            {
                throw new TimeoutException(
                    $"Timed out waiting for completion release file: {options.CompletionReleasePath}");
            }

            launcher.Tick(1f / ShooterGameplay.DefaultTickRate);

            if (metricsTask == null && soakTelemetry.ConsumeRecoveryBaselineRequest())
            {
                await RequestRecoveryFullStateSyncWhileTickingAsync(
                    launched,
                    launcher,
                    getFullSnapshotsApplied,
                    TimeSpan.FromSeconds(Math.Min(10d, Math.Max(1d, timeout.TotalSeconds))))
                    .ConfigureAwait(false);
            }

            soakTelemetry.TryCompleteRecovery(
                getFullBaselinesApplied(),
                getResyncRequests(),
                getComparableFrame(),
                getComparableHashMatched());

            if (metricsTask != null && metricsTask.IsCompleted)
            {
                try
                {
                    var metrics = await metricsTask.ConfigureAwait(false);
                    soakTelemetry.WriteDeliverySample(
                        metrics,
                        channel,
                        getPushCount(),
                        getFullBaselinesApplied(),
                        getResyncRequests(),
                        getComparableFrame(),
                        getComparableHashMatched());
                }
                catch (Exception) when (metricsTask.IsFaulted || metricsTask.IsCanceled)
                {
                    // Metrics are observational; a slow or unavailable request must not block control ACKs.
                }

                metricsTask = null;
                nextSampleAtUtc = DateTime.UtcNow + sampleInterval;
            }

            if (soakTelemetry.Enabled
                && metricsTask == null
                && now >= nextSampleAtUtc)
            {
                metricsTask = GetStateSyncDeliveryMetricsAsync(
                    launched.GatewayConnection,
                    sessionToken,
                    roomId,
                    battleId,
                    TimeSpan.FromSeconds(Math.Min(10d, Math.Max(1d, timeout.TotalSeconds))));
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        launcher.Tick(1f / ShooterGameplay.DefaultTickRate);
    }

    private static async Task WaitForReconnectReleaseAsync(
        ShooterSmokeClientProcessOptions options,
        int submittedInputCount,
        TimeSpan timeout)
    {
        if (options.Mode != ShooterSmokeClientProcessMode.Join
            || string.IsNullOrWhiteSpace(options.ReconnectReleasePath))
        {
            return;
        }

        Console.WriteLine(
            $"SHOOTER_MP_CLIENT_RECONNECT_READY clientId=\"{Escape(options.ClientId)}\" inputs={submittedInputCount}");
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(options.ReconnectReleasePath))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Timed out waiting for reconnect release file: {options.ReconnectReleasePath}");
            }

            await Task.Delay(25).ConfigureAwait(false);
        }
    }

    private static async Task<List<ShooterClientGatewayInputSubmitResult>> SubmitInputsAsync(
        ShooterClientNetworkLaunchResult launched,
        int inputCount,
        TimeSpan timeout,
        ShooterSmokeReplayRecordScope? replay)
    {
        var results = new List<ShooterClientGatewayInputSubmitResult>(Math.Max(0, inputCount));
        for (var i = 0; i < inputCount; i++)
        {
            var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(
                i % 2 == 0 ? 1f : -0.35f,
                i % 3 == 0 ? 0.25f : 0f,
                1f,
                i % 2 == 0 ? 0f : 0.2f,
                i % 2 == 0,
                timeout: timeout);

            ValidateInput(submit);
            replay?.RecordInput(in submit);
            results.Add(submit);
            await Task.Delay(35);
        }

        return results;
    }

    private static async Task<ShooterSmokeReconnectProcessResult> ReconnectAsync(
        AbilityKit.Network.Abstractions.IConnection connection,
        ShooterClientNetworkLauncher launcher,
        ShooterBattleRuntimePort runtime,
        ShooterPresentationSessionContext session,
        ShooterStartGamePayload start,
        string sessionToken,
        ShooterRoomLaunchSpec launchSpec,
        ShooterSmokeClientProcessOptions options,
        Func<int> getPushCount,
        Action<TaskCompletionSource<ShooterSnapshotPushSmokeResult>> replacePushWait,
        TimeSpan timeout)
    {
        if (options.Mode != ShooterSmokeClientProcessMode.Join)
        {
            return default;
        }

        var firstPushCount = getPushCount();
        var retryAttemptCount = 0;
        var injectedFailureCount = 0;
        ShooterClientNetworkLaunchResult? reconnected = null;
        for (var cycle = 1; cycle <= options.ReconnectCount; cycle++)
        {
            var pushesBeforeCycle = getPushCount();
            Console.WriteLine(
                $"SHOOTER_MP_RECONNECT_DIAGNOSTIC stage=before-close cycle={cycle} runtimeFrame={runtime.CurrentFrame} pushes={pushesBeforeCycle}");
            connection.Close();
            await Task.Delay(Math.Max(0, options.ReconnectDelayMs)).ConfigureAwait(false);
            connection.Tick(0f);

            var reconnectPushWait = new TaskCompletionSource<ShooterSnapshotPushSmokeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            replacePushWait(reconnectPushWait);
            reconnected = await ShooterFaultRetryPolicy.ExecuteAsync(
                async attempt =>
                {
                    retryAttemptCount++;
                    if (cycle == 1 && attempt <= options.RecoverableFailureCount)
                    {
                        injectedFailureCount++;
                        throw new IOException($"Injected recoverable reconnect failure {attempt} of {options.RecoverableFailureCount}.");
                    }

                    return await launcher.JoinReadyStartAndSubscribeAsync(
                        options.Host,
                        options.Port,
                        runtime,
                        session,
                        start,
                        sessionToken,
                        options.RoomId,
                        launchSpec,
                        options.PlayerId,
                        timeout: timeout).ConfigureAwait(false);
                },
                options.RecoverableFailureCount,
                TimeSpan.FromMilliseconds(Math.Max(1, options.ReconnectDelayMs)),
                TimeSpan.FromMilliseconds(Math.Max(options.ReconnectDelayMs, options.RetryBackoffMaxMs)),
                isRecoverable: static exception => exception is IOException or TimeoutException).ConfigureAwait(false);

            ValidateLaunch(reconnected);
            Console.WriteLine(
                $"SHOOTER_MP_RECONNECT_DIAGNOSTIC stage=launch-returned cycle={cycle} runtimeFrame={runtime.CurrentFrame} targetFrame={reconnected.Flow.TargetFrame} remoteTargetFrame={reconnected.Flow.RemoteTimeAnchorProjection.TargetFrame} pushes={getPushCount()}");
            if (reconnected.Flow.EntryKind != ShooterRoomGatewayEntryKind.Reconnect)
            {
                throw new InvalidOperationException($"Shooter multiprocess reconnect expected reconnect entry kind. Cycle={cycle}, Actual={reconnected.Flow.EntryKind}");
            }

            var reconnectPush = await WaitForPushWhileTickingAsync(
                reconnectPushWait.Task,
                launcher,
                timeout,
                () => $"cycle={cycle}, pushesBefore={pushesBeforeCycle}, pushesNow={getPushCount()}, connected={connection.IsConnected}");
            Console.WriteLine(
                $"SHOOTER_MP_RECONNECT_DIAGNOSTIC stage=first-push-applied cycle={cycle} runtimeFrame={runtime.CurrentFrame} pushFrame={reconnectPush.PackedFrame} pushServerTick={reconnectPush.PackedServerTick} pushes={getPushCount()}");
            if (!IsAppliedSnapshotResult(reconnectPush.ApplyResult) || getPushCount() <= pushesBeforeCycle)
            {
                throw new InvalidOperationException($"Shooter multiprocess reconnect did not converge. Cycle={cycle}, Result={reconnectPush.ApplyResult}, PushesBefore={pushesBeforeCycle}, PushesAfter={getPushCount()}");
            }
        }

        var completedReconnect = reconnected ??
            throw new InvalidOperationException("Shooter multiprocess reconnect completed without a launch result.");
        return new ShooterSmokeReconnectProcessResult(
            completedReconnect,
            options.ReconnectCount,
            completedReconnect.Flow.EntryKind,
            completedReconnect.Flow.TargetFrame,
            firstPushCount,
            getPushCount(),
            retryAttemptCount,
            injectedFailureCount);
    }

    private static bool TryCaptureSnapshotPush(
        uint opCode,
        ArraySegment<byte> payload,
        ShooterSnapshotApplyResult applyResult,
        out ShooterSnapshotPushSmokeResult result)
    {
        result = default;
        if (opCode != RoomGatewayOpCodes.SnapshotPushed)
        {
            return false;
        }

        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
        if (wire.ServerTicks <= 0)
        {
            throw new InvalidOperationException("Shooter snapshot push returned invalid server ticks.");
        }

        if (wire.PayloadOpCode == ShooterOpCodes.Snapshot.PureState || wire.PayloadOpCode == ShooterOpCodes.Snapshot.PureStateDelta)
        {
            if (wire.Payload == null || wire.Payload.Length == 0)
            {
                throw new InvalidOperationException("Shooter pure-state snapshot push returned empty payload.");
            }

            var pureState = ShooterPureStateSyncCodec.Deserialize(wire.Payload);
            if (pureState.WorldId != wire.WorldId)
            {
                throw new InvalidOperationException($"Shooter pure-state snapshot world id mismatch. Wire={wire.WorldId}, PureState={pureState.WorldId}");
            }

            if (pureState.Frame != wire.Frame)
            {
                throw new InvalidOperationException($"Shooter pure-state snapshot frame mismatch. Wire={wire.Frame}, PureState={pureState.Frame}");
            }

            result = new ShooterSnapshotPushSmokeResult(
                applyResult,
                wire.WorldId,
                wire.Frame,
                wire.ServerTicks,
                wire.PayloadOpCode,
                pureState.WorldId,
                pureState.Frame,
                pureState.ServerTick,
                pureState.StateHash,
                pureState.Entities?.Length ?? 0,
                0,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                pureState.SnapshotKind,
                pureState.BaselineFrame,
                pureState.BaselineHash,
                pureState.VisibilityHints?.Length ?? 0,
                wire.IsFullSnapshot);
            return true;
        }

        if (wire.PayloadOpCode != ShooterOpCodes.Snapshot.PackedState)
        {
            result = new ShooterSnapshotPushSmokeResult(
                applyResult,
                wire.WorldId,
                wire.Frame,
                wire.ServerTicks,
                wire.PayloadOpCode,
                wire.WorldId,
                wire.Frame,
                wire.ServerTicks,
                0u,
                0,
                0,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0u,
                0,
                wire.IsFullSnapshot);
            return true;
        }

        if (wire.Payload == null || wire.Payload.Length == 0)
        {
            throw new InvalidOperationException("Shooter packed snapshot push returned empty payload.");
        }

        var packed = ShooterPackedSnapshotCodec.Deserialize(wire.Payload);
        var metadata = CaptureMatchMetadata(in packed);
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
            metadata.MatchState,
            metadata.MatchFinal,
            metadata.MatchVictory,
            metadata.MatchCompletedFrame,
            metadata.DefeatedEnemies,
            metadata.VictoryTargetDefeats,
            metadata.TimeLimitFrames,
            metadata.RemainingTimeFrames,
            0,
            0,
            0u,
            0,
            wire.IsFullSnapshot);
        return true;
    }

    internal static bool TryCapturePureStateComparableHash(
        in ShooterSnapshotPushSmokeResult push,
        int appliedFrame,
        uint appliedStateHash,
        out uint comparableClientHash)
    {
        comparableClientHash = 0u;
        if (push.ApplyResult != ShooterSnapshotApplyResult.AppliedActorSnapshot
            || push.PackedFrame <= 0
            || push.PackedStateHash == 0u
            || appliedFrame != push.PackedFrame
            || appliedStateHash == 0u)
        {
            return false;
        }

        comparableClientHash = appliedStateHash;
        return true;
    }

    private static bool IsAppliedSnapshotResult(ShooterSnapshotApplyResult result)
    {
        return result == ShooterSnapshotApplyResult.AppliedPackedSnapshot
            || result == ShooterSnapshotApplyResult.AppliedActorSnapshot;
    }

    private static ShooterPackedMatchMetadata CaptureMatchMetadata(in ShooterPackedSnapshotPayload packed)
    {
        var chunks = packed.ComponentChunks ?? Array.Empty<ShooterPackedComponentChunk>();
        for (var i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            if (chunk.ComponentKind != ShooterPackedComponentKinds.RuntimeMetadata)
            {
                continue;
            }

            var values = chunk.IntValues;
            if (values == null || values.Length < 5)
            {
                break;
            }

            var matchState = values[0];
            var timeLimitFrames = Math.Max(0, values[4]);
            return new ShooterPackedMatchMetadata(
                matchState,
                IsFinalMatchState(matchState),
                matchState == (int)ShooterBattleMatchState.Victory,
                values[1],
                values[2],
                values[3],
                timeLimitFrames,
                timeLimitFrames == 0 ? 0 : Math.Max(0, timeLimitFrames - packed.Frame));
        }

        return default;
    }

    private static bool IsFinalMatchState(int matchState)
    {
        return matchState == (int)ShooterBattleMatchState.Victory
            || matchState == (int)ShooterBattleMatchState.Defeat
            || matchState == (int)ShooterBattleMatchState.Ended;
    }

    private readonly record struct ShooterPackedMatchMetadata(
        int MatchState,
        bool MatchFinal,
        bool MatchVictory,
        int MatchCompletedFrame,
        int DefeatedEnemies,
        int VictoryTargetDefeats,
        int TimeLimitFrames,
        int RemainingTimeFrames);

    private static void ValidateLaunch(ShooterClientNetworkLaunchResult launched)
    {
        if (string.IsNullOrWhiteSpace(launched.Flow.RoomId))
        {
            throw new InvalidOperationException("Shooter multiprocess client launch returned empty room id.");
        }

        if (string.IsNullOrWhiteSpace(launched.Flow.BattleId))
        {
            throw new InvalidOperationException("Shooter multiprocess client launch returned empty battle id.");
        }

        if (launched.Flow.WorldId == 0)
        {
            throw new InvalidOperationException("Shooter multiprocess client launch returned empty world id.");
        }

        if (!launched.Flow.Started || !launched.Flow.Subscribed)
        {
            throw new InvalidOperationException($"Shooter multiprocess client launch was incomplete. Started={launched.Flow.Started}, Subscribed={launched.Flow.Subscribed}, Message={launched.Flow.Message}");
        }
    }

    private static void ValidateAppliedSnapshot(
        in ShooterSnapshotPushSmokeResult push,
        ShooterBattleRuntimePort runtime,
        ShooterPresentationFacade presentation)
    {
        if (push.ApplyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot && push.ApplyResult != ShooterSnapshotApplyResult.AppliedActorSnapshot)
        {
            throw new InvalidOperationException($"Shooter multiprocess snapshot was not applied. Result={push.ApplyResult}");
        }

        if (runtime.CurrentFrame <= 0 || presentation.ViewModel.Frame <= 0)
        {
            throw new InvalidOperationException("Shooter multiprocess client runtime/presentation did not advance after snapshot push.");
        }
    }

    private static bool ValidateLatestAuthoritativeSnapshot(
        in ShooterSnapshotPushSmokeResult firstAppliedPush,
        in ShooterClientReconciliationResult reconciliation,
        bool appliedSnapshotHashMatched)
    {
        if (firstAppliedPush.ApplyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot || firstAppliedPush.PackedStateHash == 0u)
        {
            return true;
        }

        if (reconciliation.ApplyResult == ShooterSnapshotApplyResult.AppliedPackedSnapshot)
        {
            return reconciliation.AuthoritativeHashMatched
                && reconciliation.AuthoritativeStateHash != 0u
                && reconciliation.ImportedStateHash == reconciliation.AuthoritativeStateHash;
        }

        return appliedSnapshotHashMatched;
    }

    private static void ValidateInput(in ShooterClientGatewayInputSubmitResult submit)
    {
        if (!submit.Remote.Success)
        {
            throw new InvalidOperationException($"Shooter multiprocess gateway input was rejected. RequestedFrame={submit.Local.RequestedFrame}, Status={submit.Remote.Status}, Message={submit.Remote.Message}");
        }

        if (submit.Remote.AcceptedFrame < submit.Local.RequestedFrame)
        {
            throw new InvalidOperationException($"Shooter multiprocess gateway accepted frame regressed. RequestedFrame={submit.Local.RequestedFrame}, AcceptedFrame={submit.Remote.AcceptedFrame}");
        }

        if (submit.Remote.CurrentFrame < 0)
        {
            throw new InvalidOperationException("Shooter multiprocess gateway input response returned invalid current frame.");
        }

        if (submit.Remote.ServerTicks <= 0)
        {
            throw new InvalidOperationException("Shooter multiprocess gateway input response returned invalid server ticks.");
        }
    }

    private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string GetExceptionMessage(Exception exception)
    {
        return exception is AggregateException aggregate && aggregate.InnerException is not null
            ? GetExceptionMessage(aggregate.InnerException)
            : exception.Message;
    }
}

internal enum ShooterSmokeClientProcessMode
{
    Create,
    Join
}

internal readonly record struct ShooterSmokeClientProcessOptions(
    ShooterSmokeClientProcessMode Mode,
    string Host,
    int Port,
    string RoomId,
    uint PlayerId,
    string ClientId,
    int RoomMaxPlayers,
    int BattleDurationFrames,
    int BattleVictoryTargetDefeats,
    bool ContinueAfterAllPlayersDefeated,
    int InputCount,
    int Seed,
    TimeSpan Timeout,
    bool WaitForMatchEnd,
    int ReconnectCount,
    int ReconnectDelayMs,
    int RecoverableFailureCount,
    int RetryBackoffMaxMs,
    SmokeNetworkConditionOptions NetworkCondition,
    string StateSyncPayloadMode,
    string InputStateReplayOutputPath,
    string RunId,
    string CorrelationId,
    string RunRootPath,
    string DiagnosticOutputPath,
    string ReconnectReleasePath,
    string CompletionReleasePath,
    string NetworkControlPath,
    string MetricsOutputPath,
    int MetricsSampleIntervalMs);

internal readonly record struct ShooterSmokeReconnectProcessResult(
    ShooterClientNetworkLaunchResult Launched,
    int ReconnectCount,
    ShooterRoomGatewayEntryKind EntryKind,
    int TargetFrame,
    int PushesBefore,
    int PushesAfter,
    int RetryAttemptCount,
    int InjectedFailureCount);

internal readonly record struct ShooterSmokeLagCompensationProcessResult(
    bool Accepted,
    string Reason,
    int RequestedFrame,
    int ResolvedFrame,
    int HitEntityId,
    float Distance);

internal readonly record struct ShooterSmokeClientProcessResult(
    ShooterSmokeClientProcessMode Mode,
    string StateSyncPayloadMode,
    string ClientId,
    string AccountId,
    string RoomId,
    string BattleId,
    ulong WorldId,
    uint PlayerId,
    ShooterRoomGatewayEntryKind EntryKind,
    int TargetFrame,
    int RuntimeFrame,
    int ViewFrame,
    uint StateHash,
    ShooterSnapshotApplyResult SnapshotApplyResult,
    int SnapshotFrame,
    int SnapshotPayloadOpCode,
    int SnapshotPayloadKind,
    int SnapshotBaselineFrame,
    uint SnapshotBaselineHash,
    uint SnapshotStateHash,
    int SnapshotEntityCount,
    int SnapshotVisibilityHintCount,
    int PureStateFullBaselinesApplied,
    int PureStateDeltasApplied,
    int PureStateResyncRequests,
    bool PureStateLastResyncNeeded,
    bool SnapshotHashMatched,
    int ReconcilePredictedFrame,
    uint ReconcilePredictedHash,
    int ReconcileAuthoritativeFrame,
    uint ReconcileAuthoritativeHash,
    uint ReconcileImportedHash,
    bool ReconcileAuthoritativeHashMatched,
    int ReconcileReplayTicks,
    int ReconcileFinalFrame,
    uint ReconcileFinalHash,
    int ReconcilePendingBefore,
    int ReconcilePendingAfterTrim,
    int ReconcilePendingAfterReplay,
    int InputCount,
    bool LastInputSuccess,
    int LastRequestedFrame,
    int LastAcceptedFrame,
    int LastCurrentFrame,
    string LastInputStatus,
    long LastServerTicks,
    bool ShouldResync,
    int PushCount,
    int LastPushFrame,
    int MatchState,
    bool MatchFinal,
    bool MatchVictory,
    int MatchCompletedFrame,
    int DefeatedEnemies,
    int VictoryTargetDefeats,
    int TimeLimitFrames,
    int RemainingTimeFrames,
    int ReconnectCount,
    ShooterRoomGatewayEntryKind ReconnectEntryKind,
    int ReconnectTargetFrame,
    int ReconnectPushesBefore,
    int ReconnectPushesAfter,
    int RetryAttemptCount,
    int InjectedFailureCount,
    int ConditionLatencyMs,
    int ConditionJitterMs,
    double ConditionPacketLossRate,
    int ConditionInboundReceived,
    int ConditionInboundDelayed,
    int ConditionInboundDropped,
    bool RemoteAnchorValid,
    int RemoteTargetFrame,
    int RemoteCatchUpFrames,
    double RemoteElapsedSeconds,
    long RemoteServerTicks,
    long SnapshotServerTicks,
    long LastPushServerTicks,
    long LastPushPackedServerTick,
    int LocalRuntimeFrame,
    int LocalViewFrame,
    bool LagCompAccepted,
    string LagCompReason,
    int LagCompRequestedFrame,
    int LagCompResolvedFrame,
    int LagCompHitEntityId,
    float LagCompDistance,
    string InputStateReplayPath,
    string MinimizedInputStateReplayPath,
    ShooterSmokeReplayValidationResult InputStateReplayValidation,
    string RunId,
    string CorrelationId,
    string DiagnosticArtifactPath,
    string DiagnosticArtifactSha256,
    string DiffPath,
    string DiffSha256,
    string DiffStatus);
