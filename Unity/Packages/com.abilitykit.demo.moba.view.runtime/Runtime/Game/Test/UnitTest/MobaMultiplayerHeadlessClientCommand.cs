#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string ScenePath = "Assets/Scenes/MobaMultiplayerScene.unity";
        private const string RunningKey = "AbilityKit.MobaMultiplayerHeadless.Running";
        private const string OptionsKey = "AbilityKit.MobaMultiplayerHeadless.Options";
        private static readonly TimeSpan StateWriteInterval = TimeSpan.FromSeconds(1);

        private static ClientOptions? _options;
        private static CancellationTokenSource? _lifetime;
        private static MultiplayerRoomFlowController? _controller;
        private static GatewayMultiplayerRoomSession? _roomSession;
        private static BattleGatewayConfigSO? _gatewayConfig;
        private static GameFlowDomain? _flow;
        private static Task? _operation;
        private static ClientStage _stage;
        private static string _stageDetail = string.Empty;
        private static DateTime _deadlineUtc;
        private static DateTime _gatewayConnectedUtc;
        private static DateTime _movementStartedUtc;
        private static DateTime _nextStateWriteUtc;
        private static DateTime _nextRoomRefreshUtc;
        private static int _battleBaselineFrame;
        private static Vector3 _ownerBaselinePosition;
        private static bool _hasOwnerBaseline;
        private static bool _movementStopped;
        private static bool _movementValidated;

        static MobaMultiplayerHeadlessClientCommand()
        {
            EditorApplication.update -= ContinueInPlayMode;
            EditorApplication.update += ContinueInPlayMode;

            // Domain reload happens between executeMethod and scene Awake. Re-publish the
            // one-shot launch intent here so GameEntry consumes the authenticated request.
            if (SessionState.GetBool(RunningKey, false))
            {
                TryRestoreOptions();
                PublishLaunchIntent();
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

                var login = DemoRoomGatewayAccountClient.LoginTcpAsync(
                        options.Host,
                        options.Port,
                        options.AccountId,
                        TimeSpan.FromSeconds(options.RequestTimeoutSeconds))
                    .GetAwaiter()
                    .GetResult();
                if (!login.Success || string.IsNullOrWhiteSpace(login.SessionToken))
                {
                    throw new InvalidOperationException("Account login failed: " + login.Message);
                }

                options.AccountId = login.AccountId;
                options.SessionToken = login.SessionToken;
                SessionState.SetBool(RunningKey, true);
                SessionState.SetString(OptionsKey, JsonConvert.SerializeObject(options));
                ResetRuntime(options);
                PublishLaunchIntent();

                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException("MOBA multiplayer scene could not be opened.");
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
            if (!GameEntry.IsInitialized)
            {
                SetStage(ClientStage.WaitingForEntry, "waiting for GameEntry");
                return;
            }

            var entry = GameEntry.Instance;
            _controller ??= entry.Get<MultiplayerRoomFlowController>();
            _roomSession ??= entry.Get<GatewayMultiplayerRoomSession>();
            _gatewayConfig ??= entry.Get<BattleGatewayConfigSO>();
            _flow ??= entry.Get<GameFlowDomain>();
            var gateway = entry.Get<IMultiplayerGatewayRuntime>();

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
                            _controller!.StartJoinRoomAsync(BuildLaunchSpec(), roomId, _lifetime!.Token),
                            ClientStage.JoiningRoom,
                            "joining " + roomId);
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
                        WriteRoomCoordination(_controller.CurrentRoomId);
                    }
                    StartOperation(
                        _controller.PickHeroAsync(BuildRoleLoadout(), _lifetime!.Token),
                        ClientStage.PreparingLoadout,
                        "configuring authoritative player loadout");
                    return;

                case ClientStage.PreparingLoadout:
                    if (!CompleteOperation()) return;
                    StartOperation(
                        _controller!.SetReadyAsync(true, _lifetime!.Token),
                        ClientStage.SettingReady,
                        "setting authoritative ready state");
                    return;

                case ClientStage.SettingReady:
                    if (!CompleteOperation()) return;
                    SetStage(ClientStage.WaitingAllReady, "waiting for both authoritative players to be ready");
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
                        RefreshCurrentRoomIfDue();
                        return;
                    }
                    if (_options!.Role == ClientRole.Member)
                    {
                        SetStage(
                            ClientStage.WaitingAllReady,
                            "both players ready; waiting for room owner to start loading");
                        RefreshCurrentRoomIfDue();
                        return;
                    }
                    if (_controller.CurrentSnapshot?.CanStart == true)
                    {
                        StartOperation(
                            _controller.BeginLoadingAsync(_lifetime!.Token),
                            ClientStage.StartingMatch,
                            "owner starting authoritative loading barrier");
                    }
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
                        !TryGetOwnerPosition(context, out _ownerBaselinePosition, out _))
                    {
                        return;
                    }

                    _hasOwnerBaseline = true;
                    _battleBaselineFrame = context.LastFrame;
                    SetStage(ClientStage.BattleReady, "battle context and both player actors are observable");
                    return;

                case ClientStage.BattleReady:
                    if (!File.Exists(_options!.MovementSignalPath)) return;
                    _movementStartedUtc = DateTime.UtcNow;
                    if (_options.Role == ClientRole.Owner)
                    {
                        var movementContext = RequireBattleContext(entry);
                        movementContext.BeginHudMove();
                        movementContext.SetHudMove(1f, 0.25f);
                        SetStage(ClientStage.MovingOwner, "submitting owner movement input");
                    }
                    else
                    {
                        SetStage(ClientStage.ObservingMovement, "observing owner movement from member client");
                    }
                    return;

                case ClientStage.MovingOwner:
                    if (!_movementStopped && DateTime.UtcNow - _movementStartedUtc >= TimeSpan.FromSeconds(2))
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
                    Finish(true, "Formal two-client MOBA flow and synchronized movement passed.");
                    return;

                case ClientStage.Passed:
                case ClientStage.Failed:
                    return;

                default:
                    throw new InvalidOperationException("Unsupported client stage: " + _stage);
            }
        }

        private static void BeginRoomOperation()
        {
            if (_options!.Role == ClientRole.Owner)
            {
                StartOperation(
                    _controller!.StartCreateRoomAsync(BuildLaunchSpec(), _lifetime!.Token),
                    ClientStage.CreatingRoom,
                    "creating authoritative room");
                return;
            }

            SetStage(ClientStage.WaitingForRoom, "waiting for owner room coordination");
        }

        private static MultiplayerRoomLaunchSpec BuildLaunchSpec()
        {
            if (_gatewayConfig == null || _options == null)
            {
                throw new InvalidOperationException("Gateway configuration is unavailable.");
            }

            return _gatewayConfig.BuildRoomLaunchSpec(
                _options.SessionToken,
                _options.Region,
                _options.ServerId);
        }

        private static MultiplayerLoadoutSpec BuildRoleLoadout()
        {
            if (_gatewayConfig == null || _options == null)
            {
                throw new InvalidOperationException("Gateway configuration is unavailable.");
            }

            var configured = _gatewayConfig.BuildDefaultLoadout();
            return new MultiplayerLoadoutSpec(
                configured.HeroId,
                _options.Role == ClientRole.Owner ? 1 : 2,
                configured.SpawnPointId,
                configured.Level,
                configured.AttributeTemplateId,
                configured.BasicAttackSkillId,
                configured.SkillIds);
        }

        private static void StartOperation(Task operation, ClientStage stage, string detail)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            SetStage(stage, detail);
        }

        private static void RefreshCurrentRoomIfDue()
        {
            if (_operation != null ||
                _roomSession == null ||
                _controller == null ||
                string.IsNullOrWhiteSpace(_controller.CurrentRoomId) ||
                DateTime.UtcNow < _nextRoomRefreshUtc)
            {
                return;
            }

            _nextRoomRefreshUtc = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
            _operation = _roomSession.RefreshSnapshotAsync(
                _controller.CurrentRoomId,
                _lifetime!.Token);
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
            if (!TryGetOwnerPosition(context, out var current, out var actorCount))
            {
                throw new InvalidOperationException("Owner actor position is unavailable after movement.");
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
            position = default;
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
                position = new Vector3(value.X, value.Y, value.Z);
                foundOwner = true;
            }

            return foundOwner;
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
            }
            state.actors = CaptureActors(context, snapshot);

            var authority = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;
            if (authority != null)
            {
                state.confirmedFrame = authority.ConfirmedFrame;
                state.predictedFrame = authority.PredictedFrame;
            }

            return state;
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
                   $"roomState={_controller?.CurrentState.ToString() ?? "n/a"},roomId={snapshot?.RoomId ?? "n/a"}," +
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
            if (_stage == default) _stage = ClientStage.WaitingForEntry;
            return true;
        }

        private static void PublishLaunchIntent()
        {
            if (_options == null || string.IsNullOrWhiteSpace(_options.SessionToken)) return;
            DemoMultiplayerLaunchIntent.Request(
                DemoMultiplayerGameplay.Moba,
                new DemoMultiplayerLaunchRequest(
                    _options.Host,
                    _options.Port,
                    _options.Region,
                    _options.ServerId,
                    _options.AccountId,
                    _options.SessionToken,
                    TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                    suppressAutomaticLobbyActions: true));
        }

        private static void ResetRuntime(ClientOptions options)
        {
            _options = options;
            _lifetime?.Dispose();
            _lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            _controller = null;
            _roomSession = null;
            _gatewayConfig = null;
            _flow = null;
            _operation = null;
            _stage = ClientStage.WaitingForEntry;
            _stageDetail = "launching multiplayer scene";
            _deadlineUtc = default;
            _gatewayConnectedUtc = default;
            _movementStartedUtc = default;
            _nextStateWriteUtc = default;
            _nextRoomRefreshUtc = default;
            _battleBaselineFrame = 0;
            _ownerBaselinePosition = default;
            _hasOwnerBaseline = false;
            _movementStopped = false;
            _movementValidated = false;
        }

        private enum ClientStage
        {
            WaitingForEntry = 0,
            WaitingForGateway = 1,
            WaitingForRoom = 2,
            CreatingRoom = 3,
            JoiningRoom = 4,
            PreparingLoadout = 5,
            SettingReady = 6,
            WaitingAllReady = 7,
            StartingMatch = 8,
            WaitingForBattle = 9,
            BattleReady = 10,
            MovingOwner = 11,
            ObservingMovement = 12,
            WaitingForPeerObservation = 13,
            Passed = 14,
            Failed = 15
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
            public string AccountId = string.Empty;
            public string SessionToken = string.Empty;
            public string RoomPath = string.Empty;
            public string MovementSignalPath = string.Empty;
            public string OwnerObservedSignalPath = string.Empty;
            public string MemberObservedSignalPath = string.Empty;
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
                    AccountId = Required(args, "-mobaHeadlessAccount"),
                    RoomPath = FullPath(Required(args, "-mobaHeadlessRoomPath")),
                    MovementSignalPath = FullPath(Required(args, "-mobaHeadlessMovementSignal")),
                    OwnerObservedSignalPath = FullPath(Required(args, "-mobaHeadlessOwnerObservedSignal")),
                    MemberObservedSignalPath = FullPath(Required(args, "-mobaHeadlessMemberObservedSignal")),
                    StatePath = FullPath(Required(args, "-mobaHeadlessState")),
                    EventLogPath = FullPath(Required(args, "-mobaHeadlessEvents")),
                    ResultPath = FullPath(Required(args, "-mobaHeadlessResult")),
                    TimeoutSeconds = IntValue(args, "-mobaHeadlessTimeoutSeconds", 180),
                    RequestTimeoutSeconds = IntValue(args, "-mobaHeadlessRequestTimeoutSeconds", 10)
                };

                if (options.Port <= 0 || options.Port > 65535) throw new ArgumentOutOfRangeException(nameof(options.Port));
                if (options.TimeoutSeconds < 30) throw new ArgumentOutOfRangeException(nameof(options.TimeoutSeconds));
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
            public string battleId = string.Empty;
            public ulong worldId;
            public int playerCount;
            public string rootPhase = string.Empty;
            public string battlePhase = string.Empty;
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
        }
    }
}
