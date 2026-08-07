#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.View.Editor
{
    /// <summary>
    /// Runs one side of the formal Shooter room and authoritative state-sync flow in a
    /// batch-mode Unity Editor. The PowerShell coordinator starts owner and member clients.
    /// </summary>
    public static class ShooterMultiplayerHeadlessClientCommand
    {
        private static readonly TimeSpan StateWriteInterval = TimeSpan.FromMilliseconds(500);
        private const int RequiredMovementSubmissions = 18;
        private const int RequiredSettleSnapshots = 8;
        private const int MaxSamples = 128;

        private static ClientOptions? _options;
        private static Task<ClientState>? _runTask;
        private static ShooterClientNetworkLauncher? _launcher;
        private static ShooterClientNetworkLaunchResult? _battle;
        private static ShooterBattleWorldSession? _runtimeWorld;
        private static ShooterBattleRuntimePort? _runtime;
        private static ShooterRoomSessionController? _roomController;
        private static ShooterRoomGatewayRoomClient? _roomClient;
        private static ShooterMultiplayerProfileSO? _profile;
        private static DateTime _deadlineUtc;
        private static DateTime _nextStateWriteUtc;
        private static double _lastEditorTime;
        private static string _stage = "Starting";
        private static string _detail = string.Empty;
        private static string _roomId = string.Empty;
        private static string _battleId = string.Empty;
        private static ulong _worldId;
        private static uint _localPlayerId;
        private static int _playerCount;
        private static bool _soloLobbyVerified;
        private static int _roomPushCount;
        private static int _snapshotPushCount;
        private static int _fullSnapshotPushCount;
        private static int _deltaSnapshotPushCount;
        private static int _snapshotAppliedCount;
        private static int _packedSnapshotAppliedCount;
        private static int _actorSnapshotAppliedCount;
        private static int _staleSnapshotCount;
        private static int _snapshotImportFailureCount;
        private static int _snapshotResyncNeededCount;
        private static int _authoritativeHashMismatchCount;
        private static int _inputAttemptCount;
        private static int _inputSuccessCount;
        private static int _inputResyncCount;
        private static bool _movementActive;
        private static bool _hasMovementBaseline;
        private static float _movementBaselineX;
        private static float _lastMovementProgress;
        private static float _maxMovementProgress;
        private static float _maxBackwardMovement;
        private static float _maxReconciliationBackwardMovement;
        private static float _maxUnexplainedBackwardMovement;
        private static int _lastMovementAuthoritativeFrame;
        private static int _movementSampleCount;
        private static readonly List<AuthoritativeSample> Samples = new List<AuthoritativeSample>();
        private static readonly List<HashMismatchSample> HashMismatches = new List<HashMismatchSample>();

        public static void Run()
        {
            if (_runTask != null)
            {
                throw new InvalidOperationException("Shooter headless command is already running.");
            }

            _options = ClientOptions.Parse(Environment.GetCommandLineArgs());
            _deadlineUtc = DateTime.UtcNow.AddSeconds(_options.TimeoutSeconds);
            _nextStateWriteUtc = DateTime.MinValue;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            _runTask = RunAsync(_options);
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Debug.Log($"[ShooterHeadless] Started role={_options.Role} account={_options.Account} gateway={_options.Host}:{_options.Port}");
        }

        private static void Update()
        {
            try
            {
                var editorTime = EditorApplication.timeSinceStartup;
                var deltaTime = (float)Math.Max(0d, Math.Min(0.1d, editorTime - _lastEditorTime));
                _lastEditorTime = editorTime;
                _launcher?.Tick(deltaTime);
                _battle?.Session.Tick(deltaTime);
                CaptureMovementTrajectory();

                if (DateTime.UtcNow >= _nextStateWriteUtc)
                {
                    WriteState(CaptureState());
                    _nextStateWriteUtc = DateTime.UtcNow + StateWriteInterval;
                }

                if (_runTask == null || !_runTask.IsCompleted)
                {
                    if (DateTime.UtcNow >= _deadlineUtc)
                    {
                        throw new TimeoutException($"Shooter headless client exceeded {_options?.TimeoutSeconds ?? 0} seconds at stage {_stage}.");
                    }
                    return;
                }

                var state = _runTask.GetAwaiter().GetResult();
                Complete(true, "Shooter multiplayer state-sync acceptance completed.", state);
            }
            catch (Exception exception)
            {
                Complete(false, exception.ToString(), CaptureState());
            }
        }

        private static async Task<ClientState> RunAsync(ClientOptions options)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            var cancellationToken = timeout.Token;

            SetStage("Login", "Authenticating with the room gateway");
            var login = await DemoRoomGatewayAccountClient.LoginTcpAsync(
                options.Host,
                options.Port,
                options.Account,
                TimeSpan.FromSeconds(30));
            if (!login.Success)
            {
                throw new InvalidOperationException("Shooter login failed: " + login.Message);
            }

            _profile = CreateProfile(options.IsOwner ? 1 : 2, 2);
            var sessionOptions = _profile.BuildSessionOptions();
            var launchSpec = _profile.BuildRoomLaunchSpec(
                sessionOptions,
                options.Region,
                options.ServerId,
                "Shooter Headless " + options.RunId);
            var roomSpec = new ShooterRoomSessionLaunchSpec(
                login.SessionToken,
                in launchSpec,
                (uint)sessionOptions.ControlledPlayerId,
                TimeSpan.FromSeconds(Math.Max(120, options.TimeoutSeconds - 20)));

            _launcher = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.TcpForUnityMainThread());
            _launcher.Open(new ShooterClientNetworkEndpoint(options.Host, options.Port));
            _launcher.GatewayConnection.ServerPushReceived += HandleServerPush;
            _launcher.GatewayConnection.SnapshotPushDispatched += HandleSnapshotPush;
            _roomClient = new ShooterRoomGatewayRoomClient(_launcher.GatewayConnection);
            var store = new ShooterRoomSessionStore(_roomClient);
            var roomSession = new ShooterGatewayRoomSession(_roomClient, store);
            _roomController = new ShooterRoomSessionController(roomSession, store);

            if (options.IsOwner)
            {
                SetStage("CreatingRoom", "Creating the formal Shooter room");
                await _roomController.StartCreateRoomAsync(roomSpec, cancellationToken);
                CaptureRoomState();
                if (string.IsNullOrWhiteSpace(_roomId))
                {
                    throw new InvalidOperationException("Shooter owner created no room id.");
                }
                SetStage("SoloLobby", "Verifying that the owner remains in lobby before the member joins");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                CaptureRoomState();
                var solo = _roomController.CurrentSnapshot;
                _soloLobbyVerified = solo != null
                    && solo.Phase == ShooterRoomSessionPhase.Lobby
                    && solo.Members.Count == 1
                    && string.IsNullOrWhiteSpace(solo.BattleId);
                if (!_soloLobbyVerified)
                {
                    throw new InvalidOperationException("Shooter owner did not remain in a one-player lobby before member join.");
                }
                File.WriteAllText(options.RoomPath, JsonUtility.ToJson(new RoomCoordinate { roomId = _roomId }, true));
            }
            else
            {
                SetStage("WaitingForRoom", "Waiting for the owner room coordinate");
                _roomId = await WaitForRoomIdAsync(options.RoomPath, cancellationToken);
                SetStage("JoiningRoom", "Joining the owner Shooter room");
                await _roomController.StartJoinRoomAsync(roomSpec, _roomId, cancellationToken);
                CaptureRoomState();
            }

            SetStage("Ready", "Publishing local lobby readiness");
            await _roomController.SetReadyAsync(true, cancellationToken);
            CaptureRoomState();

            if (options.IsOwner)
            {
                SetStage("WaitingForMember", "Waiting for two ready authoritative members");
                await WaitUntilAsync(
                    () =>
                    {
                        CaptureRoomState();
                        var snapshot = _roomController?.CurrentSnapshot;
                        return snapshot != null
                            && snapshot.Phase == ShooterRoomSessionPhase.Lobby
                            && snapshot.Members.Count >= 2
                            && snapshot.CanStart;
                    },
                    "two ready Shooter room members",
                    cancellationToken);
                SetStage("BeginningLoading", "Owner starts the formal loading generation");
                await _roomController.BeginLoadingAsync(cancellationToken);
            }
            else
            {
                SetStage("WaitingForLoading", "Waiting for the owner loading push");
                await WaitUntilAsync(
                    () => _roomController?.CurrentState == ShooterRoomSessionState.LoadingAssets,
                    "owner loading push",
                    cancellationToken);
            }

            SetStage("LoadingAssets", "Running the Shooter loading pipeline and reporting completion");
            await _roomController.PrepareAssetsAsync(cancellationToken);
            if (_roomController.CurrentState != ShooterRoomSessionState.InBattle)
            {
                SetStage("WaitingForBattle", "Waiting for the authoritative battle start");
                await _roomController.WaitForBattleStartAsync(cancellationToken);
            }
            CaptureRoomState();
            if (_roomController.CurrentSnapshot == null
                || _roomController.CurrentSnapshot.Phase != ShooterRoomSessionPhase.InBattle
                || _roomController.CurrentSnapshot.WorldId == 0)
            {
                throw new InvalidOperationException("Shooter staged room flow did not reach an authoritative battle.");
            }

            _localPlayerId = _roomController.LocalPlayerId;
            var roomSnapshot = _roomController.CurrentSnapshot;
            _battleId = roomSnapshot.BattleId;
            _worldId = roomSnapshot.WorldId;
            _roomController.Dispose();
            _roomController = null;
            _roomClient.Dispose();
            _roomClient = null;

            SetStage("ConnectingBattle", "Subscribing to authoritative Shooter state synchronization");
            _runtimeWorld = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld(
                $"shooter-headless-{options.Role}-{options.RunId}",
                sessionOptions);
            _runtime = _runtimeWorld.Runtime;
            var presentation = new ShooterPresentationFacade();
            var launch = await _launcher.JoinReadyStartAndSubscribeAsync(
                new ShooterClientNetworkEndpoint(options.Host, options.Port),
                _runtime,
                presentation,
                CreateStartPayload(sessionOptions),
                login.SessionToken,
                _roomId,
                launchSpec,
                _localPlayerId,
                sessionOptions.TickRate,
                TimeSpan.FromSeconds(Math.Max(90, options.TimeoutSeconds - 30)),
                cancellationToken);
            _battle = launch;
            _battleId = launch.Flow.BattleId;
            _worldId = launch.Flow.WorldId;
            if (!launch.Flow.Started || !launch.Flow.Subscribed || _worldId == 0)
            {
                throw new InvalidOperationException(
                    $"Shooter battle launch incomplete. started={launch.Flow.Started}, subscribed={launch.Flow.Subscribed}, message={launch.Flow.Message}");
            }

            SetStage("BattleReady", "Waiting for both clients before the movement probe");
            await WaitUntilAsync(
                () => _snapshotAppliedCount >= 2,
                "initial authoritative Shooter snapshots",
                cancellationToken);
            await WaitForFileAsync(options.MovementSignalPath, "movement signal", cancellationToken);

            var direction = options.IsOwner ? 1f : -1f;
            BeginMovementProbe(direction);
            SetStage("Movement", "Submitting controlled movement to the authoritative world");
            for (var i = 0; i < RequiredMovementSubmissions; i++)
            {
                _inputAttemptCount++;
                var submit = await launch.Battle.SubmitLocalInputToGatewayAsync(
                    direction,
                    0f,
                    direction,
                    0f,
                    fire: i == RequiredMovementSubmissions / 2,
                    timeout: TimeSpan.FromSeconds(10),
                    cancellationToken: cancellationToken);
                if (!submit.Remote.Success)
                {
                    throw new InvalidOperationException(
                        $"Shooter input rejected. status={submit.Remote.Status}, message={submit.Remote.Message}, requested={submit.Local.RequestedFrame}");
                }
                _inputSuccessCount++;
                if (submit.Remote.ShouldResync) _inputResyncCount++;
                await Task.Delay(100, cancellationToken);
            }

            _inputAttemptCount++;
            var stop = await launch.Battle.SubmitLocalInputToGatewayAsync(
                0f, 0f, direction, 0f, false,
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken);
            if (!stop.Remote.Success)
            {
                throw new InvalidOperationException("Shooter stop input was rejected: " + stop.Remote.Message);
            }
            _inputSuccessCount++;
            _movementActive = false;

            var settleStartSnapshots = _snapshotAppliedCount;
            SetStage("Settling", "Waiting for authoritative correction and remote convergence");
            await WaitUntilAsync(
                () => _snapshotAppliedCount >= settleStartSnapshots + RequiredSettleSnapshots,
                "post-input authoritative snapshots",
                cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            SetStage("AwaitFinalize", "Waiting for the coordinator to select a common authoritative frame");
            WriteState(CaptureState());
            var finalize = await WaitForFinalizeAsync(options.FinalizePath, cancellationToken);
            await WaitUntilAsync(
                () => FindSample(finalize.frame, finalize.authoritativeHash) != null,
                $"authoritative sample frame {finalize.frame}",
                cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            SetStage("Completed", "Shooter authoritative state synchronization converged");
            var finalState = CaptureState();
            ValidateLocalResult(finalState, finalize);
            return finalState;
        }

        private static void ValidateLocalResult(ClientState state, FinalizeCoordinate finalize)
        {
            if (state.playerCount < 2) throw new InvalidOperationException("Shooter client did not observe two room members.");
            if (state.roomPushCount < 1) throw new InvalidOperationException("Shooter client received no room-state push.");
            if (state.snapshotAppliedCount < 5 || state.packedSnapshotAppliedCount < 1)
                throw new InvalidOperationException("Shooter client did not apply enough authoritative snapshots.");
            if (state.fullSnapshotPushCount < 5 || state.deltaSnapshotPushCount != 0)
                throw new InvalidOperationException(
                    $"Shooter predict-rollback flow did not use full authoritative snapshots exclusively. full={state.fullSnapshotPushCount}, delta={state.deltaSnapshotPushCount}");
            if (state.authoritativeHashMismatchCount != 0)
                throw new InvalidOperationException(
                    $"Shooter authoritative snapshot imports mismatched {state.authoritativeHashMismatchCount} times: {FormatHashMismatches(state.hashMismatches)}");
            if (state.snapshotImportFailureCount != 0 || state.snapshotResyncNeededCount != 0 || state.inputResyncCount != 0)
                throw new InvalidOperationException("Shooter state synchronization requested resync or failed snapshot import.");
            if (state.needsFullSnapshotResync)
                throw new InvalidOperationException("Shooter client ended while still requiring full snapshot resync.");
            if (state.inputSuccessCount < RequiredMovementSubmissions)
                throw new InvalidOperationException("Shooter movement inputs were not accepted.");
            if (state.maxMovementProgress < 0.2f)
                throw new InvalidOperationException($"Shooter controlled player did not move far enough. progress={state.maxMovementProgress:F3}");
            if (state.maxUnexplainedBackwardMovement > 0.5f)
                throw new InvalidOperationException(
                    $"Shooter controlled player tugged backward without a new authoritative correction. " +
                    $"maxUnexplained={state.maxUnexplainedBackwardMovement:F3}, " +
                    $"maxReconciliation={state.maxReconciliationBackwardMovement:F3}, " +
                    $"maxRaw={state.maxBackwardMovement:F3}");
            if (FindSample(finalize.frame, finalize.authoritativeHash) == null)
                throw new InvalidOperationException("Shooter client lacks the coordinator-selected authoritative sample.");
        }

        private static void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode == RoomGatewayOpCodes.RoomStateChanged) _roomPushCount++;
        }

        private static void HandleSnapshotPush(uint opCode, ArraySegment<byte> payload, ShooterSnapshotApplyResult result)
        {
            if (opCode != RoomGatewayOpCodes.SnapshotPushed && opCode != RoomGatewayOpCodes.DeltaSnapshotPushed) return;
            _snapshotPushCount++;
            if (opCode == RoomGatewayOpCodes.SnapshotPushed) _fullSnapshotPushCount++;
            else _deltaSnapshotPushCount++;
            switch (result)
            {
                case ShooterSnapshotApplyResult.AppliedPackedSnapshot:
                    _snapshotAppliedCount++;
                    _packedSnapshotAppliedCount++;
                    CountAuthoritativeHashMismatch();
                    CaptureAuthoritativeSample();
                    break;
                case ShooterSnapshotApplyResult.AppliedActorSnapshot:
                    _snapshotAppliedCount++;
                    _actorSnapshotAppliedCount++;
                    break;
                case ShooterSnapshotApplyResult.IgnoredStaleSnapshot:
                    _staleSnapshotCount++;
                    break;
                case ShooterSnapshotApplyResult.ImportFailed:
                case ShooterSnapshotApplyResult.UnsupportedVersion:
                    _snapshotImportFailureCount++;
                    break;
                case ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded:
                    _snapshotResyncNeededCount++;
                    break;
            }
        }

        private static void CountAuthoritativeHashMismatch()
        {
            var session = _launcher?.GatewayConnection.CurrentSession;
            var evidence = session?.FrameSync.LastImportedSnapshotEvidence
                ?? ShooterClientImportedSnapshotEvidence.None;
            if (evidence.AuthoritativeStateHash != 0u &&
                evidence.ImportedStateHash != evidence.AuthoritativeStateHash)
            {
                _authoritativeHashMismatchCount++;
                HashMismatches.Add(new HashMismatchSample
                {
                    authoritativeFrame = evidence.Frame,
                    clientFrame = session?.CurrentFrame ?? 0,
                    authoritativeHash = FormatHash(evidence.AuthoritativeStateHash),
                    importedHash = FormatHash(evidence.ImportedStateHash),
                    freshImportedHash = FormatHash(session?.FrameSync.LastFreshImportedStateHash ?? 0u),
                    packedPayloadPath = WriteMismatchPackedPayload(
                        evidence.Frame,
                        session?.FrameSync.LastMismatchPackedPayload,
                        "server"),
                    freshExportedPayloadPath = WriteMismatchPackedPayload(
                        evidence.Frame,
                        session?.FrameSync.LastFreshExportedPackedPayload,
                        "unity-fresh")
                });
            }
        }

        private static string WriteMismatchPackedPayload(int frame, byte[]? payload, string source)
        {
            if (payload == null || payload.Length == 0 || _options == null)
            {
                return string.Empty;
            }

            try
            {
                var directory = Path.GetDirectoryName(_options.ResultPath) ?? ".";
                var fileName = $"{_options.Role}-mismatch-frame-{frame}-{source}.packed.bin";
                var path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, payload);
                return fileName;
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private static string FormatHashMismatches(IReadOnlyList<HashMismatchSample> mismatches)
        {
            if (mismatches == null || mismatches.Count == 0) return "none";
            var parts = new string[mismatches.Count];
            for (var i = 0; i < mismatches.Count; i++)
            {
                var item = mismatches[i];
                parts[i] = $"authorityFrame={item.authoritativeFrame}/clientFrame={item.clientFrame}/{item.authoritativeHash}!={item.importedHash}/fresh={item.freshImportedHash}";
            }
            return string.Join(", ", parts);
        }

        private static void CaptureAuthoritativeSample()
        {
            var session = _launcher?.GatewayConnection.CurrentSession;
            var runtime = _runtime;
            if (session == null || runtime == null) return;
            var evidence = session.FrameSync.LastImportedSnapshotEvidence;
            if (evidence.Frame <= 0 || evidence.AuthoritativeStateHash == 0 ||
                evidence.ImportedStateHash != evidence.AuthoritativeStateHash) return;

            var sample = new AuthoritativeSample
            {
                frame = evidence.Frame,
                authoritativeHash = FormatHash(evidence.AuthoritativeStateHash),
                importedHash = FormatHash(evidence.ImportedStateHash)
            };
            CapturePlayer(runtime, 1, out sample.p1Present, out sample.p1x, out sample.p1y);
            CapturePlayer(runtime, 2, out sample.p2Present, out sample.p2x, out sample.p2y);

            for (var i = Samples.Count - 1; i >= 0; i--)
            {
                if (Samples[i].frame != sample.frame) continue;
                Samples[i] = sample;
                return;
            }
            Samples.Add(sample);
            if (Samples.Count > MaxSamples) Samples.RemoveAt(0);
        }

        private static void BeginMovementProbe(float direction)
        {
            if (_runtime == null || _localPlayerId == 0 ||
                !_runtime.TryGetPlayer((int)_localPlayerId, out var player))
            {
                throw new InvalidOperationException("Shooter controlled player is unavailable before movement probe.");
            }
            _movementBaselineX = player.X;
            _lastMovementProgress = 0f;
            _maxMovementProgress = 0f;
            _maxBackwardMovement = 0f;
            _maxReconciliationBackwardMovement = 0f;
            _maxUnexplainedBackwardMovement = 0f;
            _lastMovementAuthoritativeFrame = _launcher?.GatewayConnection.CurrentSession
                ?.FrameSync.LastImportedSnapshotEvidence.Frame ?? 0;
            _movementSampleCount = 0;
            _hasMovementBaseline = true;
            _movementActive = true;
        }

        private static void CaptureMovementTrajectory()
        {
            var options = _options;
            var runtime = _runtime;
            if (!_movementActive || !_hasMovementBaseline || options == null || runtime == null || _localPlayerId == 0) return;
            if (!runtime.TryGetPlayer((int)_localPlayerId, out var player)) return;
            var direction = options.IsOwner ? 1f : -1f;
            var progress = (player.X - _movementBaselineX) * direction;
            var backward = _lastMovementProgress - progress;
            var authoritativeFrame = _launcher?.GatewayConnection.CurrentSession
                ?.FrameSync.LastImportedSnapshotEvidence.Frame ?? _lastMovementAuthoritativeFrame;
            if (backward > _maxBackwardMovement) _maxBackwardMovement = backward;
            if (backward > 0f)
            {
                if (authoritativeFrame != _lastMovementAuthoritativeFrame)
                {
                    if (backward > _maxReconciliationBackwardMovement) _maxReconciliationBackwardMovement = backward;
                }
                else if (backward > _maxUnexplainedBackwardMovement)
                {
                    _maxUnexplainedBackwardMovement = backward;
                }
            }
            if (progress > _maxMovementProgress) _maxMovementProgress = progress;
            _lastMovementProgress = progress;
            _lastMovementAuthoritativeFrame = authoritativeFrame;
            _movementSampleCount++;
        }

        private static void CaptureRoomState()
        {
            var controller = _roomController;
            if (controller == null) return;
            if (!string.IsNullOrWhiteSpace(controller.CurrentRoomId)) _roomId = controller.CurrentRoomId;
            if (controller.LocalPlayerId != 0) _localPlayerId = controller.LocalPlayerId;
            var snapshot = controller.CurrentSnapshot;
            if (snapshot == null) return;
            _playerCount = Math.Max(_playerCount, snapshot.Members.Count);
            if (!string.IsNullOrWhiteSpace(snapshot.BattleId)) _battleId = snapshot.BattleId;
            if (snapshot.WorldId != 0) _worldId = snapshot.WorldId;
        }

        private static ClientState CaptureState()
        {
            CaptureRoomState();
            var state = new ClientState
            {
                role = _options?.Role ?? string.Empty,
                account = _options?.Account ?? string.Empty,
                stage = _stage,
                detail = _detail,
                roomId = _roomId,
                battleId = _battleId,
                worldId = _worldId.ToString(CultureInfo.InvariantCulture),
                localPlayerId = _localPlayerId,
                playerCount = _playerCount,
                soloLobbyVerified = _soloLobbyVerified,
                roomPushCount = _roomPushCount,
                snapshotPushCount = _snapshotPushCount,
                fullSnapshotPushCount = _fullSnapshotPushCount,
                deltaSnapshotPushCount = _deltaSnapshotPushCount,
                snapshotAppliedCount = _snapshotAppliedCount,
                packedSnapshotAppliedCount = _packedSnapshotAppliedCount,
                actorSnapshotAppliedCount = _actorSnapshotAppliedCount,
                staleSnapshotCount = _staleSnapshotCount,
                snapshotImportFailureCount = _snapshotImportFailureCount,
                snapshotResyncNeededCount = _snapshotResyncNeededCount,
                authoritativeHashMismatchCount = _authoritativeHashMismatchCount,
                hashMismatches = new List<HashMismatchSample>(HashMismatches),
                inputAttemptCount = _inputAttemptCount,
                inputSuccessCount = _inputSuccessCount,
                inputResyncCount = _inputResyncCount,
                movementSampleCount = _movementSampleCount,
                maxMovementProgress = _maxMovementProgress,
                maxBackwardMovement = _maxBackwardMovement,
                maxReconciliationBackwardMovement = _maxReconciliationBackwardMovement,
                maxUnexplainedBackwardMovement = _maxUnexplainedBackwardMovement,
                frame = _battle?.Session.CurrentFrame ?? _runtime?.CurrentFrame ?? 0,
                stateHash = _runtime == null ? "0x00000000" : FormatHash(_runtime.ComputeStateHash()),
                samples = new List<AuthoritativeSample>(Samples)
            };

            var session = _battle?.Session;
            if (session != null)
            {
                var diagnostics = session.FrameworkSnapshotPipelineDiagnostics;
                var reconciliation = session.LastReconciliationResult;
                state.pipelinePacketCount = diagnostics.PacketCount;
                state.pipelineDispatchedCount = diagnostics.DispatchedSnapshotCount;
                state.pipelineLastFrame = diagnostics.LastFrame;
                state.recoveryState = session.RecoveryState.ToString();
                state.needsFullSnapshotResync = session.NeedsFullSnapshotResync;
                state.lastResyncReason = session.LastResyncReason.ToString();
                state.lastAuthoritativeFrame = reconciliation.AuthoritativeFrame;
                state.lastAuthoritativeHash = FormatHash(reconciliation.AuthoritativeStateHash);
                state.lastImportedHash = FormatHash(reconciliation.ImportedStateHash);
                state.authoritativeHashMatched = reconciliation.AuthoritativeStateHash == 0 || reconciliation.AuthoritativeHashMatched;
                state.replayTicks = reconciliation.ReplayTicks;
                state.pendingInputFrames = session.FrameSync.PendingInputFrameCount;
            }

            if (_runtime != null)
            {
                CapturePlayer(_runtime, 1, out state.p1Present, out state.p1x, out state.p1y);
                CapturePlayer(_runtime, 2, out state.p2Present, out state.p2x, out state.p2y);
            }
            return state;
        }

        private static void CapturePlayer(ShooterBattleRuntimePort runtime, int playerId, out bool present, out float x, out float y)
        {
            present = runtime.TryGetPlayer(playerId, out var player);
            x = present ? player.X : 0f;
            y = present ? player.Y : 0f;
        }

        private static async Task WaitUntilAsync(Func<bool> predicate, string description, CancellationToken cancellationToken)
        {
            while (!predicate())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= _deadlineUtc) throw new TimeoutException("Timed out waiting for " + description + ".");
                await Task.Delay(50, cancellationToken);
            }
        }

        private static async Task<string> WaitForRoomIdAsync(string path, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    try
                    {
                        var coordinate = JsonUtility.FromJson<RoomCoordinate>(File.ReadAllText(path));
                        if (coordinate != null && !string.IsNullOrWhiteSpace(coordinate.roomId)) return coordinate.roomId;
                    }
                    catch (IOException)
                    {
                    }
                }
                await Task.Delay(100, cancellationToken);
            }
        }

        private static async Task WaitForFileAsync(string path, string description, CancellationToken cancellationToken)
        {
            await WaitUntilAsync(() => File.Exists(path), description, cancellationToken);
        }

        private static async Task<FinalizeCoordinate> WaitForFinalizeAsync(string path, CancellationToken cancellationToken)
        {
            while (true)
            {
                await WaitForFileAsync(path, "finalize coordinate", cancellationToken);
                try
                {
                    var coordinate = JsonUtility.FromJson<FinalizeCoordinate>(File.ReadAllText(path));
                    if (coordinate != null && coordinate.frame > 0 && !string.IsNullOrWhiteSpace(coordinate.authoritativeHash)) return coordinate;
                }
                catch (IOException)
                {
                }
                await Task.Delay(100, cancellationToken);
            }
        }

        private static AuthoritativeSample? FindSample(int frame, string authoritativeHash)
        {
            for (var i = Samples.Count - 1; i >= 0; i--)
            {
                var sample = Samples[i];
                if (sample.frame == frame && string.Equals(sample.authoritativeHash, authoritativeHash, StringComparison.OrdinalIgnoreCase)) return sample;
            }
            return null;
        }

        private static ShooterMultiplayerProfileSO CreateProfile(int playerId, int playerCount)
        {
            var profile = ScriptableObject.CreateInstance<ShooterMultiplayerProfileSO>();
            SetProfileField(profile, "syncTemplateId", ShooterSyncTemplateIds.PredictRollbackAuthority);
            SetProfileField(profile, "controlledPlayerId", playerId);
            SetProfileField(profile, "playerCount", playerCount);
            SetProfileField(profile, "maxPlayers", playerCount);
            SetProfileField(profile, "autoReady", false);
            SetProfileField(profile, "autoStart", false);
            return profile;
        }

        private static void SetProfileField(ShooterMultiplayerProfileSO profile, string name, object value)
        {
            var field = typeof(ShooterMultiplayerProfileSO).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(ShooterMultiplayerProfileSO).FullName, name);
            field.SetValue(profile, value);
        }

        private static ShooterStartGamePayload CreateStartPayload(ShooterPlayModeSessionOptions options)
        {
            var players = new ShooterStartPlayer[options.PlayerCount];
            for (var i = 0; i < players.Length; i++) players[i] = new ShooterStartPlayer(i + 1, "P" + (i + 1), i * 4f, 0f);
            return new ShooterStartGamePayload("shooter-headless-" + options.RandomSeed, options.TickRate, options.RandomSeed, players);
        }

        private static void SetStage(string stage, string detail)
        {
            _stage = stage;
            _detail = detail;
            Debug.Log($"[ShooterHeadless] {_options?.Role}: {stage} - {detail}");
            WriteState(CaptureState());
        }

        private static void WriteState(ClientState state)
        {
            var path = _options?.StatePath;
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, JsonUtility.ToJson(state, true));
            }
            catch (IOException)
            {
            }
        }

        private static void Complete(bool success, string message, ClientState state)
        {
            EditorApplication.update -= Update;
            state.stage = success ? "Completed" : "Failed";
            state.detail = message;
            WriteState(state);
            var result = new ClientResult { success = success, message = message, state = state };
            var resultPath = _options?.ResultPath;
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
                File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            }
            Debug.Log($"[ShooterHeadless] Done success={success} message={message}");
            try { _roomController?.Dispose(); } catch { }
            try { _roomClient?.Dispose(); } catch { }
            try { _launcher?.Dispose(); } catch { }
            try { _runtimeWorld?.Dispose(); } catch { }
            if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile);
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static string FormatHash(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);

        [Serializable]
        private sealed class ClientOptions
        {
            public string Role = string.Empty;
            public string Account = string.Empty;
            public string RunId = string.Empty;
            public string RoomPath = string.Empty;
            public string MovementSignalPath = string.Empty;
            public string FinalizePath = string.Empty;
            public string StatePath = string.Empty;
            public string ResultPath = string.Empty;
            public string Host = "127.0.0.1";
            public int Port = 4000;
            public string Region = "dev";
            public string ServerId = "local";
            public int TimeoutSeconds = 240;
            public bool IsOwner => string.Equals(Role, "owner", StringComparison.OrdinalIgnoreCase);

            public static ClientOptions Parse(string[] args)
            {
                return new ClientOptions
                {
                    Role = Require(args, "-shooterHeadlessRole"),
                    Account = Require(args, "-shooterHeadlessAccount"),
                    RunId = Require(args, "-shooterHeadlessRunId"),
                    RoomPath = FullPath(Require(args, "-shooterHeadlessRoomPath")),
                    MovementSignalPath = FullPath(Require(args, "-shooterHeadlessMovementSignal")),
                    FinalizePath = FullPath(Require(args, "-shooterHeadlessFinalize")),
                    StatePath = FullPath(Require(args, "-shooterHeadlessState")),
                    ResultPath = FullPath(Require(args, "-shooterHeadlessResult")),
                    Host = Value(args, "-gatewayHost") ?? "127.0.0.1",
                    Port = IntValue(args, "-gatewayPort", 4000),
                    Region = Value(args, "-gatewayRegion") ?? "dev",
                    ServerId = Value(args, "-gatewayServerId") ?? "local",
                    TimeoutSeconds = IntValue(args, "-shooterHeadlessTimeoutSeconds", 240)
                };
            }

            private static string Require(string[] args, string name) => Value(args, name)
                ?? throw new ArgumentException("Required argument missing: " + name);
            private static string? Value(string[] args, string name)
            {
                for (var i = 0; i + 1 < args.Length; i++)
                    if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
                return null;
            }
            private static int IntValue(string[] args, string name, int fallback) =>
                int.TryParse(Value(args, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
            private static string FullPath(string value) => Path.GetFullPath(value);
        }

        [Serializable]
        private sealed class RoomCoordinate { public string roomId = string.Empty; }

        [Serializable]
        private sealed class FinalizeCoordinate
        {
            public int frame;
            public string authoritativeHash = string.Empty;
        }

        [Serializable]
        private sealed class ClientResult
        {
            public bool success;
            public string message = string.Empty;
            public ClientState state = new ClientState();
        }

        [Serializable]
        private sealed class ClientState
        {
            public string role = string.Empty;
            public string account = string.Empty;
            public string stage = string.Empty;
            public string detail = string.Empty;
            public string roomId = string.Empty;
            public string battleId = string.Empty;
            public string worldId = string.Empty;
            public uint localPlayerId;
            public int playerCount;
            public bool soloLobbyVerified;
            public int roomPushCount;
            public int snapshotPushCount;
            public int fullSnapshotPushCount;
            public int deltaSnapshotPushCount;
            public int snapshotAppliedCount;
            public int packedSnapshotAppliedCount;
            public int actorSnapshotAppliedCount;
            public int staleSnapshotCount;
            public int snapshotImportFailureCount;
            public int snapshotResyncNeededCount;
            public int authoritativeHashMismatchCount;
            public List<HashMismatchSample> hashMismatches = new List<HashMismatchSample>();
            public int inputAttemptCount;
            public int inputSuccessCount;
            public int inputResyncCount;
            public int movementSampleCount;
            public float maxMovementProgress;
            public float maxBackwardMovement;
            public float maxReconciliationBackwardMovement;
            public float maxUnexplainedBackwardMovement;
            public int frame;
            public string stateHash = string.Empty;
            public int pipelinePacketCount;
            public int pipelineDispatchedCount;
            public int pipelineLastFrame;
            public string recoveryState = string.Empty;
            public bool needsFullSnapshotResync;
            public string lastResyncReason = string.Empty;
            public int lastAuthoritativeFrame;
            public string lastAuthoritativeHash = string.Empty;
            public string lastImportedHash = string.Empty;
            public bool authoritativeHashMatched;
            public int replayTicks;
            public int pendingInputFrames;
            public bool p1Present;
            public float p1x;
            public float p1y;
            public bool p2Present;
            public float p2x;
            public float p2y;
            public List<AuthoritativeSample> samples = new List<AuthoritativeSample>();
        }

        [Serializable]
        private sealed class AuthoritativeSample
        {
            public int frame;
            public string authoritativeHash = string.Empty;
            public string importedHash = string.Empty;
            public bool p1Present;
            public float p1x;
            public float p1y;
            public bool p2Present;
            public float p2x;
            public float p2y;
        }

        [Serializable]
        private sealed class HashMismatchSample
        {
            public int authoritativeFrame;
            public int clientFrame;
            public string authoritativeHash = string.Empty;
            public string importedHash = string.Empty;
            public string freshImportedHash = string.Empty;
            public string packedPayloadPath = string.Empty;
            public string freshExportedPayloadPath = string.Empty;
        }
    }
}
