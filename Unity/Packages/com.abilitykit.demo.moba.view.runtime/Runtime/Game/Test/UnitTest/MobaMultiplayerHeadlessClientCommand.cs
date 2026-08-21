#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Starter;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AbilityKit.World.ECS;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Runs one side of the formal MOBA multiplayer flow in a batch-mode Unity Editor.
    /// The PowerShell coordinator starts this command twice and supplies a shared room file
    /// plus a movement signal after both clients have entered the same battle.
    /// </summary>
    [InitializeOnLoad]
    public static class MobaMultiplayerHeadlessClientCommand
    {
        private const string StarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";
        private const string RunningKey = "AbilityKit.MobaMultiplayerHeadless.Running";
        private const string OptionsKey = "AbilityKit.MobaMultiplayerHeadless.Options";
        private static readonly TimeSpan StateWriteInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan SoloLobbyObservationDuration = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SkillObservationDuration = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MovementInputDuration = TimeSpan.FromSeconds(2.2);
        private const int SkillSettleRequiredFrames = 30;
        private const float SkillSettlePositionEpsilon = 0.02f;
        private const float KnockupObservationThreshold = 0.20f;
        private const float KnockupLandingEpsilon = 0.10f;
        private const float DamageObservationThreshold = 0.01f;

        private static ClientOptions? _options;
        private static CancellationTokenSource? _lifetime;
        private static MultiplayerRoomFlowController? _controller;
        private static ClientRoomStore? _roomStore;
        private static ClientRoomPushSynchronizer? _pushSynchronizer;
        private static BattleGatewayConfigSO? _gatewayConfig;
        private static GameFlowDomain? _flow;
        private static IMultiplayerGatewayDiagnostics? _gatewayDiagnostics;
        private static Task? _operation;
        private static Task? _starterOperation;
        private static StarterController? _starterController;
        private static ClientStage _stage;
        private static string _stageDetail = string.Empty;
        private static DateTime _deadlineUtc;
        private static DateTime _gatewayConnectedUtc;
        private static DateTime _soloLobbyObservationStartedUtc;
        private static DateTime _movementStartedUtc;
        private static DateTime _skillStartedUtc;
        private static DateTime _nextStateWriteUtc;
        private static int _battleBaselineFrame;
        private static Vector3 _ownerBaselinePosition;
        private static bool _hasOwnerBaseline;
        private static bool _movementStopped;
        private static bool _movementValidated;
        private static bool _hasMovementProgress;
        private static float _lastMovementProgress;
        private static float _maxBackwardMovement;
        private static int _consecutiveBackwardFrames;
        private static int _maxConsecutiveBackwardFrames;
        private static int _movementLastObservedFrame;
        private static int _movementSampleCount;
        private static bool _soloLobbyVerified;
        private static Vector3 _skillBaselinePosition;
        private static float _maxSkillDisplacement;
        private static bool _skillValidated;
        private static Vector3 _skillLastPosition;
        private static Vector3 _skillLastRuntimePosition;
        private static int _skillLastObservedFrame;
        private static int _skillStableFrameCount;
        private static bool _hasSkillLastPosition;
        private static int _skillTargetActorId;
        private static float _skillTargetBaselineHp;
        private static float _skillTargetMinimumHp;
        private static Vector3 _skillTargetBaselineRuntimePosition;
        private static Vector3 _skillTargetBaselinePresentedPosition;
        private static float _skillTargetMaxRuntimeY;
        private static float _skillTargetMaxPresentedY;
        private static bool _skillTargetRuntimeKnockupObserved;
        private static bool _skillTargetPresentedKnockupObserved;
        private static bool _skillTargetLanded;
        private static bool _skillTargetPresentedSampleObserved;

        static MobaMultiplayerHeadlessClientCommand()
        {
            EditorApplication.update -= ContinueInPlayMode;
            EditorApplication.update += ContinueInPlayMode;

            if (SessionState.GetBool(RunningKey, false))
            {
                TryRestoreOptions();
            }
        }

        public static void Run()
        {
            try
            {
                var options = ClientOptions.Parse(Environment.GetCommandLineArgs());
                Directory.CreateDirectory(Path.GetDirectoryName(options.StatePath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.ResultPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.EventLogPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.RoomPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.SkillSignalPath)!);

                SessionState.SetBool(RunningKey, true);
                SessionState.SetString(OptionsKey, JsonConvert.SerializeObject(options));
                ResetRuntime(options);

                var scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException("Multiplayer starter scene could not be opened.");
                }

                WriteState(force: true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Finish(false, "Failed to start headless client: " + exception);
            }
        }

        private static void ContinueInPlayMode()
        {
            if (!SessionState.GetBool(RunningKey, false) ||
                !EditorApplication.isPlaying ||
                EditorApplication.isPaused)
            {
                return;
            }

            try
            {
                if (_options == null && !TryRestoreOptions())
                {
                    throw new InvalidOperationException("Headless client options were lost across domain reload.");
                }

                if (_deadlineUtc == default)
                {
                    _deadlineUtc = DateTime.UtcNow.AddSeconds(_options!.TimeoutSeconds);
                }
                if (DateTime.UtcNow > _deadlineUtc)
                {
                    throw new TimeoutException("Headless multiplayer client timed out. " + BuildDiagnostic());
                }

                Tick();
                WriteState(force: false);
            }
            catch (Exception exception)
            {
                Finish(false, exception + " | " + BuildDiagnostic());
            }
        }

        private static void Tick()
        {
            if (_stage == ClientStage.WaitingForStarter ||
                _stage == ClientStage.StartingFromStarter)
            {
                TickStarterLaunch();
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, DemoSceneRoutes.Moba, StringComparison.Ordinal))
            {
                SetStage(
                    ClientStage.WaitingForEntry,
                    $"waiting for MOBA gameplay scene '{DemoSceneRoutes.Moba}' (active='{activeScene.name}')");
                return;
            }

            if (!GameEntry.IsInitialized)
            {
                SetStage(ClientStage.WaitingForEntry, "waiting for GameEntry after Starter launch");
                return;
            }

            var entry = GameEntry.Instance;
            _controller ??= entry.Get<MultiplayerRoomFlowController>();
            _roomStore ??= entry.Get<ClientRoomStore>();
            _pushSynchronizer ??= entry.Get<ClientRoomPushSynchronizer>();
            _gatewayConfig ??= entry.Get<BattleGatewayConfigSO>();
            _flow ??= entry.Get<GameFlowDomain>();
            _gatewayDiagnostics ??= entry.Get<IMultiplayerGatewayDiagnostics>();
            var gateway = _gatewayDiagnostics;

            if (_movementStartedUtc != default && entry.TryGet(out BattleContext trajectoryContext))
            {
                ObserveMovementTrajectory(trajectoryContext);
            }
            if (_skillStartedUtc != default && entry.TryGet(out BattleContext skillTrajectoryContext))
            {
                ObserveSkillTrajectory(skillTrajectoryContext);
            }

            switch (_stage)
            {
                case ClientStage.WaitingForEntry:
                case ClientStage.WaitingForGateway:
                    if (gateway.ConnectionState != ConnectionState.Connected)
                    {
                        SetStage(ClientStage.WaitingForGateway, gateway.ConnectionState.ToString());
                        return;
                    }

                    if (_gatewayConnectedUtc == default)
                    {
                        _gatewayConnectedUtc = DateTime.UtcNow;
                        SetStage(ClientStage.WaitingForGateway, "connected; allowing formal lobby initialization");
                        return;
                    }

                    if (DateTime.UtcNow - _gatewayConnectedUtc < TimeSpan.FromMilliseconds(750)) return;
                    BeginRoomOperation();
                    return;

                case ClientStage.WaitingForRoom:
                    if (_options!.Role == ClientRole.Member && _operation == null)
                    {
                        if (!TryReadRoomId(out var roomId)) return;
                        StartOperation(
                            JoinAndPrepareAsync(roomId),
                            ClientStage.JoiningRoom,
                            "joining and preparing " + roomId);
                    }
                    return;

                case ClientStage.CreatingRoom:
                case ClientStage.JoiningRoom:
                    if (!CompleteOperation()) return;
                    if (string.IsNullOrWhiteSpace(_controller!.CurrentRoomId))
                    {
                        throw new InvalidOperationException("Room operation completed without an authoritative room id.");
                    }
                    if (_options!.Role == ClientRole.Owner)
                    {
                        _soloLobbyObservationStartedUtc = default;
                        SetStage(
                            ClientStage.ObservingSoloLobby,
                            "verifying owner remains in a one-player lobby before room publication");
                        return;
                    }
                    SetStage(
                        ClientStage.WaitingAllReady,
                        "joined room with the externally driven default loadout");
                    return;

                case ClientStage.ObservingSoloLobby:
                    ObserveSoloLobbyBeforePublication();
                    return;

                case ClientStage.WaitingAllReady:
                    if (_operation != null && !CompleteOperation()) return;
                    if (_controller!.CurrentState == MultiplayerRoomFlowState.Failed)
                    {
                        throw new InvalidOperationException("Room flow failed: " + _controller.LastError);
                    }
                    if (_controller.CurrentState != MultiplayerRoomFlowState.InLobby)
                    {
                        SetStage(ClientStage.WaitingForBattle, "loading started; waiting for battle entry");
                        return;
                    }
                    var players = _controller.CurrentSnapshot?.Players;
                    if (players == null || players.Count < 2 || players.Any(player => !player.LobbyReady))
                    {
                        SetStage(
                            ClientStage.WaitingAllReady,
                            $"waiting for two ready players ({players?.Count ?? 0}/2 joined)");
                        return;
                    }
                    if (_options!.Role == ClientRole.Member)
                    {
                        SetStage(
                            ClientStage.WaitingAllReady,
                            "both players ready; waiting for room owner to start loading");
                        return;
                    }
                    StartOperation(
                        _controller.BeginLoadingAsync(_lifetime!.Token),
                        ClientStage.StartingMatch,
                        "room owner starting the match");
                    return;

                case ClientStage.StartingMatch:
                    if (!CompleteOperation()) return;
                    SetStage(ClientStage.WaitingForBattle, "loading started; waiting for battle entry");
                    return;

                case ClientStage.WaitingForBattle:
                    if (_controller!.CurrentState == MultiplayerRoomFlowState.Failed)
                    {
                        throw new InvalidOperationException("Room flow failed: " + _controller.LastError);
                    }
                    if (_flow!.CurrentBattlePhase != MobaBattleState.InMatch ||
                        !entry.TryGet(out BattleContext context) ||
                        context.LastFrame <= 0 ||
                        !TryGetOwnerRuntimePosition(context, out _ownerBaselinePosition, out _))
                    {
                        return;
                    }

                    _hasOwnerBaseline = true;
                    _battleBaselineFrame = context.LastFrame;
                    SetStage(ClientStage.BattleReady, "battle context and both runtime player actors are observable");
                    return;

                case ClientStage.BattleReady:
                    if (!File.Exists(_options!.MovementSignalPath)) return;
                    var movementContext = RequireBattleContext(entry);
                    if (movementContext.Plan.Sync.SyncMode != BattleSyncMode.Lockstep)
                    {
                        throw new InvalidOperationException(
                            $"FrameSync headless probe requires Lockstep, got {movementContext.Plan.Sync.SyncMode}.");
                    }
                    _movementStartedUtc = DateTime.UtcNow;
                    movementContext.BeginHudMove();
                    movementContext.SetHudMove(
                        _options.Role == ClientRole.Owner ? 1f : -1f,
                        0.25f);
                    SetStage(
                        ClientStage.MovingPlayers,
                        _options.Role == ClientRole.Owner
                            ? "moving owner toward the collision lane"
                            : "moving member toward the collision lane");
                    return;

                case ClientStage.MovingPlayers:
                    if (!_movementStopped && DateTime.UtcNow - _movementStartedUtc >= MovementInputDuration)
                    {
                        var stopContext = RequireBattleContext(entry);
                        stopContext.SetHudMove(0f, 0f);
                        stopContext.EndHudMove();
                        _movementStopped = true;
                        SetStage(ClientStage.ObservingMovement, "movement stopped; waiting for synchronized settle");
                    }
                    return;

                case ClientStage.ObservingMovement:
                    var wait = _options!.Role == ClientRole.Owner
                        ? TimeSpan.FromSeconds(4)
                        : TimeSpan.FromSeconds(5);
                    if (DateTime.UtcNow - _movementStartedUtc < wait) return;
                    ValidateSynchronizedMovement(RequireBattleContext(entry));
                    _movementValidated = true;
                    File.WriteAllText(GetOwnObservationSignalPath(), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    SetStage(ClientStage.WaitingForPeerObservation, "movement validated; waiting for peer observation");
                    return;

                case ClientStage.WaitingForPeerObservation:
                    if (!_movementValidated)
                    {
                        throw new InvalidOperationException("Movement observation barrier entered before local validation.");
                    }
                    if (!File.Exists(_options!.OwnerObservedSignalPath) ||
                        !File.Exists(_options.MemberObservedSignalPath))
                    {
                        return;
                    }
                    SetStage(ClientStage.SkillReady, "movement synchronized; waiting for skill probe barrier");
                    return;

                case ClientStage.SkillReady:
                    if (!File.Exists(_options!.SkillSignalPath)) return;
                    var skillContext = RequireBattleContext(entry);
                    if (!TryGetOwnerPosition(skillContext, out _skillBaselinePosition, out _) ||
                        !TryGetOwnerRuntimePosition(skillContext, out var skillOwnerRuntimePosition, out _))
                    {
                        throw new InvalidOperationException("Owner position is unavailable before skill probe.");
                    }
                    if (!TryGetSkillTargetState(
                            skillContext,
                            out _skillTargetActorId,
                            out _skillTargetBaselineHp,
                            out _skillTargetBaselineRuntimePosition,
                            out _skillTargetBaselinePresentedPosition,
                            out var hasPresentedTarget))
                    {
                        throw new InvalidOperationException("Member target state is unavailable before skill probe.");
                    }
                    if (!hasPresentedTarget)
                    {
                        throw new InvalidOperationException("Member target presentation is unavailable before skill probe.");
                    }
                    _skillTargetMinimumHp = _skillTargetBaselineHp;
                    _skillTargetMaxRuntimeY = _skillTargetBaselineRuntimePosition.y;
                    _skillTargetMaxPresentedY = _skillTargetBaselinePresentedPosition.y;
                    _skillTargetPresentedSampleObserved = true;
                    _skillStartedUtc = DateTime.UtcNow;
                    if (_options.Role == ClientRole.Owner)
                    {
                        var skillAimOffset = _skillTargetBaselineRuntimePosition - skillOwnerRuntimePosition;
                        skillContext.SubmitHudSkillAim(
                            slot: 1,
                            aimDx: skillAimOffset.x,
                            aimDz: skillAimOffset.z);
                        SetStage(ClientStage.CastingSkill, "submitting owner skill 1 input toward member runtime position");
                    }
                    else
                    {
                        SetStage(ClientStage.ObservingSkill, "observing owner skill 1 from member client");
                    }
                    return;

                case ClientStage.CastingSkill:
                case ClientStage.ObservingSkill:
                    ValidateSynchronizedSkill(entry);
                    return;

                case ClientStage.WaitingForSkillSettle:
                    if (!ObserveSkillSettle(RequireBattleContext(entry))) return;
                    Finish(
                        true,
                        "Formal two-client MOBA flow, movement, skill synchronization, and final convergence passed.");
                    return;

                case ClientStage.Passed:
                case ClientStage.Failed:
                    return;

                default:
                    throw new InvalidOperationException("Unsupported client stage: " + _stage);
            }
        }

        private static void TickStarterLaunch()
        {
            if (_starterOperation == null)
            {
                _starterController = Object.FindObjectOfType<StarterController>();
                if (_starterController == null)
                {
                    SetStage(ClientStage.WaitingForStarter, "waiting for StarterController");
                    return;
                }

                _starterOperation = _starterController.LaunchMobaAutomatedAsync(
                    _options!.AccountId,
                    _options.Host,
                    _options.Port,
                    _options.Region,
                    _options.ServerId,
                    TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                    suppressAutomaticLobbyActions: true);
                SetStage(ClientStage.StartingFromStarter, "Starter is logging in and launching MOBA");
                return;
            }

            if (!_starterOperation.IsCompleted) return;
            if (_starterOperation.IsCanceled)
            {
                throw new OperationCanceledException("Automated Starter launch was canceled.");
            }
            if (_starterOperation.IsFaulted)
            {
                throw _starterOperation.Exception?.GetBaseException() ??
                      new InvalidOperationException("Automated Starter launch failed.");
            }
            if (_starterController == null || string.IsNullOrWhiteSpace(_starterController.SessionToken))
            {
                throw new InvalidOperationException("Starter launch completed without an authenticated session.");
            }

            _options!.AccountId = _starterController.AuthenticatedAccountId;
            _options.SessionToken = _starterController.SessionToken;
            SessionState.SetString(OptionsKey, JsonConvert.SerializeObject(_options));
            _starterOperation = null;
            _starterController = null;
            SetStage(ClientStage.WaitingForEntry, "Starter launched MOBA; waiting for GameEntry");
        }

        private static void BeginRoomOperation()
        {
            if (_options!.Role == ClientRole.Owner)
            {
                StartOperation(
                    CreateAndPrepareAsync(),
                    ClientStage.CreatingRoom,
                    "creating and preparing authoritative room");
                return;
            }

            SetStage(ClientStage.WaitingForRoom, "waiting for owner room coordination");
        }

        private static async Task CreateAndPrepareAsync()
        {
            await _controller!.StartCreateRoomAsync(BuildLaunchSpec(), _lifetime!.Token);
            await PrepareDefaultLoadoutAsync();
        }

        private static async Task JoinAndPrepareAsync(string roomId)
        {
            await _controller!.StartJoinRoomAsync(BuildLaunchSpec(), roomId, _lifetime!.Token);
            await PrepareDefaultLoadoutAsync();
        }

        private static async Task PrepareDefaultLoadoutAsync()
        {
            var loadout = FormalLobbyFeature.ResolveAvailableDefaultLoadout(
                _gatewayConfig!.BuildDefaultLoadout(),
                _controller!.CurrentSnapshot,
                _controller.LocalPlayerId);
            await _controller.PickHeroAsync(loadout, _lifetime!.Token);
            await _controller.SetReadyAsync(true, _lifetime.Token);
        }

        private static MultiplayerRoomLaunchSpec BuildLaunchSpec()
        {
            if (_gatewayConfig == null || _options == null)
            {
                throw new InvalidOperationException("Gateway configuration is unavailable.");
            }

            var spec = _gatewayConfig.BuildRoomLaunchSpec(
                _options.SessionToken,
                _options.Region,
                _options.ServerId);
            spec.AccountId = _options.AccountId;
            spec.SyncTemplateId = _options.SyncTemplateId;
            spec.SyncModel = _options.SyncModel;
            return spec;
        }

        private static void ObserveSoloLobbyBeforePublication()
        {
            var snapshot = _controller?.CurrentSnapshot;
            if (_controller?.CurrentState == MultiplayerRoomFlowState.Failed)
            {
                throw new InvalidOperationException("Room flow failed: " + _controller.LastError);
            }
            if (_controller?.CurrentState != MultiplayerRoomFlowState.InLobby ||
                snapshot == null ||
                snapshot.Phase != MultiplayerRoomPhase.Lobby)
            {
                throw new InvalidOperationException(
                    "Owner left the lobby before a second player joined. " + BuildDiagnostic());
            }
            if (snapshot.Players == null || snapshot.Players.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one player before room publication, got {snapshot.Players?.Count ?? 0}.");
            }
            if (snapshot.CanStart ||
                !string.IsNullOrWhiteSpace(snapshot.BattleId) ||
                snapshot.WorldId != 0UL)
            {
                throw new InvalidOperationException(
                    "A one-player room became startable or acquired battle identity. " + BuildDiagnostic());
            }

            if (_soloLobbyObservationStartedUtc == default)
            {
                _soloLobbyObservationStartedUtc = DateTime.UtcNow;
                SetStage(
                    ClientStage.ObservingSoloLobby,
                    "one-player lobby is not startable; observing stability");
                return;
            }

            if (DateTime.UtcNow - _soloLobbyObservationStartedUtc < SoloLobbyObservationDuration)
            {
                return;
            }

            _soloLobbyVerified = true;
            WriteRoomCoordination(snapshot.RoomId);
            SetStage(
                ClientStage.WaitingAllReady,
                "single-player lobby verified; room published for second client");
        }

        private static void StartOperation(Task operation, ClientStage stage, string detail)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            SetStage(stage, detail);
        }

        private static bool CompleteOperation()
        {
            if (_operation == null) throw new InvalidOperationException("Room operation was not started.");
            if (!_operation.IsCompleted) return false;
            if (_operation.IsCanceled) throw new OperationCanceledException("Room operation was canceled.");
            if (_operation.IsFaulted)
            {
                throw _operation.Exception?.GetBaseException() ??
                      new InvalidOperationException("Room operation failed.");
            }

            _operation = null;
            return true;
        }

        private static BattleContext RequireBattleContext(GameEntry entry)
        {
            if (!entry.TryGet(out BattleContext context) || context == null)
            {
                throw new InvalidOperationException("Battle context is unavailable.");
            }
            return context;
        }

        private static void ValidateSynchronizedMovement(BattleContext context)
        {
            if (!_hasOwnerBaseline)
            {
                throw new InvalidOperationException("Owner movement baseline was not captured.");
            }
            if (context.LastFrame - _battleBaselineFrame < 10)
            {
                throw new InvalidOperationException(
                    $"Battle frame did not advance enough: baseline={_battleBaselineFrame}, current={context.LastFrame}.");
            }
            if (!TryGetOwnerRuntimePosition(context, out var current, out var actorCount))
            {
                throw new InvalidOperationException("Owner actor runtime position is unavailable after movement.");
            }
            if (actorCount < 2)
            {
                throw new InvalidOperationException("Both player actors are not present in the runtime world.");
            }

            var displacement = Vector3.Distance(_ownerBaselinePosition, current);
            if (displacement < 0.20f)
            {
                throw new InvalidOperationException(
                    $"Owner movement was not observed. displacement={displacement.ToString("F3", CultureInfo.InvariantCulture)}.");
            }
            if (_maxConsecutiveBackwardFrames >= 3)
            {
                throw new InvalidOperationException(
                    $"Owner movement repeatedly regressed during synchronization. " +
                    $"maxBackward={_maxBackwardMovement.ToString("F3", CultureInfo.InvariantCulture)}, " +
                    $"consecutiveFrames={_maxConsecutiveBackwardFrames}.");
            }

            var prediction = context.PredictionStats;
            if (prediction != null &&
                (prediction.TotalRollbackRestoreFailed > 0 ||
                 prediction.TotalReplayTimeout > 0 ||
                 prediction.TotalLocalDelayQueueDroppedBatches > 0))
            {
                throw new InvalidOperationException(
                    $"Prediction recovery was not lossless. " +
                    $"restoreFailed={prediction.TotalRollbackRestoreFailed}, " +
                    $"replayTimeout={prediction.TotalReplayTimeout}, " +
                    $"droppedInputBatches={prediction.TotalLocalDelayQueueDroppedBatches}.");
            }
        }

        private static void ObserveMovementTrajectory(BattleContext context)
        {
            if (!_hasOwnerBaseline ||
                context.LastFrame <= _movementLastObservedFrame ||
                !TryGetOwnerRuntimePosition(context, out var current, out _))
            {
                return;
            }

            var direction = new Vector3(1f, 0f, 0.25f).normalized;
            var progress = Vector3.Dot(current - _ownerBaselinePosition, direction);
            if (_hasMovementProgress)
            {
                var backward = _lastMovementProgress - progress;
                if (backward > _maxBackwardMovement) _maxBackwardMovement = backward;
                _consecutiveBackwardFrames = backward > 0.15f
                    ? _consecutiveBackwardFrames + 1
                    : 0;
                if (_consecutiveBackwardFrames > _maxConsecutiveBackwardFrames)
                {
                    _maxConsecutiveBackwardFrames = _consecutiveBackwardFrames;
                }
            }

            _lastMovementProgress = progress;
            _movementLastObservedFrame = context.LastFrame;
            _hasMovementProgress = true;
            _movementSampleCount++;
        }

        private static void ObserveSkillTrajectory(BattleContext context)
        {
            if (TryGetOwnerPosition(context, out var current, out _))
            {
                var displacement = Vector3.Distance(current, _skillBaselinePosition);
                if (displacement > _maxSkillDisplacement) _maxSkillDisplacement = displacement;
            }

            if (_skillTargetActorId <= 0 ||
                !TryGetSkillTargetState(
                    context,
                    out var targetActorId,
                    out var hp,
                    out var runtimePosition,
                    out var presentedPosition,
                    out var hasPresentedPosition) ||
                targetActorId != _skillTargetActorId)
            {
                return;
            }

            if (hp < _skillTargetMinimumHp) _skillTargetMinimumHp = hp;
            if (runtimePosition.y > _skillTargetMaxRuntimeY) _skillTargetMaxRuntimeY = runtimePosition.y;
            if (hasPresentedPosition)
            {
                _skillTargetPresentedSampleObserved = true;
                if (presentedPosition.y > _skillTargetMaxPresentedY) _skillTargetMaxPresentedY = presentedPosition.y;
            }

            _skillTargetRuntimeKnockupObserved |=
                _skillTargetMaxRuntimeY - _skillTargetBaselineRuntimePosition.y >= KnockupObservationThreshold;
            _skillTargetPresentedKnockupObserved |=
                _skillTargetMaxPresentedY - _skillTargetBaselinePresentedPosition.y >= KnockupObservationThreshold;
            if (_skillTargetRuntimeKnockupObserved &&
                _skillTargetPresentedKnockupObserved &&
                runtimePosition.y <= _skillTargetBaselineRuntimePosition.y + KnockupLandingEpsilon &&
                hasPresentedPosition &&
                presentedPosition.y <= _skillTargetBaselinePresentedPosition.y + KnockupLandingEpsilon)
            {
                _skillTargetLanded = true;
            }
        }

        private static void ValidateSynchronizedSkill(GameEntry entry)
        {
            if (_skillStartedUtc == default || DateTime.UtcNow - _skillStartedUtc < SkillObservationDuration)
            {
                return;
            }
            if (_maxSkillDisplacement < 0.15f)
            {
                throw new InvalidOperationException(
                    $"Owner skill 1 displacement was not observed. role={_options?.Role}, maxDisplacement={_maxSkillDisplacement:F3}.");
            }
            var damage = _skillTargetBaselineHp - _skillTargetMinimumHp;
            var runtimeRise = _skillTargetMaxRuntimeY - _skillTargetBaselineRuntimePosition.y;
            var presentedRise = _skillTargetMaxPresentedY - _skillTargetBaselinePresentedPosition.y;
            if (damage < DamageObservationThreshold ||
                !_skillTargetRuntimeKnockupObserved ||
                !_skillTargetPresentedKnockupObserved ||
                !_skillTargetPresentedSampleObserved ||
                !_skillTargetLanded)
            {
                throw new InvalidOperationException(
                    $"Owner skill 1 hit effects were not synchronized. role={_options?.Role}, " +
                    $"damage={damage:F3}, runtimeRise={runtimeRise:F3}, presentedRise={presentedRise:F3}, " +
                    $"presentedSample={_skillTargetPresentedSampleObserved}, landed={_skillTargetLanded}.");
            }
            if (_options?.Role == ClientRole.Owner)
            {
                var inputFeature = entry.Get<BattleInputFeature>();
                if (inputFeature.SkillSubmitAttemptCount <= 0 || inputFeature.SkillSubmitSuccessCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Owner skill input was not submitted. attempts={inputFeature.SkillSubmitAttemptCount}, successes={inputFeature.SkillSubmitSuccessCount}.");
                }
            }

            _skillValidated = true;
            _hasSkillLastPosition = false;
            _skillLastObservedFrame = 0;
            _skillStableFrameCount = 0;
            SetStage(
                ClientStage.WaitingForSkillSettle,
                "skill synchronized; waiting for predicted and presented movement to settle");
        }

        private static bool ObserveSkillSettle(BattleContext context)
        {
            if (context == null || context.LastFrame <= _skillLastObservedFrame)
            {
                return false;
            }
            if (!TryGetOwnerPositions(
                    context,
                    out var currentRuntime,
                    out var currentPresented,
                    out _))
            {
                _hasSkillLastPosition = false;
                _skillStableFrameCount = 0;
                _skillLastObservedFrame = context.LastFrame;
                return false;
            }

            if (_hasSkillLastPosition)
            {
                var presentedDisplacement = Vector3.Distance(_skillLastPosition, currentPresented);
                var runtimeDisplacement = Vector3.Distance(_skillLastRuntimePosition, currentRuntime);
                _skillStableFrameCount = presentedDisplacement <= SkillSettlePositionEpsilon &&
                                         runtimeDisplacement <= SkillSettlePositionEpsilon
                    ? _skillStableFrameCount + 1
                    : 0;
            }

            _skillLastPosition = currentPresented;
            _skillLastRuntimePosition = currentRuntime;
            _skillLastObservedFrame = context.LastFrame;
            _hasSkillLastPosition = true;

            var prediction = context.PredictionStats;
            return _skillStableFrameCount >= SkillSettleRequiredFrames &&
                   prediction?.IsReplaying != true;
        }

        private static string GetOwnObservationSignalPath()
        {
            if (_options == null)
            {
                throw new InvalidOperationException("Headless client options are unavailable.");
            }

            return _options.Role == ClientRole.Owner
                ? _options.OwnerObservedSignalPath
                : _options.MemberObservedSignalPath;
        }

        private static bool TryGetOwnerPosition(BattleContext context, out Vector3 position, out int actorCount)
        {
            return TryGetOwnerPositions(
                context,
                out _,
                out position,
                out actorCount);
        }

        private static bool TryGetOwnerRuntimePosition(
            BattleContext context,
            out Vector3 position,
            out int actorCount)
        {
            return TryGetOwnerPositions(
                context,
                out position,
                out _,
                out actorCount,
                requireMemberView: false);
        }

        private static bool TryGetOwnerPositions(
            BattleContext context,
            out Vector3 runtimePosition,
            out Vector3 presentedPosition,
            out int actorCount,
            bool requireMemberView = true)
        {
            runtimePosition = default;
            presentedPosition = default;
            actorCount = 0;
            var snapshot = _controller?.CurrentSnapshot;
            if (context == null || snapshot?.Players == null || snapshot.Players.Count < 2 ||
                !context.TryGetRuntimeWorld(out var world) || world.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var map) || map == null ||
                !world.Services.TryResolve<MobaEntityManager>(out var entities) || entities == null)
            {
                return false;
            }

            var foundOwner = false;
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var player = snapshot.Players[i];
                if (!map.TryGetActorId(new PlayerId(player.PlayerId.ToString(CultureInfo.InvariantCulture)), out var actorId) ||
                    actorId <= 0 ||
                    !entities.TryGetActorEntity(actorId, out var actor) ||
                    actor == null ||
                    !actor.hasTransform)
                {
                    continue;
                }

                actorCount++;
                if (!string.Equals(player.AccountId, snapshot.OwnerAccountId, StringComparison.Ordinal)) continue;
                var value = actor.transform.Value.Position;
                runtimePosition = new Vector3(value.X, value.Y, value.Z);
                presentedPosition = runtimePosition;
                if (requireMemberView &&
                    _options?.Role == ClientRole.Member &&
                    !TryGetViewPosition(context, actorId, out presentedPosition))
                {
                    return false;
                }
                foundOwner = true;
            }

            return foundOwner;
        }

        private static bool TryGetSkillTargetState(
            BattleContext context,
            out int actorId,
            out float hp,
            out Vector3 runtimePosition,
            out Vector3 presentedPosition,
            out bool hasPresentedPosition)
        {
            actorId = 0;
            hp = 0f;
            runtimePosition = default;
            presentedPosition = default;
            hasPresentedPosition = false;
            var snapshot = _controller?.CurrentSnapshot;
            if (context == null || snapshot?.Players == null ||
                !context.TryGetRuntimeWorld(out var world) || world.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var map) || map == null ||
                !world.Services.TryResolve<MobaEntityManager>(out var entities) || entities == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var player = snapshot.Players[i];
                if (string.Equals(player.AccountId, snapshot.OwnerAccountId, StringComparison.Ordinal) ||
                    !map.TryGetActorId(new PlayerId(player.PlayerId.ToString(CultureInfo.InvariantCulture)), out actorId) ||
                    actorId <= 0 ||
                    !entities.TryGetActorEntity(actorId, out var actor) || actor == null ||
                    !actor.hasTransform || !actor.hasAttributeGroup || !actor.hasResourceContainer)
                {
                    continue;
                }

                var value = actor.transform.Value.Position;
                runtimePosition = new Vector3(value.X, value.Y, value.Z);
                hp = actor.GetMobaAttrs().Hp;
                hasPresentedPosition = TryGetViewPosition(context, actorId, out presentedPosition);
                if (!hasPresentedPosition) presentedPosition = runtimePosition;
                return true;
            }

            actorId = 0;
            return false;
        }

        private static bool TryGetViewPosition(BattleContext context, int actorId, out Vector3 position)
        {
            position = default;
            if (context?.EntityWorld == null ||
                context.EntityLookup == null ||
                !context.EntityLookup.TryResolve(
                    context.EntityWorld,
                    new BattleNetId(actorId),
                    out var entity) ||
                !entity.TryGetRef(out BattleTransformComponent transform) ||
                transform == null)
            {
                return false;
            }

            position = transform.Position;
            return true;
        }

        private static void WriteRoomCoordination(string roomId)
        {
            var snapshot = _controller?.CurrentSnapshot;
            var coordination = new RoomCoordination
            {
                roomId = roomId,
                numericRoomId = snapshot?.NumericRoomId ?? 0UL,
                ownerAccountId = _options?.AccountId ?? string.Empty
            };
            File.WriteAllText(_options!.RoomPath, JsonConvert.SerializeObject(coordination));
        }

        private static bool TryReadRoomId(out string roomId)
        {
            roomId = string.Empty;
            if (!File.Exists(_options!.RoomPath)) return false;
            try
            {
                var coordination = JsonConvert.DeserializeObject<RoomCoordination>(
                    File.ReadAllText(_options.RoomPath));
                roomId = coordination?.roomId?.Trim() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(roomId);
            }
            catch (IOException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static void SetStage(ClientStage stage, string detail)
        {
            if (_stage == stage && string.Equals(_stageDetail, detail, StringComparison.Ordinal)) return;
            _stage = stage;
            _stageDetail = detail ?? string.Empty;
            WriteState(force: true);
        }

        private static ClientState CaptureState()
        {
            var state = new ClientState
            {
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                role = _options?.Role.ToString().ToLowerInvariant() ?? string.Empty,
                accountId = _options?.AccountId ?? string.Empty,
                stage = _stage.ToString(),
                detail = _stageDetail,
                success = _stage == ClientStage.Passed,
                roomFlowState = _controller?.CurrentState.ToString() ?? string.Empty,
                roomError = _controller?.LastError ?? string.Empty,
                flowFaulted = _flow?.IsFaulted == true,
                rootPhase = _flow?.CurrentPhase.ToString() ?? string.Empty,
                battlePhase = _flow?.CurrentBattlePhase.ToString() ?? string.Empty
            };

            if (GameEntry.IsInitialized)
            {
                var entry = GameEntry.Instance;
                if (entry.TryGet(out LobbyBattleEntrySelection selection))
                {
                    state.selectionRemote = selection.IsRemoteSelected;
                }
                if (entry.TryGet(out IBattleAssetLeaseTransferSource transferSource))
                {
                    state.assetTransferSourceRegistered = true;
                    if (transferSource is MultiplayerBattleAssetLoader multiplayerLoader)
                    {
                        state.preloadedLeaseAvailable = multiplayerLoader.HasLease;
                    }
                }
                if (entry.TryGet(out BattleLoadingScreenFeature loadingFeature))
                {
                    var loading = loadingFeature.CurrentSnapshot;
                    state.assetLoading = loading.IsLoading;
                    state.assetLoadCompleted = loading.Completed;
                    state.assetLoadSuccess = loading.Success;
                    state.assetLoadError = loading.ErrorMessage ?? string.Empty;
                    state.currentAssetKey = loading.CurrentAssetKey ?? string.Empty;
                    state.assetLoadedCount = loading.LoadedCount;
                    state.assetTotalCount = loading.TotalCount;
                }
            }

            var snapshot = _controller?.CurrentSnapshot;
            if (snapshot != null)
            {
                state.roomId = snapshot.RoomId;
                state.numericRoomId = snapshot.NumericRoomId;
                state.battleId = snapshot.BattleId;
                state.worldId = snapshot.WorldId;
                state.roomPhase = snapshot.Phase.ToString();
                state.roomRevision = snapshot.RoomRevision;
                state.playerCount = snapshot.Players?.Count ?? 0;
                state.canStart = snapshot.CanStart;
                CaptureSyncCapabilities(snapshot.SyncCapabilities, state, storeSnapshot: false);
            }
            state.soloLobbyVerified = _soloLobbyVerified;
            state.gatewayConnectionState = _gatewayDiagnostics?.ConnectionState.ToString() ?? "Unavailable";
            state.roomOperationActive = _operation != null;
            state.roomOperationStatus = _operation == null
                ? "None"
                : _operation.Status.ToString();
            state.roomStoreStale = _roomStore?.IsStale == true;
            if (_roomStore?.Current != null)
            {
                state.roomEventSequence = _roomStore.Current.LastEventSequence;
                CaptureSyncCapabilities(_roomStore.Current.SyncCapabilities, state, storeSnapshot: true);
            }
            if (_pushSynchronizer != null)
            {
                state.roomPushCount = _pushSynchronizer.HandledPushCount;
                state.roomPushAppliedCount = _pushSynchronizer.AppliedPushCount;
                state.roomPushLastRevision = _pushSynchronizer.LastPushRevision;
                state.roomPushLastUtc = _pushSynchronizer.LastPushUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
                state.roomRefreshFallbackCount = _pushSynchronizer.RefreshFallbackCount;
            }
            state.battleEntryCanEnter = MultiplayerBattleEntryGate.CanEnter(
                _controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                snapshot);

            var context = BattleFlowDebugProvider.Current;
            if (context != null)
            {
                state.frame = context.LastFrame;
                state.localActorId = context.LocalActorId;
                state.localPlayerId = context.ResolveLocalControlPlayerId();
                var prediction = context.PredictionStats;
                if (prediction != null)
                {
                    state.predictionRollbackCount = prediction.TotalRollbackCount;
                    state.predictionRollbackRestoreFailed = prediction.TotalRollbackRestoreFailed;
                    state.predictionReplayTimeoutCount = prediction.TotalReplayTimeout;
                    state.predictionMismatchCount = prediction.TotalReconcileMismatch;
                    state.predictionDroppedLocalInputBatches = prediction.TotalLocalDelayQueueDroppedBatches;
                    state.predictionReplaying = prediction.IsReplaying;
                    if (prediction.TryGetFrames(
                            new WorldId(context.Plan.World.WorldId),
                            out var confirmed,
                            out var predicted))
                    {
                        state.confirmedFrame = confirmed.Value;
                        state.predictedFrame = predicted.Value;
                    }
                }
            }
            if (GameEntry.IsInitialized && GameEntry.Instance.TryGet(out BattleInputFeature inputFeature))
            {
                state.inputFeatureAttached = true;
                state.inputFeatureHasContext = inputFeature.HasContext;
                state.inputCanSubmit = inputFeature.CanSubmitGameplayInput;
                state.inputTickCount = inputFeature.TickCount;
                state.moveReadCount = inputFeature.MoveReadCount;
                state.moveSubmitAttemptCount = inputFeature.MoveSubmitAttemptCount;
                state.moveSubmitSuccessCount = inputFeature.MoveSubmitSuccessCount;
                state.skillSubmitAttemptCount = inputFeature.SkillSubmitAttemptCount;
                state.skillSubmitSuccessCount = inputFeature.SkillSubmitSuccessCount;
            }
            var inputSubmission = BattleFlowDebugProvider.InputSubmissionStats;
            if (inputSubmission != null)
            {
                state.inputResponseCompletedCount = inputSubmission.CompletedCount;
                state.inputResponseAcceptedCount = inputSubmission.AcceptedCount;
                state.inputResponseRejectedCount = inputSubmission.RejectedCount;
                state.inputResponseFailedCount = inputSubmission.FailedCount;
                state.inputResponseLastServerFrame = inputSubmission.LastServerFrame;
                state.inputResponseLastAcceptedFrame = inputSubmission.LastAcceptedFrame;
                state.inputResponseLastReasonCode = inputSubmission.LastReasonCode;
                state.inputResponseLastShouldResync = inputSubmission.LastShouldResync;
                state.inputResponseLastStatus = inputSubmission.LastStatus ?? string.Empty;
                state.inputResponseLastMessage = inputSubmission.LastMessage ?? string.Empty;
                state.inputResponseLastFailure = inputSubmission.LastFailure ?? string.Empty;
            }
            state.actors = CaptureActors(context, snapshot);
            state.syncMode = context?.Plan.Sync.SyncMode.ToString() ?? string.Empty;
            state.movementSampleCount = _movementSampleCount;
            state.maxBackwardMovement = _maxBackwardMovement;
            state.skillValidated = _skillValidated;
            state.maxSkillDisplacement = _maxSkillDisplacement;
            state.skillStableFrameCount = _skillStableFrameCount;
            state.skillTargetActorId = _skillTargetActorId;
            state.skillTargetBaselineHp = _skillTargetBaselineHp;
            state.skillTargetMinimumHp = _skillTargetMinimumHp;
            state.skillTargetDamage = _skillTargetBaselineHp - _skillTargetMinimumHp;
            state.skillTargetRuntimeRise = _skillTargetMaxRuntimeY - _skillTargetBaselineRuntimePosition.y;
            state.skillTargetPresentedRise = _skillTargetMaxPresentedY - _skillTargetBaselinePresentedPosition.y;
            state.skillTargetRuntimeKnockupObserved = _skillTargetRuntimeKnockupObserved;
            state.skillTargetPresentedKnockupObserved = _skillTargetPresentedKnockupObserved;
            state.skillTargetLanded = _skillTargetLanded;

            var authority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;
            if (authority != null && context?.PredictionStats == null)
            {
                state.confirmedFrame = authority.ConfirmedFrame;
                state.predictedFrame = authority.PredictedFrame;
            }

            return state;
        }

        private static void CaptureSyncCapabilities(
            RoomGatewayNetworkSyncCapabilities capabilities,
            ClientState state,
            bool storeSnapshot)
        {
            if (capabilities == null) return;

            if (storeSnapshot)
            {
                state.storeSyncCapabilityPresent = true;
                state.storeSyncCapabilityProfileName = capabilities.ProfileName;
                state.storeSyncCapabilityMetadataVersion = capabilities.MetadataVersion;
                return;
            }

            state.syncCapabilityPresent = true;
            state.syncCapabilityProfileName = capabilities.ProfileName;
            state.syncCapabilityMetadataVersion = capabilities.MetadataVersion;
            state.syncCapabilityClientPlayback = (int)capabilities.Capabilities.ClientPlayback;
            state.syncCapabilityInput = (int)capabilities.Capabilities.Input;
            state.syncCapabilitySnapshot = (int)capabilities.Capabilities.Snapshot;
        }

        private static List<ActorState> CaptureActors(
            BattleContext? context,
            MultiplayerRoomSnapshot? snapshot)
        {
            var result = new List<ActorState>();
            if (snapshot?.Players == null)
            {
                return result;
            }

            MobaPlayerActorMapService? map = null;
            MobaEntityManager? entities = null;
            if (context != null && context.TryGetRuntimeWorld(out var world) && world.Services != null)
            {
                world.Services.TryResolve(out map);
                world.Services.TryResolve(out entities);
            }

            foreach (var player in snapshot.Players)
            {
                var playerId = player.PlayerId.ToString(CultureInfo.InvariantCulture);
                var actorState = new ActorState
                {
                    accountId = player.AccountId,
                    playerId = playerId,
                    ready = player.LobbyReady,
                    online = player.IsOnline
                };
                if (map != null && entities != null && map.TryGetActorId(new PlayerId(playerId), out var actorId))
                {
                    actorState.actorId = actorId;
                    if (entities.TryGetActorEntity(actorId, out var actor) && actor != null && actor.hasTransform)
                    {
                        var value = actor.transform.Value.Position;
                        actorState.hasPosition = true;
                        actorState.x = value.X;
                        actorState.y = value.Y;
                        actorState.z = value.Z;
                        actorState.runtimeX = value.X;
                        actorState.runtimeY = value.Y;
                        actorState.runtimeZ = value.Z;
                        if (actor.hasMoveInput)
                        {
                            actorState.moveDx = actor.moveInput.Dx;
                            actorState.moveDz = actor.moveInput.Dz;
                        }
                        if (actorId != context.LocalActorId &&
                            TryGetViewPosition(context, actorId, out var viewPosition))
                        {
                            actorState.x = viewPosition.x;
                            actorState.y = viewPosition.y;
                            actorState.z = viewPosition.z;
                        }
                    }
                }
                result.Add(actorState);
            }
            return result;
        }

        private static void WriteState(bool force)
        {
            if (_options == null || string.IsNullOrWhiteSpace(_options.StatePath)) return;
            if (!force && DateTime.UtcNow < _nextStateWriteUtc) return;
            _nextStateWriteUtc = DateTime.UtcNow + StateWriteInterval;

            var json = JsonConvert.SerializeObject(CaptureState(), Formatting.Indented);
            File.WriteAllText(_options.StatePath, json);
            File.AppendAllText(_options.EventLogPath, JsonConvert.SerializeObject(CaptureState()) + Environment.NewLine);
            Debug.Log("[MobaMultiplayerHeadless] " + BuildDiagnostic());
        }

        private static string BuildDiagnostic()
        {
            var snapshot = _controller?.CurrentSnapshot;
            var context = BattleFlowDebugProvider.Current;
            return $"role={_options?.Role.ToString() ?? "n/a"},stage={_stage},detail={_stageDetail}," +
                   $"gateway={_gatewayDiagnostics?.ConnectionState.ToString() ?? "n/a"},operation={_operation?.Status.ToString() ?? "none"}," +
                   $"roomState={_controller?.CurrentState.ToString() ?? "n/a"},roomId={snapshot?.RoomId ?? "n/a"}," +
                   $"roomRevision={snapshot?.RoomRevision ?? 0L},eventSequence={_roomStore?.Current?.LastEventSequence ?? 0L}," +
                   $"storeStale={_roomStore?.IsStale == true},pushes={_pushSynchronizer?.HandledPushCount ?? 0L}," +
                   $"pushApplied={_pushSynchronizer?.AppliedPushCount ?? 0L},refreshFallbacks={_pushSynchronizer?.RefreshFallbackCount ?? 0L}," +
                   $"battleId={snapshot?.BattleId ?? "n/a"},worldId={snapshot?.WorldId ?? 0UL}," +
                   $"battlePhase={_flow?.CurrentBattlePhase.ToString() ?? "n/a"},frame={context?.LastFrame ?? 0}," +
                   $"roomError={_controller?.LastError ?? "n/a"}";
        }

        private static void Finish(bool success, string message)
        {
            if (_options == null) TryRestoreOptions();
            if (_options == null)
            {
                Debug.LogError("[MobaMultiplayerHeadless] " + message);
                EditorApplication.Exit(1);
                return;
            }

            try
            {
                _stage = success ? ClientStage.Passed : ClientStage.Failed;
                _stageDetail = message;
                WriteState(force: true);
                var result = new ClientResult
                {
                    success = success,
                    message = message,
                    state = CaptureState()
                };
                File.WriteAllText(
                    _options.ResultPath,
                    JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            catch (Exception writeException)
            {
                Debug.LogException(writeException);
                success = false;
            }
            finally
            {
                _lifetime?.Cancel();
                SessionState.EraseBool(RunningKey);
                SessionState.EraseString(OptionsKey);
                Debug.Log("[MobaMultiplayerHeadless] " + message);
                EditorApplication.update -= ContinueInPlayMode;
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static bool TryRestoreOptions()
        {
            var json = SessionState.GetString(OptionsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return false;
            _options = JsonConvert.DeserializeObject<ClientOptions>(json);
            if (_options == null) return false;
            if (_lifetime == null)
            {
                _lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            }
            if (_stage == default) _stage = ClientStage.WaitingForStarter;
            return true;
        }

        private static void ResetRuntime(ClientOptions options)
        {
            _options = options;
            _lifetime?.Dispose();
            _lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            _controller = null;
            _roomStore = null;
            _pushSynchronizer = null;
            _gatewayConfig = null;
            _flow = null;
            _gatewayDiagnostics = null;
            _operation = null;
            _starterOperation = null;
            _starterController = null;
            _stage = ClientStage.WaitingForStarter;
            _stageDetail = "opening Starter scene";
            _deadlineUtc = default;
            _gatewayConnectedUtc = default;
            _soloLobbyObservationStartedUtc = default;
            _movementStartedUtc = default;
            _skillStartedUtc = default;
            _nextStateWriteUtc = default;
            _battleBaselineFrame = 0;
            _ownerBaselinePosition = default;
            _hasOwnerBaseline = false;
            _movementStopped = false;
            _movementValidated = false;
            _hasMovementProgress = false;
            _lastMovementProgress = 0f;
            _maxBackwardMovement = 0f;
            _consecutiveBackwardFrames = 0;
            _maxConsecutiveBackwardFrames = 0;
            _movementLastObservedFrame = 0;
            _movementSampleCount = 0;
            _soloLobbyVerified = false;
            _skillBaselinePosition = default;
            _maxSkillDisplacement = 0f;
            _skillValidated = false;
            _skillTargetActorId = 0;
            _skillTargetBaselineHp = 0f;
            _skillTargetMinimumHp = 0f;
            _skillTargetBaselineRuntimePosition = default;
            _skillTargetBaselinePresentedPosition = default;
            _skillTargetMaxRuntimeY = 0f;
            _skillTargetMaxPresentedY = 0f;
            _skillTargetRuntimeKnockupObserved = false;
            _skillTargetPresentedKnockupObserved = false;
            _skillTargetLanded = false;
            _skillTargetPresentedSampleObserved = false;
            _skillLastPosition = default;
            _skillLastRuntimePosition = default;
            _skillLastObservedFrame = 0;
            _skillStableFrameCount = 0;
            _hasSkillLastPosition = false;
        }

        private enum ClientStage
        {
            WaitingForStarter = 0,
            StartingFromStarter = 1,
            WaitingForEntry = 2,
            WaitingForGateway = 3,
            WaitingForRoom = 4,
            CreatingRoom = 5,
            JoiningRoom = 6,
            ObservingSoloLobby = 7,
            WaitingAllReady = 8,
            StartingMatch = 9,
            WaitingForBattle = 10,
            BattleReady = 11,
            MovingPlayers = 12,
            ObservingMovement = 13,
            WaitingForPeerObservation = 14,
            SkillReady = 15,
            CastingSkill = 16,
            ObservingSkill = 17,
            WaitingForSkillSettle = 18,
            Passed = 19,
            Failed = 20
        }

        private enum ClientRole
        {
            Owner,
            Member
        }

        [Serializable]
        private sealed class ClientOptions
        {
            public ClientRole Role;
            public string Host = "127.0.0.1";
            public int Port = 4000;
            public string Region = "dev";
            public string ServerId = "local";
            public string SyncTemplateId = "frame-sync-authority";
            public int SyncModel = 1;
            public string AccountId = string.Empty;
            public string SessionToken = string.Empty;
            public string RoomPath = string.Empty;
            public string MovementSignalPath = string.Empty;
            public string OwnerObservedSignalPath = string.Empty;
            public string MemberObservedSignalPath = string.Empty;
            public string SkillSignalPath = string.Empty;
            public string StatePath = string.Empty;
            public string EventLogPath = string.Empty;
            public string ResultPath = string.Empty;
            public int TimeoutSeconds = 180;
            public int RequestTimeoutSeconds = 10;

            public static ClientOptions Parse(string[] args)
            {
                var roleText = Required(args, "-mobaHeadlessRole");
                if (!Enum.TryParse(roleText, ignoreCase: true, out ClientRole role))
                {
                    throw new ArgumentException("-mobaHeadlessRole must be owner or member.");
                }

                var options = new ClientOptions
                {
                    Role = role,
                    Host = Value(args, "-gatewayHost") ?? "127.0.0.1",
                    Port = IntValue(args, "-gatewayPort", 4000),
                    Region = Value(args, "-gatewayRegion") ?? "dev",
                    ServerId = Value(args, "-gatewayServerId") ?? "local",
                    SyncTemplateId = Value(args, "-mobaHeadlessSyncTemplate") ?? "frame-sync-authority",
                    SyncModel = IntValue(args, "-mobaHeadlessSyncModel", 1),
                    AccountId = Required(args, "-mobaHeadlessAccount"),
                    RoomPath = FullPath(Required(args, "-mobaHeadlessRoomPath")),
                    MovementSignalPath = FullPath(Required(args, "-mobaHeadlessMovementSignal")),
                    OwnerObservedSignalPath = FullPath(Required(args, "-mobaHeadlessOwnerObservedSignal")),
                    MemberObservedSignalPath = FullPath(Required(args, "-mobaHeadlessMemberObservedSignal")),
                    SkillSignalPath = FullPath(Required(args, "-mobaHeadlessSkillSignal")),
                    StatePath = FullPath(Required(args, "-mobaHeadlessState")),
                    EventLogPath = FullPath(Required(args, "-mobaHeadlessEvents")),
                    ResultPath = FullPath(Required(args, "-mobaHeadlessResult")),
                    TimeoutSeconds = IntValue(args, "-mobaHeadlessTimeoutSeconds", 180),
                    RequestTimeoutSeconds = IntValue(args, "-mobaHeadlessRequestTimeoutSeconds", 10)
                };

                if (options.Port <= 0 || options.Port > 65535) throw new ArgumentOutOfRangeException(nameof(options.Port));
                if (string.IsNullOrWhiteSpace(options.SyncTemplateId)) throw new ArgumentException("Sync template is required.");
                if (options.SyncModel <= 0) throw new ArgumentOutOfRangeException(nameof(options.SyncModel));
                if (options.TimeoutSeconds < 30) throw new ArgumentOutOfRangeException(nameof(options.TimeoutSeconds));
                options.SyncTemplateId = options.SyncTemplateId.Trim();
                return options;
            }

            private static string Required(string[] args, string name) =>
                Value(args, name) ?? throw new ArgumentException(name + " is required.");

            private static string? Value(string[] args, string name)
            {
                for (var i = 0; i + 1 < args.Length; i++)
                {
                    if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
                }
                return null;
            }

            private static int IntValue(string[] args, string name, int fallback) =>
                int.TryParse(Value(args, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : fallback;

            private static string FullPath(string value) => Path.GetFullPath(value);
        }

        [Serializable]
        private sealed class RoomCoordination
        {
            public string roomId = string.Empty;
            public ulong numericRoomId;
            public string ownerAccountId = string.Empty;
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
            public string timestampUtc = string.Empty;
            public string role = string.Empty;
            public string accountId = string.Empty;
            public string stage = string.Empty;
            public string detail = string.Empty;
            public bool success;
            public string gatewayConnectionState = string.Empty;
            public bool roomOperationActive;
            public string roomOperationStatus = string.Empty;
            public string roomFlowState = string.Empty;
            public string roomError = string.Empty;
            public bool flowFaulted;
            public bool selectionRemote;
            public bool battleEntryCanEnter;
            public bool assetTransferSourceRegistered;
            public bool preloadedLeaseAvailable;
            public bool assetLoading;
            public bool assetLoadCompleted;
            public bool assetLoadSuccess;
            public string assetLoadError = string.Empty;
            public string currentAssetKey = string.Empty;
            public int assetLoadedCount;
            public int assetTotalCount;
            public string roomId = string.Empty;
            public ulong numericRoomId;
            public string roomPhase = string.Empty;
            public long roomRevision;
            public long roomEventSequence;
            public bool roomStoreStale;
            public long roomPushCount;
            public long roomPushAppliedCount;
            public long roomPushLastRevision;
            public string roomPushLastUtc = string.Empty;
            public long roomRefreshFallbackCount;
            public string battleId = string.Empty;
            public ulong worldId;
            public int playerCount;
            public bool canStart;
            public bool soloLobbyVerified;
            public string rootPhase = string.Empty;
            public string battlePhase = string.Empty;
            public string syncMode = string.Empty;
            public bool syncCapabilityPresent;
            public int syncCapabilityMetadataVersion;
            public string syncCapabilityProfileName = string.Empty;
            public int syncCapabilityClientPlayback;
            public int syncCapabilityInput;
            public int syncCapabilitySnapshot;
            public bool storeSyncCapabilityPresent;
            public int storeSyncCapabilityMetadataVersion;
            public string storeSyncCapabilityProfileName = string.Empty;
            public int frame;
            public int confirmedFrame;
            public int predictedFrame;
            public string localPlayerId = string.Empty;
            public int localActorId;
            public bool inputFeatureAttached;
            public bool inputFeatureHasContext;
            public bool inputCanSubmit;
            public int inputTickCount;
            public int moveReadCount;
            public int moveSubmitAttemptCount;
            public int moveSubmitSuccessCount;
            public int skillSubmitAttemptCount;
            public int skillSubmitSuccessCount;
            public int inputResponseCompletedCount;
            public int inputResponseAcceptedCount;
            public int inputResponseRejectedCount;
            public int inputResponseFailedCount;
            public int inputResponseLastServerFrame;
            public int inputResponseLastAcceptedFrame;
            public int inputResponseLastReasonCode;
            public bool inputResponseLastShouldResync;
            public string inputResponseLastStatus = string.Empty;
            public string inputResponseLastMessage = string.Empty;
            public string inputResponseLastFailure = string.Empty;
            public long predictionRollbackCount;
            public long predictionRollbackRestoreFailed;
            public long predictionReplayTimeoutCount;
            public long predictionMismatchCount;
            public long predictionDroppedLocalInputBatches;
            public bool predictionReplaying;
            public int movementSampleCount;
            public float maxBackwardMovement;
            public bool skillValidated;
            public float maxSkillDisplacement;
            public int skillStableFrameCount;
            public int skillTargetActorId;
            public float skillTargetBaselineHp;
            public float skillTargetMinimumHp;
            public float skillTargetDamage;
            public float skillTargetRuntimeRise;
            public float skillTargetPresentedRise;
            public bool skillTargetRuntimeKnockupObserved;
            public bool skillTargetPresentedKnockupObserved;
            public bool skillTargetLanded;
            public List<ActorState> actors = new List<ActorState>();
        }

        [Serializable]
        private sealed class ActorState
        {
            public string accountId = string.Empty;
            public string playerId = string.Empty;
            public int actorId;
            public bool ready;
            public bool online;
            public bool hasPosition;
            public float x;
            public float y;
            public float z;
            public float runtimeX;
            public float runtimeY;
            public float runtimeZ;
            public float moveDx;
            public float moveDz;
        }
    }
}
