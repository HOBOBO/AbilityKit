#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
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
        private const int MaxRemotePlayerPureStateEvents = 64;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

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
        private static double _lastSnapshotTime;
        private static double _movementProbeStartTime;
        private static long _editorUpdateCount;
        private static double _lastEditorUpdateGapMs;
        private static double _maxEditorUpdateGapMs;
        private static double _maxSnapshotGapMs;
        private static double _inputRoundTripTotalMs;
        private static double _maxInputRoundTripMs;
        private static double _firstMovementResponseMs = -1d;
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
        private static int _lastActorAuthoritativeFrame;
        private static int _movementSampleCount;
        private static bool _aoiInitialViewsCaptured;
        private static bool _remotePlayerViewObserved;
        private static bool _remotePlayerViewRemoved;
        private static int _aoiInitialPlayerViewCount;
        private static int _aoiMaxPlayerViewCount;
        private static int _remotePlayerViewEnterFrame;
        private static int _remotePlayerViewLeaveFrame;
        private static int _pureStateAppliedCount;
        private static int _pureStateFullAppliedCount;
        private static int _pureStateDeltaAppliedCount;
        private static int _pureStateSpawnCount;
        private static int _pureStateUpdateCount;
        private static int _pureStateDespawnCount;
        private static int _pureStateLowFrequencyUpdateCount;
        private static int _remotePlayerSpawnFrame;
        private static int _remotePlayerDespawnFrame;
        private static int _remotePlayerFullBaselineRemovalFrame;
        private static int _remotePlayerPostDespawnSpawnCount;
        private static int _remotePlayerPostDespawnUpdateCount;
        private static int _remotePlayerFirstReintroducedFrame;
        private static ShooterPureStateSyncSettings _observedPureStateSettings;
        private static readonly ShooterGatewaySnapshotDecoder SnapshotDecoder = new ShooterGatewaySnapshotDecoder();
        private static readonly Dictionary<int, ShooterGatewayActorSnapshot> AuthoritativeActors =
            new Dictionary<int, ShooterGatewayActorSnapshot>();
        private static readonly List<int> SortedAuthoritativeActorIds = new List<int>();
        private static readonly List<AuthoritativeSample> Samples = new List<AuthoritativeSample>();
        private static readonly List<HashMismatchSample> HashMismatches = new List<HashMismatchSample>();
        private static readonly List<RemotePlayerPureStateEvent> RemotePlayerPureStateEvents =
            new List<RemotePlayerPureStateEvent>();
        private static readonly List<MovementBackwardEvent> MovementBackwardEvents =
            new List<MovementBackwardEvent>();
        private static readonly ShooterDurationMetric EditorUpdateGapMetric = new ShooterDurationMetric();
        private static readonly ShooterDurationMetric SnapshotGapMetric = new ShooterDurationMetric();
        private static readonly ShooterDurationMetric InputRoundTripMetric = new ShooterDurationMetric();

        public static void Run()
        {
            if (_runTask != null)
            {
                throw new InvalidOperationException("Shooter headless command is already running.");
            }

            _options = ClientOptions.Parse(Environment.GetCommandLineArgs());
            _deadlineUtc = DateTime.UtcNow.AddSeconds(_options.TimeoutSeconds);
            _nextStateWriteUtc = DateTime.MinValue;
            _lastEditorTime = MonotonicTimeSeconds();
            EditorUpdateGapMetric.Reset();
            SnapshotGapMetric.Reset();
            InputRoundTripMetric.Reset();
            _runTask = RunAsync(_options);
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Debug.Log($"[ShooterHeadless] Started role={_options.Role} account={_options.Account} gateway={_options.Host}:{_options.Port}");
        }

        private static void Update()
        {
            try
            {
                var editorTime = MonotonicTimeSeconds();
                var rawDeltaSeconds = Math.Max(0d, editorTime - _lastEditorTime);
                _editorUpdateCount++;
                _lastEditorUpdateGapMs = rawDeltaSeconds * 1000d;
                _maxEditorUpdateGapMs = Math.Max(_maxEditorUpdateGapMs, _lastEditorUpdateGapMs);
                EditorUpdateGapMetric.RecordMilliseconds(rawDeltaSeconds * 1000d);
                var deltaTime = (float)Math.Min(0.1d, rawDeltaSeconds);
                _lastEditorTime = editorTime;
                if (ShooterRemoteStateSyncPlayModeHost.IsRunning)
                {
                    ShooterRemoteStateSyncPlayModeHost.Tick(deltaTime);
                }
                else
                {
                    _launcher?.Tick(deltaTime);
                }
                CaptureMovementTrajectory();
                CaptureAoiViewLifecycle();

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

            var requestedTemplate = ShooterAcceptanceCatalog.GetSyncTemplate(options.SyncTemplateId);
            if ((int)requestedTemplate.SyncModel != options.SyncModel)
            {
                throw new InvalidOperationException(
                    $"Shooter headless sync model does not match template. Template={requestedTemplate.Id}, Expected={(int)requestedTemplate.SyncModel}, Actual={options.SyncModel}");
            }

            _profile = CreateProfile(
                options.IsOwner ? 1 : 2,
                2,
                requestedTemplate.Id,
                options.NetworkEnvironmentId,
                options.EnemyBudget);
            var sessionOptions = _profile.BuildSessionOptions();
            var launchSpec = _profile.BuildRoomLaunchSpec(
                sessionOptions,
                options.Region,
                options.ServerId,
                "Shooter Headless " + options.RunId);
            launchSpec = OverrideNetworkEnvironment(in launchSpec, options.NetworkEnvironmentId);
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

            SetStage("ConnectingBattle", "Restoring the active room through the formal GUI battle handoff");
            var request = new DemoMultiplayerLaunchRequest(
                options.Host,
                options.Port,
                options.Region,
                options.ServerId,
                login.AccountId,
                login.SessionToken,
                TimeSpan.FromSeconds(Math.Max(90, options.TimeoutSeconds - 30)));
            var handoffOptions = ShooterFormalMultiplayerController.BuildBattleHandoffLaunchOptions(
                _profile,
                request,
                _roomId);
            ShooterRemoteStateSyncPlayModeHost.SetViewBackend(options.ViewBackend);
            var adoptedLauncher = _launcher;
            _launcher = null;
            var launch = await ShooterRemoteStateSyncPlayModeHost.StartAsync(handoffOptions, adoptedLauncher);
            _battle = launch;
            _runtime = ShooterRemoteStateSyncPlayModeHost.Runtime;
            _battleId = launch.Flow.BattleId;
            _worldId = launch.Flow.WorldId;
            if (!launch.Flow.Started || !launch.Flow.Subscribed || _worldId == 0)
            {
                throw new InvalidOperationException(
                    $"Shooter battle launch incomplete. started={launch.Flow.Started}, subscribed={launch.Flow.Subscribed}, message={launch.Flow.Message}");
            }

            var usesPureStateAoi = UsesPureStateAoi(options.SyncTemplateId, options.SyncModel);
            SetStage("WaitingForBattleViews", "Waiting for the baseline and visible Unity views");
            await WaitUntilAsync(
                () => _fullSnapshotPushCount >= 1
                    && _snapshotAppliedCount >= 1
                    && ShooterRemoteStateSyncPlayModeHost.RenderCount > 0
                    && (usesPureStateAoi
                        ? IsPlayerViewActive((int)_localPlayerId) && IsRemotePlayerViewActive()
                        : CountActiveViews("ShooterPlayer_") >= 2 && CountActiveViews("ShooterEnemy_") > 0),
                usesPureStateAoi
                    ? "initial AOI baseline and local/remote Unity player views"
                    : "initial authoritative Shooter snapshot and Unity player/enemy views",
                cancellationToken);
            CaptureInitialAoiViews();
            SetStage("BattleReady", usesPureStateAoi
                ? "AOI baseline contains local and remote player views"
                : "Authoritative baseline and Unity views are ready");
            await WaitForFileAsync(options.MovementSignalPath, "movement signal", cancellationToken);

            var direction = ResolveMovementDirection(options);
            var movementSubmissions = RequiredMovementSubmissions;
            if (usesPureStateAoi)
            {
                ShooterRemoteStateSyncPlayModeHost.SetInputOverride(
                    _ => new ShooterHostFrameInput(direction, 0f, direction, 0f, false));
            }
            BeginMovementProbe(direction);
            SetStage("Movement", usesPureStateAoi
                ? "Moving players apart beyond the AOI boundary"
                : "Submitting controlled movement to the authoritative world");
            for (var i = 0; i < movementSubmissions; i++)
            {
                _inputAttemptCount++;
                var inputStartTime = MonotonicTimeSeconds();
                var submit = await launch.Battle.SubmitLocalInputToGatewayAsync(
                    direction,
                    0f,
                    direction,
                    0f,
                    fire: i == RequiredMovementSubmissions / 2,
                    timeout: TimeSpan.FromSeconds(10),
                    cancellationToken: cancellationToken);
                RecordInputRoundTrip(inputStartTime);
                if (!submit.Remote.Success)
                {
                    throw new InvalidOperationException(
                        $"Shooter input rejected. status={submit.Remote.Status}, message={submit.Remote.Message}, requested={submit.Local.RequestedFrame}");
                }
                _inputSuccessCount++;
                if (submit.Remote.ShouldResync) _inputResyncCount++;
                await Task.Delay(100, cancellationToken);
            }

            if (usesPureStateAoi)
            {
                SetStage("Movement", "Holding continuous frame input until the remote player leaves AOI");
                await WaitUntilAsync(
                    () => _remotePlayerViewRemoved,
                    "remote player to leave AOI while continuous frame input remains active",
                    cancellationToken);
            }

            if (usesPureStateAoi)
            {
                ShooterRemoteStateSyncPlayModeHost.SetInputOverride(_ => default);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            else
            {
                _inputAttemptCount++;
                var stopInputStartTime = MonotonicTimeSeconds();
                var stop = await launch.Battle.SubmitLocalInputToGatewayAsync(
                    0f, 0f, direction, 0f, false,
                    timeout: TimeSpan.FromSeconds(10),
                    cancellationToken: cancellationToken);
                RecordInputRoundTrip(stopInputStartTime);
                if (!stop.Remote.Success)
                {
                    throw new InvalidOperationException("Shooter stop input was rejected: " + stop.Remote.Message);
                }
                _inputSuccessCount++;
            }
            _movementActive = false;

            var settleStartSnapshots = _snapshotAppliedCount;
            SetStage("Settling", usesPureStateAoi
                ? "Waiting for authoritative AOI removal and inactive remote Unity view"
                : "Waiting for authoritative correction and remote convergence");
            await WaitUntilAsync(
                () => _snapshotAppliedCount >= settleStartSnapshots + RequiredSettleSnapshots &&
                    (!usesPureStateAoi || (_remotePlayerViewRemoved && HasRemotePlayerProtocolRemoval())),
                usesPureStateAoi
                    ? "remote player authoritative AOI removal and inactive Unity view"
                    : "post-input authoritative snapshots",
                cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            SetStage("AwaitFinalize", usesPureStateAoi
                ? "Waiting for the coordinator to finalize observer-specific AOI evidence"
                : "Waiting for the coordinator to select a common authoritative frame");
            WriteState(CaptureState());
            var finalize = await WaitForFinalizeAsync(options.FinalizePath, cancellationToken);
            if (!usesPureStateAoi)
            {
                await WaitUntilAsync(
                    () => FindSample(finalize.frame, finalize.authoritativeHash) != null,
                    $"authoritative sample frame {finalize.frame}",
                    cancellationToken);
            }
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
            if (!string.Equals(
                    state.battleHandoffMode,
                    ShooterRemoteStateSyncLaunchMode.RestoreOnly.ToString(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Shooter battle host did not use the formal RestoreOnly handoff.");
            if (!state.hostRunning || state.hostRenderCount < 1)
                throw new InvalidOperationException("Shooter battle host did not remain running and render a frame.");
            var usesPureStateAoi = UsesPureStateAoi(state.syncTemplateId, state.syncModel);
            if (!usesPureStateAoi && (state.playerViewCount < 2 || state.enemyViewCount < 1))
                throw new InvalidOperationException(
                    $"Shooter Unity views were incomplete. players={state.playerViewCount}, enemies={state.enemyViewCount}");
            if (state.snapshotAppliedCount < 5)
                throw new InvalidOperationException("Shooter client did not apply enough authoritative snapshots.");

            if (usesPureStateAoi)
            {
                if (state.pureStateAppliedCount < 5 || state.pureStateFullAppliedCount < 1 || state.pureStateDeltaAppliedCount < 1)
                    throw new InvalidOperationException(
                        $"Shooter AOI flow did not apply the expected pure-state full/delta snapshots. " +
                        $"pure={state.pureStateAppliedCount}, full={state.pureStateFullAppliedCount}, delta={state.pureStateDeltaAppliedCount}");
                // 非计量网络（ideal/lan）下服务端把 mid/far LOD 提到与 near 一致（3/3/3，
                // 消除低频采样实体的整批跳变）；计量档保持 3/9/30。
                var meteredLod = state.midLodIntervalFrames == 9 && state.farLodIntervalFrames == 30;
                var unmeteredLod = state.midLodIntervalFrames == 3 && state.farLodIntervalFrames == 3;
                if (state.aoiVisibleRadius != 24f || state.aoiBoundaryRadius != 30f ||
                    state.nearLodIntervalFrames != 3 || (!meteredLod && !unmeteredLod))
                    throw new InvalidOperationException(
                        $"Shooter AOI/LOD settings were unexpected. radius={state.aoiVisibleRadius}/{state.aoiBoundaryRadius}, " +
                        $"lod={state.nearLodIntervalFrames}/{state.midLodIntervalFrames}/{state.farLodIntervalFrames}");
                if (!state.remotePlayerViewObserved || !state.remotePlayerViewRemoved ||
                    state.remotePlayerSpawnFrame <= 0 || state.remotePlayerRemovalFrame <= state.remotePlayerSpawnFrame)
                    throw new InvalidOperationException(
                        $"Shooter AOI lifecycle was incomplete. observed={state.remotePlayerViewObserved}, removed={state.remotePlayerViewRemoved}, " +
                        $"spawnFrame={state.remotePlayerSpawnFrame}, removalFrame={state.remotePlayerRemovalFrame}, " +
                        $"removalKind={state.remotePlayerRemovalKind}, despawnFrame={state.remotePlayerDespawnFrame}, " +
                        $"fullBaselineRemovalFrame={state.remotePlayerFullBaselineRemovalFrame}");
                if (state.remotePlayerViewActive || state.playerViewCount != 1)
                    throw new InvalidOperationException(
                        $"Shooter remote Unity player view remained active after authoritative AOI removal. " +
                        $"remoteActive={state.remotePlayerViewActive}, players={state.playerViewCount}, " +
                        $"postDespawnSpawn={state.remotePlayerPostDespawnSpawnCount}, " +
                        $"postDespawnUpdate={state.remotePlayerPostDespawnUpdateCount}, " +
                        $"firstReintroducedFrame={state.remotePlayerFirstReintroducedFrame}");
                if (state.pureStateLowFrequencyUpdateCount < 1)
                    throw new InvalidOperationException("Shooter AOI flow observed no LowFrequency LOD updates.");
            }
            else if (state.syncModel == (int)NetworkSyncModel.AuthoritativeInterpolation)
            {
                if (state.actorSnapshotAppliedCount < 5 || state.fullSnapshotPushCount < 1 || state.deltaSnapshotPushCount < 1)
                    throw new InvalidOperationException(
                        $"Shooter authoritative interpolation did not apply the expected full/delta actor snapshots. " +
                        $"actor={state.actorSnapshotAppliedCount}, full={state.fullSnapshotPushCount}, delta={state.deltaSnapshotPushCount}");
            }
            else if (state.packedSnapshotAppliedCount < 1 || state.fullSnapshotPushCount < 5 || state.deltaSnapshotPushCount != 0)
            {
                throw new InvalidOperationException(
                    $"Shooter runtime snapshot flow did not use full packed snapshots exclusively. " +
                    $"packed={state.packedSnapshotAppliedCount}, full={state.fullSnapshotPushCount}, delta={state.deltaSnapshotPushCount}");
            }

            if (state.authoritativeHashMismatchCount != 0)
                throw new InvalidOperationException(
                    $"Shooter authoritative snapshot imports mismatched {state.authoritativeHashMismatchCount} times: {FormatHashMismatches(state.hashMismatches)}");
            if (state.snapshotImportFailureCount != 0 || state.inputResyncCount != 0 ||
                (!usesPureStateAoi && state.snapshotResyncNeededCount != 0))
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
            if (!usesPureStateAoi && FindSample(finalize.frame, finalize.authoritativeHash) == null)
                throw new InvalidOperationException("Shooter client lacks the coordinator-selected authoritative sample.");
        }

        private static void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode == RoomGatewayOpCodes.RoomStateChanged) _roomPushCount++;
        }

        private static void HandleSnapshotPush(uint opCode, ArraySegment<byte> payload, ShooterSnapshotApplyResult result)
        {
            if (opCode != RoomGatewayOpCodes.SnapshotPushed && opCode != RoomGatewayOpCodes.DeltaSnapshotPushed) return;
            var snapshotTime = MonotonicTimeSeconds();
            if (_lastSnapshotTime > 0d)
            {
                _maxSnapshotGapMs = Math.Max(_maxSnapshotGapMs, (snapshotTime - _lastSnapshotTime) * 1000d);
                SnapshotGapMetric.RecordMilliseconds((snapshotTime - _lastSnapshotTime) * 1000d);
            }
            _lastSnapshotTime = snapshotTime;
            _snapshotPushCount++;
            if (opCode == RoomGatewayOpCodes.SnapshotPushed) _fullSnapshotPushCount++;
            else _deltaSnapshotPushCount++;
            switch (result)
            {
                case ShooterSnapshotApplyResult.AppliedPackedSnapshot:
                    _snapshotAppliedCount++;
                    _packedSnapshotAppliedCount++;
                    CountAuthoritativeHashMismatch();
                    CapturePackedAuthoritativeSample();
                    break;
                case ShooterSnapshotApplyResult.AppliedActorSnapshot:
                    _snapshotAppliedCount++;
                    _actorSnapshotAppliedCount++;
                    if (UsesPureStateAoi(_options?.SyncTemplateId, _options?.SyncModel ?? 0))
                    {
                        CapturePureStateEvidence(payload);
                    }
                    else
                    {
                        CaptureActorAuthoritativeSample(payload);
                    }
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
            var session = _battle?.GatewayConnection.CurrentSession;
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

        private static void CapturePackedAuthoritativeSample()
        {
            var session = _battle?.GatewayConnection.CurrentSession;
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
            AddOrReplaceSample(sample);
        }

        private static void CapturePureStateEvidence(ArraySegment<byte> payload)
        {
            var snapshot = SnapshotDecoder.Decode(payload);
            if (!snapshot.PureStateSnapshot.HasValue) return;

            var pureState = snapshot.PureStateSnapshot.Value;
            _pureStateAppliedCount++;
            if (pureState.SnapshotKind == ShooterPureStateSnapshotKinds.FullBaseline) _pureStateFullAppliedCount++;
            else _pureStateDeltaAppliedCount++;
            _observedPureStateSettings = pureState.Settings;
            _lastActorAuthoritativeFrame = Math.Max(_lastActorAuthoritativeFrame, pureState.Frame);

            var remotePlayerId = ResolveRemotePlayerId();
            var entities = pureState.Entities ?? Array.Empty<ShooterPureStateEntityDelta>();
            var entityCount = Math.Min(pureState.EffectiveEntityCount, entities.Length);
            var remotePlayerPresentInFullBaseline = false;
            for (var i = 0; i < entityCount; i++)
            {
                var entity = entities[i];
                var isRemotePlayer = entity.EntityKind == ShooterPackedEntityKinds.Player &&
                    entity.EntityId == remotePlayerId;
                if (isRemotePlayer)
                {
                    remotePlayerPresentInFullBaseline = entity.DeltaKind != ShooterPureStateDeltaKinds.Despawn;
                    CaptureRemotePlayerPureStateEvent(in pureState, in entity);
                }

                switch (entity.DeltaKind)
                {
                    case ShooterPureStateDeltaKinds.Spawn:
                        _pureStateSpawnCount++;
                        if (isRemotePlayer)
                        {
                            var removalFrame = ResolveRemotePlayerRemovalFrame();
                            if (removalFrame > 0 && pureState.Frame > removalFrame)
                            {
                                _remotePlayerPostDespawnSpawnCount++;
                                CaptureRemotePlayerReintroduction(pureState.Frame);
                            }
                            _remotePlayerSpawnFrame = pureState.Frame;
                        }
                        break;
                    case ShooterPureStateDeltaKinds.Update:
                        _pureStateUpdateCount++;
                        if ((entity.Flags & ShooterPureStateEntityFlags.LowFrequency) != 0)
                            _pureStateLowFrequencyUpdateCount++;
                        var remoteRemovalFrame = ResolveRemotePlayerRemovalFrame();
                        if (isRemotePlayer && remoteRemovalFrame > 0 && pureState.Frame > remoteRemovalFrame)
                        {
                            _remotePlayerPostDespawnUpdateCount++;
                            CaptureRemotePlayerReintroduction(pureState.Frame);
                        }
                        break;
                    case ShooterPureStateDeltaKinds.Despawn:
                        _pureStateDespawnCount++;
                        if (isRemotePlayer)
                            _remotePlayerDespawnFrame = pureState.Frame;
                        break;
                }
            }

            if (pureState.SnapshotKind == ShooterPureStateSnapshotKinds.FullBaseline &&
                _remotePlayerSpawnFrame > 0 && !remotePlayerPresentInFullBaseline &&
                _remotePlayerFullBaselineRemovalFrame == 0)
            {
                _remotePlayerFullBaselineRemovalFrame = pureState.Frame;
            }
        }

        private static bool HasRemotePlayerProtocolRemoval() => ResolveRemotePlayerRemovalFrame() > 0;

        private static int ResolveRemotePlayerRemovalFrame()
        {
            if (_remotePlayerDespawnFrame <= 0) return _remotePlayerFullBaselineRemovalFrame;
            if (_remotePlayerFullBaselineRemovalFrame <= 0) return _remotePlayerDespawnFrame;
            return Math.Min(_remotePlayerDespawnFrame, _remotePlayerFullBaselineRemovalFrame);
        }

        private static string ResolveRemotePlayerRemovalKind()
        {
            var removalFrame = ResolveRemotePlayerRemovalFrame();
            if (removalFrame <= 0) return string.Empty;
            return removalFrame == _remotePlayerDespawnFrame ? "Despawn" : "FullBaselineOmission";
        }

        private static void CaptureRemotePlayerPureStateEvent(
            in ShooterPureStateSnapshotPayload snapshot,
            in ShooterPureStateEntityDelta entity)
        {
            if (RemotePlayerPureStateEvents.Count >= MaxRemotePlayerPureStateEvents)
            {
                RemotePlayerPureStateEvents.RemoveAt(0);
            }

            RemotePlayerPureStateEvents.Add(new RemotePlayerPureStateEvent
            {
                frame = snapshot.Frame,
                snapshotKind = snapshot.SnapshotKind,
                deltaKind = entity.DeltaKind,
                flags = entity.Flags,
                quantizedX = entity.QuantizedX,
                quantizedY = entity.QuantizedY
            });
        }

        private static void CaptureRemotePlayerReintroduction(int frame)
        {
            if (_remotePlayerFirstReintroducedFrame <= 0)
            {
                _remotePlayerFirstReintroducedFrame = frame;
            }
        }

        private static void CaptureActorAuthoritativeSample(ArraySegment<byte> payload)
        {
            var snapshot = SnapshotDecoder.Decode(payload);
            if (snapshot.Frame <= 0) return;

            if (snapshot.IsFullSnapshot)
            {
                AuthoritativeActors.Clear();
            }
            for (var i = 0; i < snapshot.Actors.Count; i++)
            {
                var actor = snapshot.Actors[i];
                AuthoritativeActors[actor.ActorId] = actor;
            }
            if (AuthoritativeActors.Count == 0) return;

            _lastActorAuthoritativeFrame = snapshot.Frame;
            var hash = ComputeActorAuthoritativeHash(snapshot.Frame);
            var sample = new AuthoritativeSample
            {
                frame = snapshot.Frame,
                authoritativeHash = FormatHash(hash),
                importedHash = FormatHash(hash)
            };
            CaptureAuthoritativeActor(1, out sample.p1Present, out sample.p1x, out sample.p1y);
            CaptureAuthoritativeActor(2, out sample.p2Present, out sample.p2x, out sample.p2y);
            AddOrReplaceSample(sample);
        }

        private static uint ComputeActorAuthoritativeHash(int frame)
        {
            SortedAuthoritativeActorIds.Clear();
            foreach (var actorId in AuthoritativeActors.Keys) SortedAuthoritativeActorIds.Add(actorId);
            SortedAuthoritativeActorIds.Sort();

            var hash = FnvOffsetBasis;
            HashInt(ref hash, frame);
            HashInt(ref hash, SortedAuthoritativeActorIds.Count);
            for (var i = 0; i < SortedAuthoritativeActorIds.Count; i++)
            {
                var actor = AuthoritativeActors[SortedAuthoritativeActorIds[i]];
                HashInt(ref hash, actor.ActorId);
                HashFloat(ref hash, actor.X);
                HashFloat(ref hash, actor.Y);
                HashFloat(ref hash, actor.Rotation);
                HashFloat(ref hash, actor.VelocityX);
                HashFloat(ref hash, actor.VelocityY);
                HashFloat(ref hash, actor.Hp);
                HashFloat(ref hash, actor.HpMax);
                HashInt(ref hash, actor.TeamId);
            }
            return hash;
        }

        private static void HashFloat(ref uint hash, float value)
        {
            HashInt(ref hash, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        private static void HashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash = (hash ^ (byte)value) * FnvPrime;
                hash = (hash ^ (byte)(value >> 8)) * FnvPrime;
                hash = (hash ^ (byte)(value >> 16)) * FnvPrime;
                hash = (hash ^ (byte)(value >> 24)) * FnvPrime;
            }
        }

        private static void CaptureAuthoritativeActor(int actorId, out bool present, out float x, out float y)
        {
            present = AuthoritativeActors.TryGetValue(actorId, out var actor);
            x = present ? actor.X : 0f;
            y = present ? actor.Y : 0f;
        }

        private static void AddOrReplaceSample(AuthoritativeSample sample)
        {
            for (var i = Samples.Count - 1; i >= 0; i--)
            {
                if (Samples[i].frame != sample.frame) continue;
                Samples[i] = sample;
                return;
            }
            Samples.Add(sample);
            if (Samples.Count > MaxSamples) Samples.RemoveAt(0);
        }

        private static double MonotonicTimeSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private static void RecordInputRoundTrip(double startTime)
        {
            var elapsedMs = Math.Max(0d, (MonotonicTimeSeconds() - startTime) * 1000d);
            _inputRoundTripTotalMs += elapsedMs;
            _maxInputRoundTripMs = Math.Max(_maxInputRoundTripMs, elapsedMs);
            InputRoundTripMetric.RecordMilliseconds(elapsedMs);
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
            _lastMovementAuthoritativeFrame = ResolveLatestAuthoritativeFrame();
            _movementSampleCount = 0;
            MovementBackwardEvents.Clear();
            _movementProbeStartTime = MonotonicTimeSeconds();
            _firstMovementResponseMs = -1d;
            _hasMovementBaseline = true;
            _movementActive = true;
        }

        private static void CaptureMovementTrajectory()
        {
            var options = _options;
            var runtime = _runtime;
            if (!_movementActive || !_hasMovementBaseline || options == null || runtime == null || _localPlayerId == 0) return;
            if (!runtime.TryGetPlayer((int)_localPlayerId, out var player)) return;
            var direction = ResolveMovementDirection(options);
            var progress = (player.X - _movementBaselineX) * direction;
            var backward = _lastMovementProgress - progress;
            var authoritativeFrame = ResolveLatestAuthoritativeFrame();
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

                RecordMovementBackwardEvent(
                    backward,
                    _lastMovementProgress,
                    progress,
                    _lastMovementAuthoritativeFrame,
                    authoritativeFrame);
            }
            if (progress > _maxMovementProgress) _maxMovementProgress = progress;
            if (_firstMovementResponseMs < 0d && progress >= 0.05f)
            {
                _firstMovementResponseMs = Math.Max(
                    0d,
                    (MonotonicTimeSeconds() - _movementProbeStartTime) * 1000d);
            }
            _lastMovementProgress = progress;
            _lastMovementAuthoritativeFrame = authoritativeFrame;
            _movementSampleCount++;
        }

        private static void RecordMovementBackwardEvent(
            float backward,
            float previousProgress,
            float currentProgress,
            int previousAuthoritativeFrame,
            int authoritativeFrame)
        {
            const float meaningfulBackwardThreshold = 0.05f;
            const int maxEvents = 32;
            if (backward < meaningfulBackwardThreshold)
            {
                return;
            }

            var dataPlane = ShooterRemoteStateSyncPlayModeHost.BattleDataPlaneDiagnostics;
            var item = new MovementBackwardEvent
            {
                backward = backward,
                previousProgress = previousProgress,
                currentProgress = currentProgress,
                previousAuthoritativeFrame = previousAuthoritativeFrame,
                authoritativeFrame = authoritativeFrame,
                authorityAdvanced = authoritativeFrame != previousAuthoritativeFrame,
                runtimeFrame = _battle?.Session.CurrentFrame ?? _runtime?.CurrentFrame ?? 0,
                snapshotAppliedCount = _snapshotAppliedCount,
                snapshotResyncNeededCount = _snapshotResyncNeededCount,
                queueDepth = dataPlane.QueueDepth,
                peakQueueDepth = dataPlane.PeakQueueDepth,
                lastDrainMilliseconds = dataPlane.LastDrainMilliseconds,
                editorUpdateGapMilliseconds = _lastEditorUpdateGapMs
            };
            if (MovementBackwardEvents.Count < maxEvents)
            {
                MovementBackwardEvents.Add(item);
                return;
            }

            var smallestIndex = 0;
            for (var i = 1; i < MovementBackwardEvents.Count; i++)
            {
                if (MovementBackwardEvents[i].backward < MovementBackwardEvents[smallestIndex].backward)
                {
                    smallestIndex = i;
                }
            }

            if (backward > MovementBackwardEvents[smallestIndex].backward)
            {
                MovementBackwardEvents[smallestIndex] = item;
            }
        }

        private static int ResolveLatestAuthoritativeFrame()
        {
            var packedFrame = _battle?.GatewayConnection.CurrentSession
                ?.FrameSync.LastImportedSnapshotEvidence.Frame ?? 0;
            return Math.Max(packedFrame, _lastActorAuthoritativeFrame);
        }

        private static void CaptureInitialAoiViews()
        {
            if (!UsesPureStateAoi(_options?.SyncTemplateId, _options?.SyncModel ?? 0)) return;
            _aoiInitialPlayerViewCount = CountActiveViews("ShooterPlayer_");
            _aoiMaxPlayerViewCount = Math.Max(_aoiMaxPlayerViewCount, _aoiInitialPlayerViewCount);
            _aoiInitialViewsCaptured = true;
            CaptureAoiViewLifecycle();
        }

        private static void CaptureAoiViewLifecycle()
        {
            if (!UsesPureStateAoi(_options?.SyncTemplateId, _options?.SyncModel ?? 0) || _localPlayerId == 0) return;
            var playerViewCount = CountActiveViews("ShooterPlayer_");
            _aoiMaxPlayerViewCount = Math.Max(_aoiMaxPlayerViewCount, playerViewCount);
            var remoteActive = IsRemotePlayerViewActive();
            if (remoteActive && !_remotePlayerViewObserved)
            {
                _remotePlayerViewObserved = true;
                _remotePlayerViewEnterFrame = ResolveLatestAuthoritativeFrame();
            }
            else if (_aoiInitialViewsCaptured && _remotePlayerViewObserved && !remoteActive && !_remotePlayerViewRemoved)
            {
                _remotePlayerViewRemoved = true;
                _remotePlayerViewLeaveFrame = ResolveLatestAuthoritativeFrame();
            }
        }

        private static int ResolveRemotePlayerId() => _localPlayerId == 1 ? 2 : 1;

        private static bool IsRemotePlayerViewActive() => IsPlayerViewActive(ResolveRemotePlayerId());

        private static bool IsPlayerViewActive(int playerId)
        {
            if (playerId <= 0) return false;
            var diagnostics = ShooterRemoteStateSyncPlayModeHost.ViewRenderDiagnostics;
            if (diagnostics.Backend == ShooterUnityViewRenderBackend.GpuInstancedDotsReady)
            {
                if (playerId == _localPlayerId)
                {
                    return diagnostics.HasControlledPlayer;
                }

                var controlledPlayerCount = diagnostics.HasControlledPlayer ? 1 : 0;
                return diagnostics.PlayerCount > controlledPlayerCount;
            }

            var expectedName = "ShooterPlayer_" + playerId.ToString(CultureInfo.InvariantCulture);
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i].activeInHierarchy && string.Equals(objects[i].name, expectedName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static float ResolveMovementDirection(ClientOptions options)
        {
            return UsesPureStateAoi(options.SyncTemplateId, options.SyncModel)
                ? (options.IsOwner ? -1f : 1f)
                : (options.IsOwner ? 1f : -1f);
        }

        private static bool UsesPureStateAoi(string? syncTemplateId, int syncModel)
        {
            return syncModel == (int)NetworkSyncModel.MassBattleLodSync ||
                string.Equals(syncTemplateId, ShooterSyncTemplateIds.MassBattleLodAoi, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountActiveViews(string namePrefix)
        {
            var diagnostics = ShooterRemoteStateSyncPlayModeHost.ViewRenderDiagnostics;
            if (diagnostics.Backend == ShooterUnityViewRenderBackend.GpuInstancedDotsReady)
            {
                if (string.Equals(namePrefix, "ShooterPlayer_", StringComparison.Ordinal)) return diagnostics.PlayerCount;
                if (string.Equals(namePrefix, "ShooterBullet_", StringComparison.Ordinal)) return diagnostics.BulletCount;
                if (string.Equals(namePrefix, "ShooterEnemy_", StringComparison.Ordinal)) return diagnostics.EnemyCount;
            }

            var count = 0;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < objects.Length; i++)
            {
                var view = objects[i];
                if (view.activeInHierarchy && view.name.StartsWith(namePrefix, StringComparison.Ordinal)) count++;
            }
            return count;
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
            var editorUpdateGap = EditorUpdateGapMetric.Capture();
            var snapshotGap = SnapshotGapMetric.Capture();
            var inputRoundTrip = InputRoundTripMetric.Capture();
            var hostPerformance = ShooterRemoteStateSyncPlayModeHost.PerformanceDiagnostics;
            var pureStatePlayback = ShooterRemoteStateSyncPlayModeHost.PureStatePlaybackDiagnostics;
            var dataPlane = ShooterRemoteStateSyncPlayModeHost.BattleDataPlaneDiagnostics;
            var viewRender = ShooterRemoteStateSyncPlayModeHost.ViewRenderDiagnostics;
            var state = new ClientState
            {
                role = _options?.Role ?? string.Empty,
                account = _options?.Account ?? string.Empty,
                syncTemplateId = _options?.SyncTemplateId ?? string.Empty,
                syncModel = _options?.SyncModel ?? 0,
                networkEnvironmentId = _options?.NetworkEnvironmentId ?? string.Empty,
                enemyBudget = _options?.EnemyBudget ?? 0,
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
                automaticFullStateSyncCoalescedRequestCount = ShooterRemoteStateSyncPlayModeHost.Battle?.AutomaticFullStateSyncCoalescedRequestCount ?? 0L,
                authoritativeHashMismatchCount = _authoritativeHashMismatchCount,
                hashMismatches = new List<HashMismatchSample>(HashMismatches),
                inputAttemptCount = _inputAttemptCount,
                inputSuccessCount = _inputSuccessCount,
                inputResyncCount = _inputResyncCount,
                averageInputRoundTripMs = _inputSuccessCount > 0
                    ? _inputRoundTripTotalMs / _inputSuccessCount
                    : 0d,
                maxInputRoundTripMs = _maxInputRoundTripMs,
                firstMovementResponseMs = _firstMovementResponseMs,
                editorUpdateCount = _editorUpdateCount,
                maxEditorUpdateGapMs = _maxEditorUpdateGapMs,
                maxSnapshotGapMs = _maxSnapshotGapMs,
                p95EditorUpdateGapMs = editorUpdateGap.P95Milliseconds,
                p99EditorUpdateGapMs = editorUpdateGap.P99Milliseconds,
                p95SnapshotGapMs = snapshotGap.P95Milliseconds,
                p99SnapshotGapMs = snapshotGap.P99Milliseconds,
                p50InputRoundTripMs = inputRoundTrip.P50Milliseconds,
                p95InputRoundTripMs = inputRoundTrip.P95Milliseconds,
                p99InputRoundTripMs = inputRoundTrip.P99Milliseconds,
                syncFrameCount = hostPerformance.FrameCount,
                syncHitchCount = hostPerformance.HitchCount,
                p50SyncFrameMs = hostPerformance.Frame.P50Milliseconds,
                p95SyncFrameMs = hostPerformance.Frame.P95Milliseconds,
                p99SyncFrameMs = hostPerformance.Frame.P99Milliseconds,
                p95LauncherMs = hostPerformance.Launcher.P95Milliseconds,
                p95SessionTickMs = hostPerformance.SessionTick.P95Milliseconds,
                p95PresentationBuildMs = hostPerformance.PresentationBuild.P95Milliseconds,
                p95ViewRenderMs = hostPerformance.ViewRender.P95Milliseconds,
                averageGcBytesPerFrame = hostPerformance.AverageAllocatedBytes,
                maxGcBytesPerFrame = hostPerformance.MaxAllocatedBytes,
                pureStatePlaybackRenderTickCount = pureStatePlayback.RenderTickCount,
                pureStatePlaybackPublishedSnapshotCount = pureStatePlayback.PublishedSnapshotCount,
                pureStatePlaybackStarvedRenderTickCount = pureStatePlayback.StarvedRenderTickCount,
                pureStatePlaybackHeldRenderTickCount = pureStatePlayback.HeldPlaybackRenderTickCount,
                pureStatePlaybackStarvationRatio = pureStatePlayback.StarvationRatio,
                pureStatePlaybackHeldRatio = pureStatePlayback.HeldPlaybackRatio,
                pureStatePlaybackBufferedSnapshotCount = pureStatePlayback.BufferedSnapshotCount,
                pureStatePlaybackBufferedFrameSpan = pureStatePlayback.BufferedFrameSpan,
                pureStatePlaybackLeadFrames = pureStatePlayback.AvailablePlaybackLeadFrames,
                pureStatePlaybackFrame = pureStatePlayback.PlaybackFrame,
                pureStatePlaybackCurrentDelayFrames = pureStatePlayback.CurrentDelayFrames,
                pureStatePlaybackTargetDelayFrames = pureStatePlayback.TargetDelayFrames,
                pureStatePlaybackBaseDelayFrames = pureStatePlayback.BaseDelayFrames,
                pureStatePlaybackMaxDelayFrames = pureStatePlayback.MaxDelayFrames,
                pureStatePlaybackIsStarved = pureStatePlayback.IsStarved,
                pureStatePlaybackReceivedSampleBlockCount = pureStatePlayback.ReceivedSampleBlockCount,
                pureStatePlaybackReceivedFrameSampleCount = pureStatePlayback.ReceivedFrameSampleCount,
                pureStatePlaybackRejectedFrameSampleCount = pureStatePlayback.RejectedFrameSampleCount,
                pureStatePlaybackStaleFrameSampleCount = pureStatePlayback.StaleFrameSampleCount,
                pureStatePlaybackInvalidFrameSampleCount = pureStatePlayback.InvalidFrameSampleCount,
                pureStatePlaybackAverageFrameSamplesPerBlock = pureStatePlayback.AverageFrameSamplesPerBlock,
                pureStatePlaybackReceivedTransformSampleCount = pureStatePlayback.ReceivedTransformSampleCount,
                pureStatePlaybackMaxTransformSampleCountPerBlock = pureStatePlayback.MaxTransformSampleCountPerBlock,
                pureStatePlaybackReceivedAuthoritativeTransformCount = pureStatePlayback.ReceivedAuthoritativeTransformCount,
                pureStatePlaybackAverageTransformSamplesPerFrame = pureStatePlayback.AverageTransformSamplesPerFrame,
                pureStatePlaybackHistoricalTransformAmplificationRatio = pureStatePlayback.HistoricalTransformAmplificationRatio,
                pureStatePlaybackObservedTransformSampleIntervalCount = pureStatePlayback.ObservedTransformSampleIntervalCount,
                pureStatePlaybackTransformSampleIntervalP50Frames = pureStatePlayback.TransformSampleIntervalP50Frames,
                pureStatePlaybackTransformSampleIntervalP95Frames = pureStatePlayback.TransformSampleIntervalP95Frames,
                pureStatePlaybackTransformSampleIntervalP99Frames = pureStatePlayback.TransformSampleIntervalP99Frames,
                pureStatePlaybackTransformSampleIntervalMaxFrames = pureStatePlayback.TransformSampleIntervalMaxFrames,
                battlePushQueueDepth = dataPlane.QueueDepth,
                battlePushPeakQueueDepth = dataPlane.PeakQueueDepth,
                battlePushEnqueuedCount = dataPlane.EnqueuedPushCount,
                battlePushProcessedCount = dataPlane.ProcessedPushCount,
                battlePushCoalescedSnapshotCount = dataPlane.CoalescedSnapshotCount,
                battlePushDrainCount = dataPlane.DrainCount,
                battlePushBudgetLimitedDrainCount = dataPlane.BudgetLimitedDrainCount,
                battlePushLastDrainProcessedCount = dataPlane.LastDrainProcessedCount,
                battlePushLastDrainMs = dataPlane.LastDrainMilliseconds,
                battlePushOldestQueueMs = dataPlane.OldestQueuedMilliseconds,
                p95BattlePushQueueWaitMs = dataPlane.QueueWait.P95Milliseconds,
                p99BattlePushQueueWaitMs = dataPlane.QueueWait.P99Milliseconds,
                p95BattlePushApplyMs = dataPlane.PushProcess.P95Milliseconds,
                p99BattlePushApplyMs = dataPlane.PushProcess.P99Milliseconds,
                battlePushFullSnapshotProcessCount = dataPlane.FullSnapshotProcess.SampleCount,
                battlePushFullSnapshotApplyMaxMs = dataPlane.FullSnapshotProcess.MaxMilliseconds,
                battlePushDeltaSnapshotProcessCount = dataPlane.DeltaSnapshotProcess.SampleCount,
                battlePushDeltaSnapshotApplyP95Ms = dataPlane.DeltaSnapshotProcess.P95Milliseconds,
                battlePushDeltaSnapshotApplyP99Ms = dataPlane.DeltaSnapshotProcess.P99Milliseconds,
                battlePushDeltaSnapshotApplyMaxMs = dataPlane.DeltaSnapshotProcess.MaxMilliseconds,
                battlePushReliableEventProcessCount = dataPlane.ReliableEventProcess.SampleCount,
                battlePushReliableEventApplyP50Ms = dataPlane.ReliableEventProcess.P50Milliseconds,
                battlePushReliableEventApplyP95Ms = dataPlane.ReliableEventProcess.P95Milliseconds,
                battlePushReliableEventApplyP99Ms = dataPlane.ReliableEventProcess.P99Milliseconds,
                battlePushReliableEventApplyMaxMs = dataPlane.ReliableEventProcess.MaxMilliseconds,
                battlePushOtherProcessCount = dataPlane.OtherPushProcess.SampleCount,
                battlePushOtherApplyMaxMs = dataPlane.OtherPushProcess.MaxMilliseconds,
                p95SnapshotArrivalGapMs = dataPlane.SnapshotArrivalGap.P95Milliseconds,
                p99SnapshotArrivalGapMs = dataPlane.SnapshotArrivalGap.P99Milliseconds,
                p95SnapshotSourceAgeMs = dataPlane.SnapshotSourceAge.P95Milliseconds,
                p99SnapshotSourceAgeMs = dataPlane.SnapshotSourceAge.P99Milliseconds,
                battlePushPayloadBytes = dataPlane.ReceivedPayloadBytes,
                battlePushMaxPayloadBytes = dataPlane.MaxPayloadBytes,
                battlePushReceivedCount = dataPlane.EnqueuedPushCount,
                battlePushAveragePayloadBytes = dataPlane.EnqueuedPushCount > 0L
                    ? dataPlane.ReceivedPayloadBytes / (double)dataPlane.EnqueuedPushCount
                    : 0d,
                movementSampleCount = _movementSampleCount,
                maxMovementProgress = _maxMovementProgress,
                maxBackwardMovement = _maxBackwardMovement,
                maxReconciliationBackwardMovement = _maxReconciliationBackwardMovement,
                maxUnexplainedBackwardMovement = _maxUnexplainedBackwardMovement,
                movementBackwardEvents = new List<MovementBackwardEvent>(MovementBackwardEvents),
                battleHandoffMode = ShooterRemoteStateSyncPlayModeHost.LastConnectionResult?.RequestedMode.ToString() ?? string.Empty,
                hostRunning = ShooterRemoteStateSyncPlayModeHost.IsRunning,
                hostRenderCount = ShooterRemoteStateSyncPlayModeHost.RenderCount,
                viewBackend = viewRender.Backend.ToString(),
                viewUsesIndirectRendering = viewRender.UsesIndirectRendering,
                viewFullRebuildCount = viewRender.FullRebuildCount,
                viewIncrementalBatchCount = viewRender.IncrementalBatchCount,
                viewIndirectUploadPassCount = viewRender.IndirectUploadPassCount,
                viewMatrixUploadCallCount = viewRender.MatrixUploadCallCount,
                viewUploadedMatrixCount = viewRender.UploadedMatrixCount,
                viewFullBufferUploadCount = viewRender.FullBufferUploadCount,
                viewPartialUploadRangeCount = viewRender.PartialUploadRangeCount,
                viewHasControlledPlayer = viewRender.HasControlledPlayer,
                playerViewCount = CountActiveViews("ShooterPlayer_"),
                enemyViewCount = CountActiveViews("ShooterEnemy_"),
                aoiInitialPlayerViewCount = _aoiInitialPlayerViewCount,
                aoiMaxPlayerViewCount = _aoiMaxPlayerViewCount,
                remotePlayerViewObserved = _remotePlayerViewObserved,
                remotePlayerViewRemoved = _remotePlayerViewRemoved,
                remotePlayerViewActive = IsRemotePlayerViewActive(),
                remotePlayerViewEnterFrame = _remotePlayerViewEnterFrame,
                remotePlayerViewLeaveFrame = _remotePlayerViewLeaveFrame,
                pureStateAppliedCount = _pureStateAppliedCount,
                pureStateFullAppliedCount = _pureStateFullAppliedCount,
                pureStateDeltaAppliedCount = _pureStateDeltaAppliedCount,
                pureStateSpawnCount = _pureStateSpawnCount,
                pureStateUpdateCount = _pureStateUpdateCount,
                pureStateDespawnCount = _pureStateDespawnCount,
                pureStateLowFrequencyUpdateCount = _pureStateLowFrequencyUpdateCount,
                remotePlayerSpawnFrame = _remotePlayerSpawnFrame,
                remotePlayerDespawnFrame = _remotePlayerDespawnFrame,
                remotePlayerFullBaselineRemovalFrame = _remotePlayerFullBaselineRemovalFrame,
                remotePlayerRemovalFrame = ResolveRemotePlayerRemovalFrame(),
                remotePlayerRemovalKind = ResolveRemotePlayerRemovalKind(),
                remotePlayerPostDespawnSpawnCount = _remotePlayerPostDespawnSpawnCount,
                remotePlayerPostDespawnUpdateCount = _remotePlayerPostDespawnUpdateCount,
                remotePlayerFirstReintroducedFrame = _remotePlayerFirstReintroducedFrame,
                remotePlayerPureStateEvents = new List<RemotePlayerPureStateEvent>(RemotePlayerPureStateEvents),
                aoiVisibleRadius = 24f,
                aoiBoundaryRadius = 30f,
                nearLodIntervalFrames = _observedPureStateSettings.NearLodIntervalFrames,
                midLodIntervalFrames = _observedPureStateSettings.MidLodIntervalFrames,
                farLodIntervalFrames = _observedPureStateSettings.FarLodIntervalFrames,
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
                state.snapshotFrameAge = diagnostics.LastFrame > 0
                    ? Math.Max(0, session.CurrentFrame - diagnostics.LastFrame)
                    : 0;
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

        private static ShooterMultiplayerProfileSO CreateProfile(
            int playerId,
            int playerCount,
            string syncTemplateId,
            string networkEnvironmentId,
            int enemyBudget)
        {
            var profile = ScriptableObject.CreateInstance<ShooterMultiplayerProfileSO>();
            SetProfileField(profile, "syncTemplateId", syncTemplateId);
            SetProfileField(
                profile,
                "networkEnvironmentId",
                string.IsNullOrWhiteSpace(networkEnvironmentId)
                    ? ShooterRoomLaunchSpec.DefaultNetworkEnvironmentId
                    : networkEnvironmentId.Trim());
            SetProfileField(profile, "controlledPlayerId", playerId);
            SetProfileField(profile, "playerCount", playerCount);
            SetProfileField(profile, "maxPlayers", playerCount);
            SetProfileField(profile, "enemyBudget", Math.Max(1, enemyBudget));
            SetProfileField(profile, "autoReady", false);
            SetProfileField(profile, "autoStart", false);
            return profile;
        }

        private static ShooterRoomLaunchSpec OverrideNetworkEnvironment(
            in ShooterRoomLaunchSpec source,
            string networkEnvironmentId)
        {
            if (string.IsNullOrWhiteSpace(networkEnvironmentId) ||
                string.Equals(source.NetworkEnvironmentId, networkEnvironmentId, StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }

            var normalized = networkEnvironmentId.Trim();
            var tags = new Dictionary<string, string>(source.Tags, StringComparer.Ordinal)
            {
                [ShooterRoomLaunchTagKeys.NetworkEnvironmentId] = normalized
            };
            return new ShooterRoomLaunchSpec(
                source.Region,
                source.ServerId,
                source.RoomTitle,
                source.MaxPlayers,
                source.GameplayId,
                source.RuleSetId,
                source.ConfigVersion,
                source.ProtocolVersion,
                source.WorldType,
                source.ClientId,
                tags,
                source.SyncTemplateId,
                source.SyncModel,
                normalized,
                source.CarrierName,
                source.EnableAuthoritativeWorld,
                source.InterpolationEnabled,
                source.InputDelayFrames);
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
            try { ShooterRemoteStateSyncPlayModeHost.Stop(); } catch { }
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
            public string SyncTemplateId = ShooterSyncTemplateIds.StateSyncAuthority;
            public int SyncModel = ShooterRoomLaunchSpec.DefaultSyncModel;
            public string NetworkEnvironmentId = string.Empty;
            public int EnemyBudget = ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget;
            public int TimeoutSeconds = 240;
            public ShooterUnityViewRenderBackend ViewBackend = ShooterUnityViewRenderBackend.GameObject;
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
                    SyncTemplateId = Value(args, "-shooterHeadlessSyncTemplate") ?? ShooterSyncTemplateIds.StateSyncAuthority,
                    SyncModel = IntValue(args, "-shooterHeadlessSyncModel", ShooterRoomLaunchSpec.DefaultSyncModel),
                    NetworkEnvironmentId = Value(args, "-shooterHeadlessNetworkEnvironment") ?? string.Empty,
                    EnemyBudget = IntValue(args, "-shooterHeadlessEnemyBudget", ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget),
                    TimeoutSeconds = IntValue(args, "-shooterHeadlessTimeoutSeconds", 240),
                    ViewBackend = ParseViewBackend(Value(args, "-shooterHeadlessViewBackend"))
                };
            }

            private static ShooterUnityViewRenderBackend ParseViewBackend(string? value)
            {
                if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "gameobject", StringComparison.OrdinalIgnoreCase))
                {
                    return ShooterUnityViewRenderBackend.GameObject;
                }

                if (string.Equals(value, "gpu", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "gpuinstanced", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, nameof(ShooterUnityViewRenderBackend.GpuInstancedDotsReady), StringComparison.OrdinalIgnoreCase))
                {
                    return ShooterUnityViewRenderBackend.GpuInstancedDotsReady;
                }

                throw new ArgumentException("Unsupported Shooter view backend: " + value);
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
            public string syncTemplateId = string.Empty;
            public int syncModel;
            public string networkEnvironmentId = string.Empty;
            public int enemyBudget;
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
            public long automaticFullStateSyncCoalescedRequestCount;
            public int authoritativeHashMismatchCount;
            public List<HashMismatchSample> hashMismatches = new List<HashMismatchSample>();
            public int inputAttemptCount;
            public int inputSuccessCount;
            public int inputResyncCount;
            public double averageInputRoundTripMs;
            public double maxInputRoundTripMs;
            public double firstMovementResponseMs;
            public long editorUpdateCount;
            public double maxEditorUpdateGapMs;
            public double maxSnapshotGapMs;
            public double p95EditorUpdateGapMs;
            public double p99EditorUpdateGapMs;
            public double p95SnapshotGapMs;
            public double p99SnapshotGapMs;
            public double p50InputRoundTripMs;
            public double p95InputRoundTripMs;
            public double p99InputRoundTripMs;
            public long syncFrameCount;
            public long syncHitchCount;
            public double p50SyncFrameMs;
            public double p95SyncFrameMs;
            public double p99SyncFrameMs;
            public double p95LauncherMs;
            public double p95SessionTickMs;
            public double p95PresentationBuildMs;
            public double p95ViewRenderMs;
            public double averageGcBytesPerFrame;
            public long maxGcBytesPerFrame;
            public long pureStatePlaybackRenderTickCount;
            public long pureStatePlaybackPublishedSnapshotCount;
            public long pureStatePlaybackStarvedRenderTickCount;
            public long pureStatePlaybackHeldRenderTickCount;
            public double pureStatePlaybackStarvationRatio;
            public double pureStatePlaybackHeldRatio;
            public int pureStatePlaybackBufferedSnapshotCount;
            public float pureStatePlaybackBufferedFrameSpan;
            public float pureStatePlaybackLeadFrames;
            public float pureStatePlaybackFrame;
            public float pureStatePlaybackCurrentDelayFrames;
            public float pureStatePlaybackTargetDelayFrames;
            public int pureStatePlaybackBaseDelayFrames;
            public int pureStatePlaybackMaxDelayFrames;
            public bool pureStatePlaybackIsStarved;
            public long pureStatePlaybackReceivedSampleBlockCount;
            public long pureStatePlaybackReceivedFrameSampleCount;
            public long pureStatePlaybackRejectedFrameSampleCount;
            public long pureStatePlaybackStaleFrameSampleCount;
            public long pureStatePlaybackInvalidFrameSampleCount;
            public double pureStatePlaybackAverageFrameSamplesPerBlock;
            public long pureStatePlaybackReceivedTransformSampleCount;
            public int pureStatePlaybackMaxTransformSampleCountPerBlock;
            public long pureStatePlaybackReceivedAuthoritativeTransformCount;
            public double pureStatePlaybackAverageTransformSamplesPerFrame;
            public double pureStatePlaybackHistoricalTransformAmplificationRatio;
            public long pureStatePlaybackObservedTransformSampleIntervalCount;
            public int pureStatePlaybackTransformSampleIntervalP50Frames;
            public int pureStatePlaybackTransformSampleIntervalP95Frames;
            public int pureStatePlaybackTransformSampleIntervalP99Frames;
            public int pureStatePlaybackTransformSampleIntervalMaxFrames;
            public int battlePushQueueDepth;
            public int battlePushPeakQueueDepth;
            public long battlePushEnqueuedCount;
            public long battlePushProcessedCount;
            public long battlePushCoalescedSnapshotCount;
            public long battlePushDrainCount;
            public long battlePushBudgetLimitedDrainCount;
            public int battlePushLastDrainProcessedCount;
            public double battlePushLastDrainMs;
            public double battlePushOldestQueueMs;
            public double p95BattlePushQueueWaitMs;
            public double p99BattlePushQueueWaitMs;
            public double p95BattlePushApplyMs;
            public double p99BattlePushApplyMs;
            public long battlePushFullSnapshotProcessCount;
            public double battlePushFullSnapshotApplyMaxMs;
            public long battlePushDeltaSnapshotProcessCount;
            public double battlePushDeltaSnapshotApplyP95Ms;
            public double battlePushDeltaSnapshotApplyP99Ms;
            public double battlePushDeltaSnapshotApplyMaxMs;
            public long battlePushReliableEventProcessCount;
            public double battlePushReliableEventApplyP50Ms;
            public double battlePushReliableEventApplyP95Ms;
            public double battlePushReliableEventApplyP99Ms;
            public double battlePushReliableEventApplyMaxMs;
            public long battlePushOtherProcessCount;
            public double battlePushOtherApplyMaxMs;
            public double p95SnapshotArrivalGapMs;
            public double p99SnapshotArrivalGapMs;
            public double p95SnapshotSourceAgeMs;
            public double p99SnapshotSourceAgeMs;
            public long battlePushPayloadBytes;
            public int battlePushMaxPayloadBytes;
            public long battlePushReceivedCount;
            public double battlePushAveragePayloadBytes;
            public int movementSampleCount;
            public float maxMovementProgress;
            public float maxBackwardMovement;
            public float maxReconciliationBackwardMovement;
            public float maxUnexplainedBackwardMovement;
            public List<MovementBackwardEvent> movementBackwardEvents = new List<MovementBackwardEvent>();
            public string battleHandoffMode = string.Empty;
            public bool hostRunning;
            public long hostRenderCount;
            public string viewBackend = string.Empty;
            public bool viewUsesIndirectRendering;
            public long viewFullRebuildCount;
            public long viewIncrementalBatchCount;
            public long viewIndirectUploadPassCount;
            public long viewMatrixUploadCallCount;
            public long viewUploadedMatrixCount;
            public long viewFullBufferUploadCount;
            public long viewPartialUploadRangeCount;
            public bool viewHasControlledPlayer;
            public int playerViewCount;
            public int enemyViewCount;
            public int aoiInitialPlayerViewCount;
            public int aoiMaxPlayerViewCount;
            public bool remotePlayerViewObserved;
            public bool remotePlayerViewRemoved;
            public bool remotePlayerViewActive;
            public int remotePlayerViewEnterFrame;
            public int remotePlayerViewLeaveFrame;
            public int pureStateAppliedCount;
            public int pureStateFullAppliedCount;
            public int pureStateDeltaAppliedCount;
            public int pureStateSpawnCount;
            public int pureStateUpdateCount;
            public int pureStateDespawnCount;
            public int pureStateLowFrequencyUpdateCount;
            public int remotePlayerSpawnFrame;
            public int remotePlayerDespawnFrame;
            public int remotePlayerFullBaselineRemovalFrame;
            public int remotePlayerRemovalFrame;
            public string remotePlayerRemovalKind = string.Empty;
            public int remotePlayerPostDespawnSpawnCount;
            public int remotePlayerPostDespawnUpdateCount;
            public int remotePlayerFirstReintroducedFrame;
            public List<RemotePlayerPureStateEvent> remotePlayerPureStateEvents = new List<RemotePlayerPureStateEvent>();
            public float aoiVisibleRadius;
            public float aoiBoundaryRadius;
            public int nearLodIntervalFrames;
            public int midLodIntervalFrames;
            public int farLodIntervalFrames;
            public int frame;
            public string stateHash = string.Empty;
            public int pipelinePacketCount;
            public int pipelineDispatchedCount;
            public int pipelineLastFrame;
            public int snapshotFrameAge;
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
        private sealed class RemotePlayerPureStateEvent
        {
            public int frame;
            public int snapshotKind;
            public int deltaKind;
            public byte flags;
            public int quantizedX;
            public int quantizedY;
        }

        [Serializable]
        private sealed class MovementBackwardEvent
        {
            public float backward;
            public float previousProgress;
            public float currentProgress;
            public int previousAuthoritativeFrame;
            public int authoritativeFrame;
            public bool authorityAdvanced;
            public int runtimeFrame;
            public int snapshotAppliedCount;
            public int snapshotResyncNeededCount;
            public int queueDepth;
            public int peakQueueDepth;
            public double lastDrainMilliseconds;
            public double editorUpdateGapMilliseconds;
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
