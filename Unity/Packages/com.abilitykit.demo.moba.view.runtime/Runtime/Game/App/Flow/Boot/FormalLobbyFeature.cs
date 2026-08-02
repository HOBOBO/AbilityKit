using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Production-style multiplayer lobby for the MOBA demo.
    /// The authoritative room controller owns state; this feature only coordinates entry and presentation.
    /// </summary>
    public sealed class FormalLobbyFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private const float WindowWidth = 460f;
        private const float WindowHeight = 570f;
        private const long RoomNoticeDurationMilliseconds = 6000L;

        private readonly MultiplayerBattleEntryGate _battleEntryGate = new MultiplayerBattleEntryGate();
        private readonly List<DemoRoomSummary> _rooms = new List<DemoRoomSummary>();

        private MultiplayerRoomFlowController _controller;
        private GatewayMultiplayerRoomSession _session;
        private LobbyBattleEntrySelection _selection;
        private IMultiplayerGatewayRuntime _gatewayRuntime;
        private IDemoRoomDirectoryClient _roomDirectory;
        private BattleGatewayConfigSO _gatewayConfig;
        private DemoMultiplayerLaunchRequest _launchRequest;
        private ClientRoomStore _roomStore;
        private CancellationTokenSource _lifetime;
        private Task _operationTask = Task.CompletedTask;
        private string _operationLabel = string.Empty;
        private string _operationError = string.Empty;
        private string _configurationError = string.Empty;
        private string _preparedRoomId = string.Empty;
        private string _automaticStartRoomId = string.Empty;
        private bool _initializationStarted;
        private bool _roomListLoaded;
        private bool _roomListBusy;
        private Vector2 _roomScroll;
        private string _roomNotice = string.Empty;
        private long _roomNoticeExpiresAtUnixMs;

        private bool IsOperationBusy => _operationTask != null && !_operationTask.IsCompleted;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _lifetime = new CancellationTokenSource();
            _controller = ResolveController(ctx);
            if (ctx.Entry != null)
            {
                ctx.Entry.TryGet(out _gatewayConfig);
                ctx.Entry.TryGet(out _session);
                ctx.Entry.TryGet(out _selection);
                ctx.Entry.TryGet(out _gatewayRuntime);
                ctx.Entry.TryGet(out _launchRequest);
                if (ctx.Entry.TryGet(out _roomStore))
                {
                    _roomStore.OnMembershipChanged += HandleMembershipChanged;
                }
                if (ctx.Entry.TryGet(out IGatewayRoomClient roomClient))
                {
                    _roomDirectory = roomClient as IDemoRoomDirectoryClient;
                }
            }

            if (!TryBuildLaunchSpec(
                    _gatewayConfig,
                    _launchRequest,
                    _session?.SessionToken,
                    out _,
                    out _configurationError))
            {
                return;
            }

            if (_controller == null)
            {
                _configurationError = "Multiplayer room flow is unavailable.";
            }
            else if (_roomDirectory == null)
            {
                _configurationError = "Room directory service is unavailable.";
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_roomStore != null)
            {
                _roomStore.OnMembershipChanged -= HandleMembershipChanged;
                _roomStore = null;
            }

            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _roomNotice = string.Empty;
            _roomNoticeExpiresAtUnixMs = 0L;
            _automaticStartRoomId = string.Empty;
            _battleEntryGate.Reset();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (!ShouldShowFlowWindow(_selection)) return;

            if (string.IsNullOrEmpty(_configurationError) &&
                !_initializationStarted &&
                _gatewayRuntime?.ConnectionState == ConnectionState.Connected)
            {
                _initializationStarted = true;
                StartOperation("Opening multiplayer lobby", InitializeLobbyAsync);
            }

            TryStartAutomaticPreparation();
            TryStartAutomaticMatch();

            if (!ShouldEnterBattle(_selection, _controller)) return;

            var snapshot = _controller.CurrentSnapshot;
            var flow = ctx.Entry?.Get<GameFlowDomain>();
            if (snapshot == null ||
                flow == null ||
                _session == null ||
                string.IsNullOrWhiteSpace(_session.SessionToken))
            {
                return;
            }
            if (!_battleEntryGate.TryAccept(_controller.CurrentState, snapshot)) return;

            try
            {
                var configured = new ConfiguredBattleBootstrapper(_selection.Config, _selection.Preset);
                flow.EnterBattle(new ExistingGatewayRoomBattleBootstrapper(
                    configured,
                    _session.SessionToken,
                    snapshot.RoomId,
                    snapshot.BattleId,
                    snapshot.NumericRoomId,
                    snapshot.WorldId,
                    _controller.LocalPlayerId,
                    _session,
                    _launchRequest,
                    snapshot.Players));
            }
            catch
            {
                _battleEntryGate.Reset();
                throw;
            }
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null || !ShouldShowFlowWindow(_selection)) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle) return;

            var width = Mathf.Min(WindowWidth, Mathf.Max(300f, Screen.width - 24f));
            var height = Mathf.Min(WindowHeight, Mathf.Max(360f, Screen.height - 24f));
            var x = Mathf.Max(12f, (Screen.width - width) * 0.5f);
            var y = Mathf.Max(12f, (Screen.height - height) * 0.5f);

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            DrawHeader();
            DrawConnectionStatus();

            if (!string.IsNullOrWhiteSpace(_configurationError))
            {
                GUILayout.Space(10f);
                GUILayout.Label("Multiplayer is not configured correctly.");
                GUILayout.Label(_configurationError);
                GUILayout.EndArea();
                return;
            }

            DrawErrors();
            DrawRoomNotice();
            GUILayout.Space(8f);
            DrawCurrentState();
            GUILayout.EndArea();
        }

        internal static bool ShouldShowFlowWindow(LobbyBattleEntrySelection selection)
        {
            return selection?.IsRemoteSelected == true;
        }

        internal static bool ShouldEnterBattle(
            LobbyBattleEntrySelection selection,
            MultiplayerRoomFlowController controller)
        {
            return selection?.IsRemoteSelected == true &&
                   controller != null &&
                   MultiplayerBattleEntryGate.CanEnter(
                       controller.CurrentState,
                       controller.CurrentSnapshot);
        }

        internal static bool TryBuildLaunchSpec(
            BattleGatewayConfigSO config,
            DemoMultiplayerLaunchRequest launchRequest,
            string activeSessionToken,
            out MultiplayerRoomLaunchSpec spec,
            out string error)
        {
            spec = null;
            if (config == null)
            {
                error = "BattleGatewayConfig asset is required.";
                return false;
            }
            if (!config.TryValidateFormalLobby(out error))
            {
                return false;
            }

            var sessionToken = !string.IsNullOrWhiteSpace(activeSessionToken)
                ? activeSessionToken.Trim()
                : launchRequest?.SessionToken?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                error = "An authenticated multiplayer session is required.";
                return false;
            }

            spec = config.BuildRoomLaunchSpec(
                sessionToken,
                launchRequest?.Region,
                launchRequest?.ServerId);
            error = string.Empty;
            return true;
        }

        internal static MultiplayerRoomPlayerSnapshot FindLocalPlayer(
            MultiplayerRoomSnapshot snapshot,
            uint localPlayerId,
            string accountId)
        {
            var players = snapshot?.Players;
            if (players == null) return null;

            if (localPlayerId != 0u)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (players[i].PlayerId == localPlayerId) return players[i];
                }
            }

            if (string.IsNullOrWhiteSpace(accountId)) return null;
            for (var i = 0; i < players.Count; i++)
            {
                if (string.Equals(players[i].AccountId, accountId, StringComparison.Ordinal))
                {
                    return players[i];
                }
            }

            return null;
        }

        internal static string FormatMembershipNotice(ClientRoomMembershipChange change)
        {
            if (change == null) return string.Empty;

            var messages = new List<string>();
            for (var i = 0; i < change.LeftAccountIds.Count; i++)
            {
                messages.Add(change.LeftAccountIds[i] + " left the room.");
            }
            for (var i = 0; i < change.JoinedAccountIds.Count; i++)
            {
                messages.Add(change.JoinedAccountIds[i] + " joined the room.");
            }
            if (change.OwnerChanged && !string.IsNullOrWhiteSpace(change.CurrentOwnerAccountId))
            {
                messages.Add(change.CurrentOwnerAccountId + " is now room owner.");
            }

            return string.Join(" ", messages);
        }

        internal static bool ShouldStartAutomatically(
            bool enabled,
            MultiplayerRoomFlowState state,
            bool isLocalRoomOwner,
            MultiplayerRoomSnapshot snapshot,
            string attemptedRoomId,
            bool operationBusy)
        {
            return enabled &&
                   state == MultiplayerRoomFlowState.InLobby &&
                   isLocalRoomOwner &&
                   snapshot?.CanStart == true &&
                   !string.IsNullOrWhiteSpace(snapshot.RoomId) &&
                   !string.Equals(snapshot.RoomId, attemptedRoomId, StringComparison.Ordinal) &&
                   !operationBusy;
        }

        private static MultiplayerRoomFlowController ResolveController(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null) return null;
            return ctx.Entry.TryGet(out MultiplayerRoomFlowController controller)
                ? controller
                : null;
        }

        private async Task InitializeLobbyAsync(CancellationToken cancellationToken)
        {
            var spec = RequireLaunchSpec();
            if (_gatewayConfig.RestoreRoomOnEntry)
            {
                var restored = await _controller.RestoreAsync(
                    spec,
                    _gatewayConfig.RestoreFallbackPlayerId,
                    cancellationToken);
                if (restored.HasActiveRoom) return;
                if (_controller.CurrentState == MultiplayerRoomFlowState.Failed)
                {
                    _operationError = string.IsNullOrWhiteSpace(restored.Message)
                        ? $"Room restore failed: {restored.Status}."
                        : restored.Message;
                    return;
                }
            }

            await RefreshRoomsCoreAsync(cancellationToken);
        }

        private void TryStartAutomaticPreparation()
        {
            if (_launchRequest?.SuppressAutomaticLobbyActions == true ||
                _gatewayConfig?.AutoReadyDefaultLoadout != true ||
                _controller?.CurrentState != MultiplayerRoomFlowState.InLobby ||
                IsOperationBusy)
            {
                return;
            }

            var roomId = _controller.CurrentRoomId;
            if (string.IsNullOrWhiteSpace(roomId) ||
                string.Equals(_preparedRoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            var localPlayer = FindLocalPlayer(
                _controller.CurrentSnapshot,
                _controller.LocalPlayerId,
                accountId: string.Empty);
            if (localPlayer?.LobbyReady == true && localPlayer.HeroId > 0)
            {
                _preparedRoomId = roomId;
                return;
            }

            _preparedRoomId = roomId;
            StartOperation("Preparing player", PrepareDefaultLoadoutAsync);
        }

        private void TryStartAutomaticMatch()
        {
            var snapshot = _controller?.CurrentSnapshot;
            if (!ShouldStartAutomatically(
                    _launchRequest?.SuppressAutomaticLobbyActions != true &&
                    _gatewayConfig?.AutoStartWhenReady == true,
                    _controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    _controller?.IsLocalRoomOwner == true,
                    snapshot,
                    _automaticStartRoomId,
                    IsOperationBusy))
            {
                return;
            }

            _automaticStartRoomId = snapshot.RoomId;
            StartOperation("Starting match", BeginAutomaticLoadingAsync);
        }

        private async Task BeginAutomaticLoadingAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _controller.BeginLoadingAsync(cancellationToken);
            }
            catch
            {
                _automaticStartRoomId = string.Empty;
                throw;
            }
        }

        private async Task PrepareDefaultLoadoutAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _controller.PickHeroAsync(
                    ResolveAvailableDefaultLoadout(
                        _gatewayConfig.BuildDefaultLoadout(),
                        _controller.CurrentSnapshot,
                        _controller.LocalPlayerId),
                    cancellationToken);
                await _controller.SetReadyAsync(true, cancellationToken);
            }
            catch
            {
                _preparedRoomId = string.Empty;
                throw;
            }
        }

        internal static MultiplayerLoadoutSpec ResolveAvailableDefaultLoadout(
            MultiplayerLoadoutSpec configured,
            MultiplayerRoomSnapshot snapshot,
            uint localPlayerId)
        {
            var teamId = configured.TeamId;
            var spawnPointId = configured.SpawnPointId;
            var players = snapshot?.Players;
            if (players != null)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.PlayerId == localPlayerId || player.HeroId <= 0) continue;
                    if (player.TeamId == teamId && player.HeroId == configured.HeroId)
                    {
                        teamId = teamId == 1 ? 2 : 1;
                    }
                    if (player.TeamId == teamId && player.SpawnPointId == spawnPointId)
                    {
                        spawnPointId++;
                    }
                }
            }

            return new MultiplayerLoadoutSpec(
                configured.HeroId,
                teamId,
                spawnPointId,
                configured.Level,
                configured.AttributeTemplateId,
                configured.BasicAttackSkillId,
                configured.SkillIds);
        }

        private async Task CreateRoomAsync(CancellationToken cancellationToken)
        {
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await _controller.StartCreateRoomAsync(
                RequireLaunchSpec(),
                cancellationToken);
        }

        private async Task JoinRoomAsync(string roomId, CancellationToken cancellationToken)
        {
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await _controller.StartJoinRoomAsync(
                RequireLaunchSpec(),
                roomId,
                cancellationToken);
        }

        private async Task RefreshRoomsCoreAsync(CancellationToken cancellationToken)
        {
            if (_roomListBusy) return;

            _roomListBusy = true;
            try
            {
                var spec = RequireLaunchSpec();
                var result = await _roomDirectory.ListRoomsAsync(
                    new DemoRoomDirectoryQuery(
                        spec.SessionToken,
                        spec.Region,
                        spec.ServerId,
                        spec.RoomType,
                        offset: 0,
                        limit: _gatewayConfig.RoomListLimit),
                    timeout: _launchRequest?.Timeout,
                    cancellationToken: cancellationToken);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.Message)
                            ? "Room directory request failed."
                            : result.Message);
                }

                _rooms.Clear();
                for (var i = 0; i < result.Rooms.Count; i++)
                {
                    if (result.Rooms[i].HasOpenSlot) _rooms.Add(result.Rooms[i]);
                }
                _roomListLoaded = true;
            }
            finally
            {
                _roomListBusy = false;
            }
        }

        private MultiplayerRoomLaunchSpec RequireLaunchSpec()
        {
            if (TryBuildLaunchSpec(
                    _gatewayConfig,
                    _launchRequest,
                    _session?.SessionToken,
                    out var spec,
                    out var error))
            {
                return spec;
            }

            throw new InvalidOperationException(error);
        }

        private void StartOperation(
            string label,
            Func<CancellationToken, Task> operation)
        {
            if (IsOperationBusy || operation == null || _lifetime == null) return;
            _operationTask = RunOperationAsync(label, operation, _lifetime.Token);
        }

        private async Task RunOperationAsync(
            string label,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            _operationLabel = label ?? string.Empty;
            _operationError = string.Empty;
            try
            {
                await operation(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _operationError = ex.Message;
            }
            finally
            {
                _operationLabel = string.Empty;
            }
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("MOBA MULTIPLAYER");
            GUILayout.FlexibleSpace();
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !IsOperationBusy;
            if (GUILayout.Button("Back", GUILayout.Width(64f), GUILayout.Height(24f)))
            {
                StartOperation("Leaving multiplayer", ExitToStarterAsync);
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private void DrawConnectionStatus()
        {
            var state = _gatewayRuntime?.ConnectionState ?? ConnectionState.Disconnected;
            var label = state == ConnectionState.Connected
                ? "Online"
                : state == ConnectionState.Connecting
                    ? "Connecting"
                    : "Offline";
            GUILayout.Label($"Gateway: {label}");

            if (_gatewayRuntime?.RecoveryState == MultiplayerRecoveryState.ReconnectExhausted)
            {
                GUILayout.Label("Connection recovery stopped.");
                if (GUILayout.Button("Reconnect", GUILayout.Height(28f)))
                {
                    _gatewayRuntime.ResetReconnect();
                }
            }
            else if (_gatewayRuntime != null &&
                     _gatewayRuntime.RecoveryState != MultiplayerRecoveryState.None &&
                     _gatewayRuntime.RecoveryState != MultiplayerRecoveryState.Recovered)
            {
                GUILayout.Label("Restoring multiplayer session...");
            }
        }

        private void DrawErrors()
        {
            var error = !string.IsNullOrWhiteSpace(_operationError)
                ? _operationError
                : _controller?.LastError;
            if (!string.IsNullOrWhiteSpace(error))
            {
                GUILayout.Space(6f);
                GUILayout.Label(error);
            }

            if (IsOperationBusy && !string.IsNullOrWhiteSpace(_operationLabel))
            {
                GUILayout.Space(6f);
                GUILayout.Label(_operationLabel + "...");
            }
        }

        private void HandleMembershipChanged(ClientRoomMembershipChange change)
        {
            var notice = FormatMembershipNotice(change);
            if (string.IsNullOrWhiteSpace(notice)) return;

            _roomNotice = notice;
            _roomNoticeExpiresAtUnixMs =
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + RoomNoticeDurationMilliseconds;
        }

        private void DrawRoomNotice()
        {
            if (string.IsNullOrWhiteSpace(_roomNotice)) return;
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= _roomNoticeExpiresAtUnixMs)
            {
                _roomNotice = string.Empty;
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(_roomNotice);
        }

        private void DrawCurrentState()
        {
            if (_controller == null)
            {
                GUILayout.Label("Room flow is unavailable.");
                return;
            }

            switch (_controller.CurrentState)
            {
                case MultiplayerRoomFlowState.Idle:
                    DrawRoomBrowser();
                    break;
                case MultiplayerRoomFlowState.LoggingIn:
                    GUILayout.Label("Authenticating session...");
                    break;
                case MultiplayerRoomFlowState.CreatingRoom:
                    GUILayout.Label("Creating room...");
                    break;
                case MultiplayerRoomFlowState.JoiningRoom:
                    GUILayout.Label("Joining room...");
                    break;
                case MultiplayerRoomFlowState.LeavingRoom:
                    GUILayout.Label("Leaving room...");
                    break;
                case MultiplayerRoomFlowState.InLobby:
                    DrawLobby();
                    break;
                case MultiplayerRoomFlowState.LoadingAssets:
                    DrawLoading("Loading battle assets");
                    break;
                case MultiplayerRoomFlowState.WaitingForBattle:
                    DrawLoading("Waiting for battle server");
                    break;
                case MultiplayerRoomFlowState.Failed:
                    DrawFailed();
                    break;
                default:
                    GUILayout.Label("Entering battle...");
                    break;
            }
        }

        private void DrawRoomBrowser()
        {
            var connected = _gatewayRuntime?.ConnectionState == ConnectionState.Connected;
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && connected && !IsOperationBusy;
            if (GUILayout.Button("Create Room", GUILayout.Height(38f)))
            {
                StartOperation("Creating room", CreateRoomAsync);
            }
            GUI.enabled = previousEnabled;

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Open Rooms");
            GUILayout.FlexibleSpace();
            GUI.enabled = previousEnabled && connected && !IsOperationBusy;
            if (GUILayout.Button("Refresh", GUILayout.Width(72f), GUILayout.Height(24f)))
            {
                StartOperation("Refreshing rooms", RefreshRoomsCoreAsync);
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            if (_roomListLoaded && _rooms.Count == 0 && !IsOperationBusy)
            {
                GUILayout.Label("No open rooms.");
            }

            _roomScroll = GUILayout.BeginScrollView(_roomScroll, GUILayout.Height(300f));
            for (var i = 0; i < _rooms.Count; i++)
            {
                var room = _rooms[i];
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(room.DisplayName);
                GUILayout.Label($"{room.PlayerCount}/{room.MaxPlayers} players");
                GUILayout.EndVertical();
                GUI.enabled = previousEnabled && connected && !IsOperationBusy && room.HasOpenSlot;
                if (GUILayout.Button("Join", GUILayout.Width(64f), GUILayout.Height(34f)))
                {
                    var roomId = room.RoomId;
                    StartOperation(
                        "Joining room",
                        cancellationToken => JoinRoomAsync(roomId, cancellationToken));
                }
                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawLobby()
        {
            var snapshot = _controller.CurrentSnapshot;
            if (snapshot == null)
            {
                GUILayout.Label("Synchronizing room...");
                return;
            }

            GUILayout.Label(string.IsNullOrWhiteSpace(snapshot.RoomId)
                ? "Room"
                : $"Room {snapshot.RoomId}");
            GUILayout.Label($"Players: {snapshot.Players?.Count ?? 0}/{_gatewayConfig.MaxPlayers}");
            DrawPlayers(snapshot);

            var localPlayer = FindLocalPlayer(
                snapshot,
                _controller.LocalPlayerId,
                accountId: string.Empty);
            var localReady = localPlayer?.LobbyReady == true && localPlayer.HeroId > 0;
            var previousEnabled = GUI.enabled;

            GUILayout.Space(8f);
            if (!localReady)
            {
                GUI.enabled = previousEnabled && !IsOperationBusy;
                if (GUILayout.Button("Ready", GUILayout.Height(34f)))
                {
                    _preparedRoomId = snapshot.RoomId;
                    StartOperation("Preparing player", PrepareDefaultLoadoutAsync);
                }
                GUI.enabled = previousEnabled;
            }
            else
            {
                GUILayout.Label("Ready");
            }

            if (_controller.IsLocalRoomOwner)
            {
                GUI.enabled = previousEnabled && snapshot.CanStart && !IsOperationBusy;
                if (GUILayout.Button("Start Match", GUILayout.Height(38f)))
                {
                    StartOperation(
                        "Starting match",
                        cancellationToken => _controller.BeginLoadingAsync(cancellationToken));
                }
                GUI.enabled = previousEnabled;
                if (!snapshot.CanStart)
                {
                    GUILayout.Label("Waiting for all players to be ready.");
                }
            }
            else
            {
                GUILayout.Label("Waiting for room owner to start.");
            }

            GUI.enabled = previousEnabled && _controller.CanLeaveCurrentRoom && !IsOperationBusy;
            if (GUILayout.Button("Leave Room", GUILayout.Height(30f)))
            {
                StartOperation("Leaving room", LeaveRoomAndRefreshAsync);
            }
            GUI.enabled = previousEnabled;
        }

        private static void DrawPlayers(MultiplayerRoomSnapshot snapshot)
        {
            var players = snapshot?.Players;
            if (players == null || players.Count == 0)
            {
                GUILayout.Label("Waiting for room members...");
                return;
            }

            GUILayout.Space(8f);
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var owner = string.Equals(player.AccountId, snapshot.OwnerAccountId, StringComparison.Ordinal)
                    ? "  Owner"
                    : string.Empty;
                var status = !player.IsOnline
                    ? "Offline"
                    : player.LobbyReady && player.HeroId > 0
                        ? "Ready"
                        : "Preparing";
                GUILayout.Label($"{player.AccountId}{owner}   Hero {player.HeroId}   {status}");
            }
        }

        private void DrawLoading(string title)
        {
            GUILayout.Label(title);
            var progress = _controller.LocalLoadingProgress;
            var assetKey = _controller.CurrentLoadingAssetKey;
            GUILayout.Label(string.IsNullOrWhiteSpace(assetKey)
                ? $"Local progress: {progress}%"
                : $"Local progress: {progress}%  {assetKey}");
            DrawProgressBar(progress);
            DrawLoadingDeadline(_controller.CurrentSnapshot);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && _controller.IsLocalRoomOwner && !IsOperationBusy;
            if (GUILayout.Button("Cancel Match Start", GUILayout.Height(30f)))
            {
                StartOperation(
                    "Cancelling match start",
                    cancellationToken => _controller.CancelLoadingAsync(cancellationToken));
            }
            GUI.enabled = previousEnabled;

            GUI.enabled = previousEnabled && _controller.CanLeaveCurrentRoom && !IsOperationBusy;
            if (GUILayout.Button("Leave Room", GUILayout.Height(30f)))
            {
                StartOperation("Leaving room", LeaveRoomAndRefreshAsync);
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawFailed()
        {
            GUILayout.Label("The multiplayer flow could not continue.");
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !IsOperationBusy;
            if (GUILayout.Button("Return to Rooms", GUILayout.Height(32f)))
            {
                StartOperation("Returning to rooms", ReturnToRoomsAsync);
            }
            GUI.enabled = previousEnabled;
        }

        private static void DrawProgressBar(int progress)
        {
            var value = Mathf.Clamp(progress, 0, 100);
            var rect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, string.Empty);
            var innerWidth = Mathf.Max(0f, rect.width - 4f);
            var fill = new Rect(
                rect.x + 2f,
                rect.y + 2f,
                innerWidth * value / 100f,
                rect.height - 4f);
            if (fill.width > 0f) GUI.Box(fill, string.Empty);
            GUI.Label(rect, value + "%");
        }

        private static void DrawLoadingDeadline(MultiplayerRoomSnapshot snapshot)
        {
            if (snapshot == null || snapshot.LoadingDeadlineUnixMs <= 0) return;
            var remainingMs = snapshot.LoadingDeadlineUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            GUILayout.Label($"Time remaining: {Math.Max(0L, remainingMs) / 1000L}s");
        }

        private async Task LeaveRoomAndRefreshAsync(CancellationToken cancellationToken)
        {
            await _controller.LeaveRoomAsync(cancellationToken);
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await RefreshRoomsCoreAsync(cancellationToken);
        }

        private async Task ReturnToRoomsAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_controller.CurrentRoomId))
            {
                if (!_controller.CanLeaveCurrentRoom)
                {
                    throw new InvalidOperationException(
                        $"The room cannot be left during phase {_controller.CurrentSnapshot?.Phase}.");
                }

                await _controller.LeaveRoomAsync(cancellationToken);
            }
            else
            {
                _controller.Cancel();
            }

            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await RefreshRoomsCoreAsync(cancellationToken);
        }

        private async Task ExitToStarterAsync(CancellationToken cancellationToken)
        {
            if (_controller != null && !string.IsNullOrWhiteSpace(_controller.CurrentRoomId))
            {
                if (!_controller.CanLeaveCurrentRoom)
                {
                    throw new InvalidOperationException(
                        $"The room cannot be left during phase {_controller.CurrentSnapshot?.Phase}.");
                }

                await _controller.LeaveRoomAsync(cancellationToken);
            }

            _lifetime?.Cancel();
            _controller?.Cancel();
            _selection?.Clear();
            if (GameEntry.IsInitialized)
            {
                UnityEngine.Object.Destroy(GameEntry.Instance.gameObject);
            }

            var starterScene = !string.IsNullOrWhiteSpace(_gatewayConfig?.StarterSceneName)
                ? _gatewayConfig.StarterSceneName.Trim()
                : "MultiplayerStarterScene";
            SceneManager.LoadScene(starterScene, LoadSceneMode.Single);
        }
    }
}
