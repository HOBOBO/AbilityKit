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
    internal readonly struct FormalLobbyPresentationState
    {
        public FormalLobbyPresentationState(
            string phaseLabel,
            string roleLabel,
            string syncStatus,
            string actionStatus,
            int playerCount,
            int onlinePlayerCount,
            int readyPlayerCount,
            int maxPlayers,
            int minPlayers,
            bool localReady,
            bool canReady,
            bool canStart)
        {
            PhaseLabel = phaseLabel;
            RoleLabel = roleLabel;
            SyncStatus = syncStatus;
            ActionStatus = actionStatus;
            PlayerCount = playerCount;
            OnlinePlayerCount = onlinePlayerCount;
            ReadyPlayerCount = readyPlayerCount;
            MaxPlayers = maxPlayers;
            MinPlayers = minPlayers;
            LocalReady = localReady;
            CanReady = canReady;
            CanStart = canStart;
        }

        public string PhaseLabel { get; }
        public string RoleLabel { get; }
        public string SyncStatus { get; }
        public string ActionStatus { get; }
        public int PlayerCount { get; }
        public int OnlinePlayerCount { get; }
        public int ReadyPlayerCount { get; }
        public int MaxPlayers { get; }
        public int MinPlayers { get; }
        public bool LocalReady { get; }
        public bool CanReady { get; }
        public bool CanStart { get; }
    }

    /// <summary>
    /// Production-style multiplayer lobby for the MOBA demo.
    /// The authoritative room controller owns state; this feature only coordinates entry and presentation.
    /// </summary>
    public sealed class FormalLobbyFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private const float WindowWidth = 460f;
        private const float WindowHeight = 570f;
        private const long RoomNoticeDurationMilliseconds = 6000L;
        private const long RoomListAutoRefreshIntervalMilliseconds = 3000L;

        private readonly MultiplayerBattleEntryGate _battleEntryGate = new MultiplayerBattleEntryGate();
        private readonly List<DemoRoomSummary> _rooms = new List<DemoRoomSummary>();

        private readonly struct LobbyOperationContext
        {
            public LobbyOperationContext(
                int attachGeneration,
                int operationGeneration,
                CancellationToken cancellationToken)
            {
                AttachGeneration = attachGeneration;
                OperationGeneration = operationGeneration;
                CancellationToken = cancellationToken;
            }

            public int AttachGeneration { get; }
            public int OperationGeneration { get; }
            public CancellationToken CancellationToken { get; }
        }

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
        private int _attachGeneration;
        private int _operationGeneration;
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
        private long _lastSnapshotReceivedAtUnixMs;
        private long _lastRoomListRefreshUnixMs;
        private bool _automaticCreateAttempted;

        private bool IsOperationBusy => _operationTask != null && !_operationTask.IsCompleted;

        internal bool OperationBusyForTesting => IsOperationBusy;
        internal string OperationLabelForTesting => _operationLabel;
        internal string OperationErrorForTesting => _operationError;

        internal void StartControlledOperationForTesting(string label, Func<Task> operation)
        {
            StartOperation(
                label,
                _ => operation != null ? operation() : Task.CompletedTask);
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            _attachGeneration++;
            _operationGeneration = 0;
            _operationTask = Task.CompletedTask;
            _operationLabel = string.Empty;
            _operationError = string.Empty;
            _configurationError = string.Empty;
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            _initializationStarted = false;
            _roomListLoaded = false;
            _roomListBusy = false;
            _rooms.Clear();
            _roomScroll = default;
            _roomNotice = string.Empty;
            _roomNoticeExpiresAtUnixMs = 0L;
            _lastSnapshotReceivedAtUnixMs = 0L;
            _lastRoomListRefreshUnixMs = 0L;
            _automaticCreateAttempted = false;
            _battleEntryGate.Reset();

            _gatewayConfig = null;
            _session = null;
            _selection = null;
            _gatewayRuntime = null;
            _roomDirectory = null;
            _launchRequest = null;
            _roomStore = null;
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
                    _roomStore.OnSnapshotChanged += HandleSnapshotChanged;
                    _roomStore.OnMembershipChanged += HandleMembershipChanged;
                    _roomStore.OnPlayerStateChanged += HandlePlayerStateChanged;
                    if (_roomStore.Current != null)
                    {
                        HandleSnapshotChanged(_roomStore.Current);
                    }
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
                _roomStore.OnSnapshotChanged -= HandleSnapshotChanged;
                _roomStore.OnMembershipChanged -= HandleMembershipChanged;
                _roomStore.OnPlayerStateChanged -= HandlePlayerStateChanged;
                _roomStore = null;
            }

            _attachGeneration++;
            _operationGeneration++;
            var lifetime = _lifetime;
            _lifetime = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
            _operationTask = Task.CompletedTask;
            _operationLabel = string.Empty;
            _operationError = string.Empty;
            _roomListBusy = false;
            _roomNotice = string.Empty;
            _roomNoticeExpiresAtUnixMs = 0L;
            _lastSnapshotReceivedAtUnixMs = 0L;
            _lastRoomListRefreshUnixMs = 0L;
            _automaticCreateAttempted = false;
            _preparedRoomId = string.Empty;
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
            TryRefreshRoomsAutomatically();
            TryStartAutomaticCreate();

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
            spec.AccountId = launchRequest?.AccountId?.Trim() ?? string.Empty;
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

            if (!string.IsNullOrWhiteSpace(accountId))
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (string.Equals(players[i].AccountId, accountId, StringComparison.Ordinal))
                    {
                        return players[i];
                    }
                }
            }

            if (localPlayerId != 0u)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (players[i].PlayerId == localPlayerId) return players[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 房间 Owner 是否"不在场"：能确认 Owner 身份时，若 Owner 不在成员列表中或已离线，则视为缺席。
        /// 这种情况下非 Owner 玩家永远无法开战，需要引导其离开并自建房间。
        /// </summary>
        internal static bool IsOwnerAbsent(MultiplayerRoomSnapshot snapshot)
        {
            if (snapshot == null) return false;
            var owner = snapshot.OwnerAccountId;
            if (string.IsNullOrWhiteSpace(owner)) return false;
            var players = snapshot.Players;
            if (players == null) return false;
            for (var i = 0; i < players.Count; i++)
            {
                if (string.Equals(players[i].AccountId, owner, StringComparison.Ordinal))
                {
                    return !players[i].IsOnline;
                }
            }

            return true;
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

        internal static string FormatPlayerStateNotice(ClientRoomPlayerStateChanges changes)
        {
            if (changes?.Changes == null || changes.Changes.Count == 0) return string.Empty;

            var messages = new List<string>();
            for (var i = 0; i < changes.Changes.Count; i++)
            {
                var change = changes.Changes[i];
                if (change.OnlineChanged)
                {
                    messages.Add(change.CurrentOnline
                        ? change.AccountId + " reconnected."
                        : change.AccountId + " went offline.");
                }

                if (change.ReadyChanged)
                {
                    messages.Add(change.CurrentReady
                        ? change.AccountId + " is ready."
                        : change.AccountId + " is no longer ready.");
                }
                else if (change.LoadoutChanged && change.CurrentHeroId > 0)
                {
                    messages.Add(change.AccountId + " selected Hero " + change.CurrentHeroId + ".");
                }
            }

            return string.Join(" ", messages);
        }

        internal static FormalLobbyPresentationState BuildLobbyPresentation(
            MultiplayerRoomSnapshot snapshot,
            MultiplayerRoomPlayerSnapshot localPlayer,
            bool isLocalRoomOwner,
            int maxPlayers,
            int minPlayers,
            ConnectionState connectionState,
            bool snapshotIsStale,
            long lastSnapshotReceivedAtUnixMs,
            long nowUnixMs)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var players = snapshot.Players ?? Array.Empty<MultiplayerRoomPlayerSnapshot>();
            var onlinePlayerCount = 0;
            var readyPlayerCount = 0;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.IsOnline) continue;
                onlinePlayerCount++;
                if (player.LobbyReady && player.HeroId > 0) readyPlayerCount++;
            }

            maxPlayers = Math.Max(Math.Max(1, maxPlayers), players.Count);
            minPlayers = Math.Max(1, Math.Min(minPlayers, maxPlayers));
            var localReady = localPlayer?.LobbyReady == true && localPlayer.HeroId > 0;
            var updatesCurrent = connectionState == ConnectionState.Connected && !snapshotIsStale;
            var canReady = localPlayer != null && !localReady && updatesCurrent;
            var canStart = isLocalRoomOwner &&
                           localReady &&
                           updatesCurrent &&
                           onlinePlayerCount >= minPlayers &&
                           readyPlayerCount == onlinePlayerCount &&
                           snapshot.CanStart;

            string actionStatus;
            if (connectionState != ConnectionState.Connected)
            {
                actionStatus = "Waiting for the room connection.";
            }
            else if (snapshotIsStale)
            {
                actionStatus = "Synchronizing the latest room state.";
            }
            else if (localPlayer == null)
            {
                actionStatus = "Waiting for local player assignment.";
            }
            else if (!localReady)
            {
                actionStatus = "Ready up to join the match.";
            }
            else if (isLocalRoomOwner)
            {
                if (onlinePlayerCount < minPlayers)
                {
                    actionStatus = $"Waiting for players ({onlinePlayerCount}/{minPlayers}).";
                }
                else if (readyPlayerCount < onlinePlayerCount)
                {
                    var remaining = onlinePlayerCount - readyPlayerCount;
                    actionStatus = remaining == 1
                        ? "Waiting for 1 player to be ready."
                        : $"Waiting for {remaining} players to be ready.";
                }
                else
                {
                    actionStatus = snapshot.CanStart
                        ? "All players are ready."
                        : "Waiting for server start confirmation.";
                }
            }
            else
            {
                actionStatus = IsOwnerAbsent(snapshot)
                    ? "Room owner is offline or absent."
                    : "Waiting for room owner to start.";
            }

            return new FormalLobbyPresentationState(
                FormatPhase(snapshot.Phase),
                isLocalRoomOwner ? "Owner" : "Member",
                FormatRoomSyncStatus(
                    snapshot.RoomRevision,
                    connectionState,
                    snapshotIsStale,
                    lastSnapshotReceivedAtUnixMs,
                    nowUnixMs),
                actionStatus,
                players.Count,
                onlinePlayerCount,
                readyPlayerCount,
                maxPlayers,
                minPlayers,
                localReady,
                canReady,
                canStart);
        }

        private static string FormatPhase(MultiplayerRoomPhase phase)
        {
            return phase switch
            {
                MultiplayerRoomPhase.Lobby => "Lobby",
                MultiplayerRoomPhase.Loading => "Loading",
                MultiplayerRoomPhase.Starting => "Starting",
                MultiplayerRoomPhase.InBattle => "In Battle",
                MultiplayerRoomPhase.Closing => "Closing",
                MultiplayerRoomPhase.Closed => "Closed",
                MultiplayerRoomPhase.Expired => "Expired",
                _ => phase.ToString()
            };
        }

        private static string FormatRoomSyncStatus(
            long revision,
            ConnectionState connectionState,
            bool snapshotIsStale,
            long receivedAtUnixMs,
            long nowUnixMs)
        {
            if (connectionState != ConnectionState.Connected)
            {
                return $"Room updates: Paused | Revision {revision}";
            }

            if (snapshotIsStale)
            {
                return $"Room updates: Catching up | Revision {revision}";
            }

            if (receivedAtUnixMs <= 0L)
            {
                return $"Room updates: Synchronizing | Revision {revision}";
            }

            var ageSeconds = Math.Max(0L, nowUnixMs - receivedAtUnixMs) / 1000L;
            var age = ageSeconds <= 1L
                ? "just now"
                : ageSeconds < 60L
                    ? $"{ageSeconds}s ago"
                    : $"{ageSeconds / 60L}m ago";
            return $"Room updates: Live | Revision {revision} | {age}";
        }

        internal static bool ShouldStartAutomatically(
            bool enabled,
            MultiplayerRoomFlowState state,
            bool isLocalRoomOwner,
            MultiplayerRoomSnapshot snapshot,
            int minPlayers,
            string attemptedRoomId,
            bool operationBusy)
        {
            return enabled &&
                   state == MultiplayerRoomFlowState.InLobby &&
                   isLocalRoomOwner &&
                   snapshot?.CanStart == true &&
                   minPlayers > 0 &&
                   snapshot.Players?.Count >= minPlayers &&
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

        private async Task InitializeLobbyAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;

            var spec = RequireLaunchSpec();
            if (_gatewayConfig.RestoreRoomOnEntry)
            {
                var restored = await _controller.RestoreAsync(
                    spec,
                    _gatewayConfig.RestoreFallbackPlayerId,
                    operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
                if (restored.HasActiveRoom) return;
                if (_controller.CurrentState == MultiplayerRoomFlowState.Failed)
                {
                    _operationError = string.IsNullOrWhiteSpace(restored.Message)
                        ? $"Room restore failed: {restored.Status}."
                        : restored.Message;
                    return;
                }
            }

            await RefreshRoomsCoreAsync(operationContext);
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
                _launchRequest?.AccountId);
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
                    _gatewayConfig?.MinPlayers ?? 0,
                    _automaticStartRoomId,
                    IsOperationBusy))
            {
                return;
            }

            _automaticStartRoomId = snapshot.RoomId;
            StartOperation("Starting match", BeginAutomaticLoadingAsync);
        }

        private void TryRefreshRoomsAutomatically()
        {
            if (_controller?.CurrentState != MultiplayerRoomFlowState.Idle) return;
            if (_gatewayRuntime?.ConnectionState != ConnectionState.Connected) return;
            if (IsOperationBusy || _roomListBusy) return;
            // Wait for the initial refresh (seeded inside RefreshRoomsCoreAsync) before polling.
            if (_lastRoomListRefreshUnixMs <= 0L) return;
            var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (nowUnixMs - _lastRoomListRefreshUnixMs < RoomListAutoRefreshIntervalMilliseconds) return;

            StartOperation("Refreshing rooms", RefreshRoomsCoreAsync);
        }

        private void TryStartAutomaticCreate()
        {
            if (_gatewayConfig?.AutoCreateWhenEmpty != true) return;
            if (_controller?.CurrentState != MultiplayerRoomFlowState.Idle) return;
            if (_gatewayRuntime?.ConnectionState != ConnectionState.Connected) return;
            if (IsOperationBusy) return;
            if (!_roomListLoaded) return;
            if (_rooms.Count != 0) return;
            if (_automaticCreateAttempted) return;

            _automaticCreateAttempted = true;
            StartOperation("Creating room", CreateRoomAsync);
        }

        private async Task BeginAutomaticLoadingAsync(LobbyOperationContext operationContext)
        {
            try
            {
                await _controller.BeginLoadingAsync(operationContext.CancellationToken);
            }
            catch
            {
                if (IsCurrentOperation(operationContext))
                {
                    _automaticStartRoomId = string.Empty;
                }
                throw;
            }
        }

        private async Task PrepareDefaultLoadoutAsync(LobbyOperationContext operationContext)
        {
            try
            {
                await _controller.PickHeroAsync(
                    ResolveAvailableDefaultLoadout(
                        _gatewayConfig.BuildDefaultLoadout(),
                        _controller.CurrentSnapshot,
                        _controller.LocalPlayerId),
                    operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
                await _controller.SetReadyAsync(true, operationContext.CancellationToken);
            }
            catch
            {
                if (IsCurrentOperation(operationContext))
                {
                    _preparedRoomId = string.Empty;
                }
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

        private async Task CreateRoomAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await _controller.StartCreateRoomAsync(
                RequireLaunchSpec(),
                operationContext.CancellationToken);
        }

        private async Task JoinRoomAsync(
            string roomId,
            LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await _controller.StartJoinRoomAsync(
                RequireLaunchSpec(),
                roomId,
                operationContext.CancellationToken);
        }

        private async Task RefreshRoomsCoreAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext) || _roomListBusy) return;

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
                    cancellationToken: operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.Message)
                            ? "Room directory request failed."
                            : result.Message);
                }

                var openRooms = new List<DemoRoomSummary>();
                for (var i = 0; i < result.Rooms.Count; i++)
                {
                    if (result.Rooms[i].HasOpenSlot) openRooms.Add(result.Rooms[i]);
                }

                if (!IsCurrentOperation(operationContext)) return;
                _rooms.Clear();
                _rooms.AddRange(openRooms);
                _roomListLoaded = true;
                _lastRoomListRefreshUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            finally
            {
                if (IsCurrentOperation(operationContext))
                {
                    _roomListBusy = false;
                }
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
            Func<LobbyOperationContext, Task> operation)
        {
            var lifetime = _lifetime;
            if (IsOperationBusy || operation == null || lifetime == null) return;

            var operationContext = new LobbyOperationContext(
                _attachGeneration,
                ++_operationGeneration,
                lifetime.Token);
            _operationLabel = label ?? string.Empty;
            _operationError = string.Empty;
            _operationTask = RunOperationAsync(operation, operationContext);
        }

        private async Task RunOperationAsync(
            Func<LobbyOperationContext, Task> operation,
            LobbyOperationContext operationContext)
        {
            try
            {
                await operation(operationContext);
            }
            catch (OperationCanceledException)
                when (operationContext.CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (IsCurrentOperation(operationContext))
                {
                    _operationError = ex.Message;
                }
            }
            finally
            {
                if (IsCurrentOperation(operationContext))
                {
                    _operationLabel = string.Empty;
                }
            }
        }

        private bool IsCurrentOperation(LobbyOperationContext operationContext)
        {
            return _lifetime != null &&
                   _attachGeneration == operationContext.AttachGeneration &&
                   _operationGeneration == operationContext.OperationGeneration;
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
            AppendRoomNotice(notice);
        }

        private void HandlePlayerStateChanged(ClientRoomPlayerStateChanges changes)
        {
            AppendRoomNotice(FormatPlayerStateNotice(changes));
        }

        private void AppendRoomNotice(string notice)
        {
            if (string.IsNullOrWhiteSpace(notice)) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _roomNotice = !string.IsNullOrWhiteSpace(_roomNotice) &&
                          now < _roomNoticeExpiresAtUnixMs
                ? _roomNotice + " " + notice
                : notice;
            _roomNoticeExpiresAtUnixMs = now + RoomNoticeDurationMilliseconds;
        }

        private void HandleSnapshotChanged(ClientRoomSnapshot snapshot)
        {
            if (snapshot == null) return;
            _lastSnapshotReceivedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
                GUILayout.Label(_gatewayConfig?.AutoCreateWhenEmpty == true
                    ? "No open rooms. Creating a new room to host..."
                    : "No open rooms. Click \"Create Room\" to host.");
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
                        operationContext => JoinRoomAsync(roomId, operationContext));
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

            var localPlayer = FindLocalPlayer(
                snapshot,
                _controller.LocalPlayerId,
                _launchRequest?.AccountId);
            var configuredMaxPlayers = _gatewayConfig != null
                ? _gatewayConfig.MaxPlayers
                : snapshot.Players?.Count ?? 1;
            var configuredMinPlayers = _gatewayConfig != null
                ? _gatewayConfig.MinPlayers
                : 1;
            var presentation = BuildLobbyPresentation(
                snapshot,
                localPlayer,
                _controller.IsLocalRoomOwner,
                configuredMaxPlayers,
                configuredMinPlayers,
                _gatewayRuntime?.ConnectionState ?? ConnectionState.Disconnected,
                _roomStore?.IsStale == true,
                _lastSnapshotReceivedAtUnixMs,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            GUILayout.Label(string.IsNullOrWhiteSpace(snapshot.RoomId)
                ? "Room"
                : $"Room {snapshot.RoomId}");
            GUILayout.Label($"Status: {presentation.PhaseLabel}   You: {presentation.RoleLabel}");
            GUILayout.Label(
                $"Players: {presentation.PlayerCount}/{presentation.MaxPlayers}   " +
                $"Ready: {presentation.ReadyPlayerCount}/{presentation.OnlinePlayerCount}");
            GUILayout.Label(presentation.SyncStatus);
            DrawPlayers(snapshot, localPlayer);

            var previousEnabled = GUI.enabled;

            GUILayout.Space(8f);
            if (!presentation.LocalReady)
            {
                GUI.enabled = previousEnabled && presentation.CanReady && !IsOperationBusy;
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
                GUI.enabled = previousEnabled && presentation.CanStart && !IsOperationBusy;
                if (GUILayout.Button("Start Match", GUILayout.Height(38f)))
                {
                    StartOperation(
                        "Starting match",
                        operationContext => _controller.BeginLoadingAsync(
                            operationContext.CancellationToken));
                }
                GUI.enabled = previousEnabled;
                GUILayout.Label(presentation.ActionStatus);
            }
            else if (IsOwnerAbsent(snapshot))
            {
                GUILayout.Label(presentation.ActionStatus);
                GUILayout.Label("This room cannot be started.");
                GUI.enabled = previousEnabled && _controller.CanLeaveCurrentRoom && !IsOperationBusy;
                if (GUILayout.Button("Leave & Create Room", GUILayout.Height(34f)))
                {
                    StartOperation("Leaving and creating room", LeaveAndCreateRoomAsync);
                }
                GUI.enabled = previousEnabled;
            }
            else
            {
                GUILayout.Label(presentation.ActionStatus);
            }

            GUI.enabled = previousEnabled && _controller.CanLeaveCurrentRoom && !IsOperationBusy;
            if (GUILayout.Button("Leave Room", GUILayout.Height(30f)))
            {
                StartOperation("Leaving room", LeaveRoomAndRefreshAsync);
            }
            GUI.enabled = previousEnabled;
        }

        private static void DrawPlayers(
            MultiplayerRoomSnapshot snapshot,
            MultiplayerRoomPlayerSnapshot localPlayer)
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
                    ? " | Owner"
                    : string.Empty;
                var local = ReferenceEquals(player, localPlayer) ? " | You" : string.Empty;
                var status = !player.IsOnline
                    ? "Offline"
                    : player.LobbyReady && player.HeroId > 0
                        ? "Ready"
                        : "Preparing";
                GUILayout.Label($"{player.AccountId}{local}{owner}   Hero {player.HeroId}   {status}");
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
                    operationContext => _controller.CancelLoadingAsync(
                        operationContext.CancellationToken));
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

        private async Task LeaveRoomAndRefreshAsync(LobbyOperationContext operationContext)
        {
            await _controller.LeaveRoomAsync(operationContext.CancellationToken);
            if (!IsCurrentOperation(operationContext)) return;
            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await RefreshRoomsCoreAsync(operationContext);
        }

        private async Task LeaveAndCreateRoomAsync(LobbyOperationContext operationContext)
        {
            if (_controller.CanLeaveCurrentRoom)
            {
                await _controller.LeaveRoomAsync(operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
            }

            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await CreateRoomAsync(operationContext);
        }

        private async Task ReturnToRoomsAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            if (!string.IsNullOrWhiteSpace(_controller.CurrentRoomId))
            {
                if (!_controller.CanLeaveCurrentRoom)
                {
                    throw new InvalidOperationException(
                        $"The room cannot be left during phase {_controller.CurrentSnapshot?.Phase}.");
                }

                await _controller.LeaveRoomAsync(operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
            }
            else
            {
                _controller.Cancel();
            }

            _preparedRoomId = string.Empty;
            _automaticStartRoomId = string.Empty;
            await RefreshRoomsCoreAsync(operationContext);
        }

        private async Task ExitToStarterAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            if (_controller != null && !string.IsNullOrWhiteSpace(_controller.CurrentRoomId))
            {
                if (!_controller.CanLeaveCurrentRoom)
                {
                    throw new InvalidOperationException(
                        $"The room cannot be left during phase {_controller.CurrentSnapshot?.Phase}.");
                }

                await _controller.LeaveRoomAsync(operationContext.CancellationToken);
                if (!IsCurrentOperation(operationContext)) return;
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
