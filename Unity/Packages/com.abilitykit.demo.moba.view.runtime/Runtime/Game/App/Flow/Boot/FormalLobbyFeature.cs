using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using UnityEngine;
namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Production-style multiplayer lobby for the MOBA demo.
    /// The authoritative room controller owns state; this feature only coordinates entry and presentation.
    /// </summary>
    public sealed class FormalLobbyFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private const long RoomNoticeDurationMilliseconds = 6000L;
        private const long RoomListAutoRefreshIntervalMilliseconds = 3000L;
        private const long RoomSnapshotRecoveryIntervalMilliseconds = 5000L;

        private readonly FormalLobbyRuntime _runtime = new FormalLobbyRuntime();
        private readonly LobbyRoomDirectoryRuntime _directoryRuntime = new LobbyRoomDirectoryRuntime();
        private readonly LobbyRoomStoreSubscription _roomSubscription =
            new LobbyRoomStoreSubscription();
        private readonly LobbyBattleEntryCoordinator _battleEntry = new LobbyBattleEntryCoordinator();
        private readonly LobbySceneExitLifecycle _sceneExit = new LobbySceneExitLifecycle();

        private MultiplayerRoomFlowController _controller;
        private FormalLobbyCommandCoordinator _commands;
        private GatewayMultiplayerRoomSession _session;
        private LobbyBattleEntrySelection _selection;
        private IMultiplayerGatewayDiagnostics _gatewayDiagnostics;
        private IMultiplayerGatewayRecoveryControl _gatewayRecoveryControl;
        private IDemoRoomDirectoryClient _roomDirectory;
        private ClientRoomPushSynchronizer _pushSynchronizer;
        private BattleGatewayConfigSO _gatewayConfig;
        private DemoMultiplayerLaunchRequest _launchRequest;
        private string _configurationError = string.Empty;
        private Vector2 _roomScroll;
        private string _roomNotice = string.Empty;
        private long _roomNoticeExpiresAtUnixMs;
        private long _lastSnapshotReceivedAtUnixMs;
        private long _nextRoomSnapshotRecoveryUnixMs;
        private ClientRoomSnapshot _lastObservedRoomSnapshot;

        private bool IsOperationBusy => _runtime.IsOperationBusy;

        internal bool OperationBusyForTesting => IsOperationBusy;
        internal string OperationLabelForTesting => _runtime.OperationLabel;
        internal string OperationErrorForTesting => _runtime.OperationError;

        internal void StartControlledOperationForTesting(string label, Func<Task> operation)
        {
            StartOperation(
                label,
                _ => operation != null ? operation() : Task.CompletedTask);
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            _runtime.Attach();
            _directoryRuntime.Attach();
            _battleEntry.Attach();
            _configurationError = string.Empty;
            _roomScroll = default;
            _roomNotice = string.Empty;
            _roomNoticeExpiresAtUnixMs = 0L;
            _lastSnapshotReceivedAtUnixMs = 0L;
            _nextRoomSnapshotRecoveryUnixMs = 0L;
            _lastObservedRoomSnapshot = null;

            _gatewayConfig = null;
            _commands = null;
            _session = null;
            _selection = null;
            _gatewayDiagnostics = null;
            _gatewayRecoveryControl = null;
            _roomDirectory = null;
            _pushSynchronizer = null;
            _launchRequest = null;
            _controller = ResolveController(ctx);
            if (_controller != null)
            {
                _commands = new FormalLobbyCommandCoordinator(_runtime, _controller);
            }
            if (ctx.Entry != null)
            {
                ctx.Entry.TryGet(out _gatewayConfig);
                ctx.Entry.TryGet(out _session);
                ctx.Entry.TryGet(out _selection);
                ctx.Entry.TryGet(out _gatewayDiagnostics);
                ctx.Entry.TryGet(out _gatewayRecoveryControl);
                ctx.Entry.TryGet(out _roomDirectory);
                ctx.Entry.TryGet(out _launchRequest);
                ctx.Entry.TryGet(out _pushSynchronizer);
                if (ctx.Entry.TryGet(out ClientRoomStore roomStore))
                {
                    _roomSubscription.Attach(
                        roomStore,
                        HandleSnapshotChanged,
                        HandleMembershipChanged,
                        HandlePlayerStateChanged);
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
            _roomSubscription.Detach();
            _runtime.Detach();
            _directoryRuntime.Detach();
            _battleEntry.Detach();
            _roomNotice = string.Empty;
            _roomNoticeExpiresAtUnixMs = 0L;
            _lastSnapshotReceivedAtUnixMs = 0L;
            _nextRoomSnapshotRecoveryUnixMs = 0L;
            _lastObservedRoomSnapshot = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (!ShouldShowFlowWindow(_selection)) return;

            if (ShouldRunAutomaticLobbyActions(_launchRequest))
            {
                if (string.IsNullOrEmpty(_configurationError) &&
                    !_runtime.InitializationStarted &&
                    _gatewayDiagnostics?.ConnectionState == ConnectionState.Connected)
                {
                    if (_runtime.TryBeginInitialization())
                    {
                        StartOperation("Opening multiplayer lobby", InitializeLobbyAsync);
                    }
                }

                TryStartAutomaticPreparation();
                TryStartAutomaticMatch();
                TryRecoverCurrentRoomSnapshot();
                TryRefreshRoomsAutomatically();
                TryStartAutomaticCreate();
            }

            if (!ShouldEnterBattle(_selection, _controller) ||
                ShouldDeferBattleEntryForRestore(
                    _gatewayConfig?.RestoreRoomOnEntry == true,
                    _runtime.InitializationStarted,
                    IsOperationBusy,
                    _controller?.LastRestoreResult))
            {
                return;
            }

            var snapshot = _controller.CurrentSnapshot;
            var flow = ctx.Entry?.Get<GameFlowDomain>();
            if (snapshot == null ||
                flow == null ||
                _session == null ||
                string.IsNullOrWhiteSpace(_session.SessionToken))
            {
                return;
            }
            var coldStartReconnect = ShouldUseColdStartRecovery(
                _controller.LastRestoreResult);
            _battleEntry.TryEnterBattle(
                _controller.CurrentState,
                snapshot,
                _selection,
                _session,
                _launchRequest,
                _controller.LocalPlayerId,
                coldStartReconnect,
                flow.EnterBattle);
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null || !ShouldShowFlowWindow(_selection)) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle) return;

            var snapshot = BuildScreenSnapshot();
            var commands = new FormalLobbyRenderCommands(
                ExitToStarter,
                ResetReconnect,
                CreateRoom,
                RefreshRooms,
                JoinRoom,
                Ready,
                NotReady,
                StartMatch,
                LeaveAndCreateRoom,
                LeaveRoom,
                CancelLoading,
                ReturnToRooms);
            FormalLobbyRenderer.Draw(snapshot, commands, ref _roomScroll);
        }

        internal static bool ShouldShowFlowWindow(LobbyBattleEntrySelection selection)
        {
            return selection?.IsRemoteSelected == true;
        }

        internal static bool ShouldRunAutomaticLobbyActions(
            DemoMultiplayerLaunchRequest launchRequest)
        {
            return launchRequest?.SuppressAutomaticLobbyActions != true;
        }

        internal static bool ShouldEnterBattle(
            LobbyBattleEntrySelection selection,
            MultiplayerRoomFlowController controller)
        {
            return controller != null && FormalLobbyDecision.ShouldEnterBattle(
                selection?.IsRemoteSelected == true,
                controller.CurrentState,
                controller.CurrentSnapshot);
        }

        internal static bool ShouldDeferBattleEntryForRestore(
            bool restoreRoomOnEntry,
            bool initializationStarted,
            bool operationBusy,
            MultiplayerRoomRestoreResult? restoreResult)
        {
            return restoreRoomOnEntry &&
                   initializationStarted &&
                   operationBusy &&
                   !restoreResult.HasValue;
        }

        internal static bool ShouldUseColdStartRecovery(
            MultiplayerRoomRestoreResult? restoreResult)
        {
            return restoreResult.HasValue &&
                   restoreResult.Value.HasActiveRoom &&
                   restoreResult.Value.NextStep == MultiplayerRoomRestoreNextStep.EnterBattle;
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
            return FormalLobbyDecision.FindLocalPlayer(snapshot, localPlayerId, accountId);
        }

        internal static bool IsOwnerAbsent(MultiplayerRoomSnapshot snapshot)
        {
            return FormalLobbyDecision.IsOwnerAbsent(snapshot);
        }

        internal static string FormatMembershipNotice(ClientRoomMembershipChange change)
        {
            return LobbyNoticeFormatter.FormatMembership(change);
        }

        internal static string FormatPlayerStateNotice(ClientRoomPlayerStateChanges changes)
        {
            return LobbyNoticeFormatter.FormatPlayerState(changes);
        }

        internal static string FormatPhaseRollbackNotice(
            ClientRoomSnapshot previous,
            ClientRoomSnapshot current)
        {
            return LobbyNoticeFormatter.FormatPhaseRollback(previous, current);
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
            return FormalLobbyPresenter.Build(
                snapshot,
                localPlayer,
                isLocalRoomOwner,
                maxPlayers,
                minPlayers,
                connectionState,
                snapshotIsStale,
                lastSnapshotReceivedAtUnixMs,
                nowUnixMs);
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
            return LobbyAutomationPolicy.ShouldStart(
                enabled,
                state,
                isLocalRoomOwner,
                snapshot,
                minPlayers,
                attemptedRoomId,
                operationBusy);
        }

        private static MultiplayerRoomFlowController ResolveController(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null) return null;
            return ctx.Entry.TryGet(out MultiplayerRoomFlowController controller)
                ? controller
                : null;
        }

        private Task InitializeLobbyAsync(LobbyOperationContext operationContext)
        {
            return _commands.InitializeAsync(
                RequireLaunchSpec(),
                _gatewayConfig.RestoreRoomOnEntry,
                _gatewayConfig.RestoreFallbackPlayerId,
                RefreshRoomsCoreAsync,
                operationContext);
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
                string.Equals(_runtime.PreparedRoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            var localPlayer = FindLocalPlayer(
                _controller.CurrentSnapshot,
                _controller.LocalPlayerId,
                _launchRequest?.AccountId);
            if (localPlayer?.LobbyReady == true && localPlayer.HeroId > 0)
            {
                _runtime.MarkPrepared(roomId);
                return;
            }

            _runtime.MarkPrepared(roomId);
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
                    _runtime.AutomaticStartRoomId,
                    IsOperationBusy))
            {
                return;
            }

            _runtime.MarkAutomaticStart(snapshot.RoomId);
            StartOperation("Starting match", BeginAutomaticLoadingAsync);
        }

        private void TryRecoverCurrentRoomSnapshot()
        {
            var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_pushSynchronizer == null ||
                _controller?.CurrentState != MultiplayerRoomFlowState.InLobby ||
                _gatewayDiagnostics?.ConnectionState != ConnectionState.Connected ||
                IsOperationBusy ||
                nowUnixMs < _nextRoomSnapshotRecoveryUnixMs)
            {
                return;
            }

            _nextRoomSnapshotRecoveryUnixMs = nowUnixMs + RoomSnapshotRecoveryIntervalMilliseconds;
            StartOperation(
                "Synchronizing room",
                context => _pushSynchronizer.TryRefreshAfterSilenceAsync(
                    TimeSpan.FromMilliseconds(RoomSnapshotRecoveryIntervalMilliseconds),
                    context.CancellationToken));
        }

        private void TryRefreshRoomsAutomatically()
        {
            var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!LobbyAutomationPolicy.ShouldRefreshDirectory(
                    _controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    _gatewayDiagnostics?.ConnectionState == ConnectionState.Connected,
                    IsOperationBusy,
                    _directoryRuntime.IsBusy,
                    _directoryRuntime.LastRefreshUnixMs,
                    nowUnixMs,
                    RoomListAutoRefreshIntervalMilliseconds))
            {
                return;
            }

            StartOperation("Refreshing rooms", RefreshRoomsCoreAsync);
        }

        private void TryStartAutomaticCreate()
        {
            if (!LobbyAutomationPolicy.ShouldCreateRoom(
                    _gatewayConfig?.AutoCreateWhenEmpty == true,
                    _controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    _gatewayDiagnostics?.ConnectionState == ConnectionState.Connected,
                    IsOperationBusy,
                    _directoryRuntime.IsLoaded,
                    _directoryRuntime.Rooms.Count,
                    _runtime.AutomaticCreateAttempted))
            {
                return;
            }

            _runtime.MarkAutomaticCreateAttempted();
            StartOperation("Creating room", CreateRoomAsync);
        }

        private Task BeginAutomaticLoadingAsync(LobbyOperationContext operationContext)
        {
            return _commands.BeginAutomaticLoadingAsync(operationContext);
        }

        private Task PrepareDefaultLoadoutAsync(LobbyOperationContext operationContext)
        {
            return _commands.PrepareDefaultLoadoutAsync(
                _gatewayConfig.BuildDefaultLoadout(),
                _gatewayConfig.BuildSecondPlayerLoadout(),
                operationContext);
        }

        internal static MultiplayerLoadoutSpec ResolveAvailableDefaultLoadout(
            MultiplayerLoadoutSpec firstPlayerLoadout,
            MultiplayerLoadoutSpec secondPlayerLoadout,
            MultiplayerRoomSnapshot snapshot,
            uint localPlayerId)
        {
            return FormalLobbyDecision.ResolveAvailableDefaultLoadout(
                firstPlayerLoadout,
                secondPlayerLoadout,
                snapshot,
                localPlayerId);
        }

        private Task CreateRoomAsync(LobbyOperationContext operationContext)
        {
            return _commands.CreateRoomAsync(RequireLaunchSpec(), operationContext);
        }

        private Task JoinRoomAsync(
            string roomId,
            LobbyOperationContext operationContext)
        {
            return _commands.JoinRoomAsync(RequireLaunchSpec(), roomId, operationContext);
        }

        private async Task RefreshRoomsCoreAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            var spec = RequireLaunchSpec();
            await _directoryRuntime.RefreshAsync(
                _roomDirectory,
                new DemoRoomDirectoryQuery(
                    spec.SessionToken,
                    spec.Region,
                    spec.ServerId,
                    spec.RoomType,
                    offset: 0,
                    limit: _gatewayConfig.RoomListLimit),
                _launchRequest?.Timeout,
                operationContext,
                IsCurrentOperation);
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
            _runtime.StartOperation(label, operation);
        }

        private bool IsCurrentOperation(LobbyOperationContext operationContext)
        {
            return _runtime.IsCurrent(operationContext);
        }

        private FormalLobbyScreenSnapshot BuildScreenSnapshot()
        {
            var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var controller = _controller;
            var roomSnapshot = controller?.CurrentSnapshot;
            var recoveryState = _gatewayDiagnostics?.RecoveryState ?? MultiplayerRecoveryState.None;
            var input = new FormalLobbyScreenInput(
                _configurationError,
                _gatewayDiagnostics?.ConnectionState ?? ConnectionState.Disconnected,
                recoveryState == MultiplayerRecoveryState.ReconnectExhausted,
                recoveryState != MultiplayerRecoveryState.None &&
                recoveryState != MultiplayerRecoveryState.Recovered &&
                recoveryState != MultiplayerRecoveryState.ReconnectExhausted,
                _runtime.OperationError,
                controller?.LastError,
                _runtime.OperationLabel,
                ResolveActiveNotice(nowUnixMs),
                IsOperationBusy,
                controller != null,
                controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                _directoryRuntime.IsLoaded,
                _gatewayConfig?.AutoCreateWhenEmpty == true,
                _directoryRuntime.Rooms,
                controller?.IsLocalRoomOwner == true,
                controller?.CanLeaveCurrentRoom == true,
                controller?.LocalLoadingProgress ?? 0,
                controller?.CurrentLoadingAssetKey,
                roomSnapshot?.LoadingDeadlineUnixMs ?? 0L,
                nowUnixMs,
                BuildLobbySnapshot(roomSnapshot, nowUnixMs));
            return FormalLobbyPresenter.BuildScreen(in input);
        }

        private FormalLobbyPresentationSnapshot? BuildLobbySnapshot(
            MultiplayerRoomSnapshot snapshot,
            long nowUnixMs)
        {
            if (_controller?.CurrentState != MultiplayerRoomFlowState.InLobby || snapshot == null)
            {
                return null;
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
                _gatewayDiagnostics?.ConnectionState ?? ConnectionState.Disconnected,
                _roomSubscription.IsStale,
                _lastSnapshotReceivedAtUnixMs,
                nowUnixMs);
            return new FormalLobbyPresentationSnapshot(
                snapshot.RoomId,
                presentation,
                FormalLobbyPresenter.BuildPlayerLabels(snapshot, localPlayer),
                _controller.IsLocalRoomOwner,
                IsOwnerAbsent(snapshot),
                _controller.CanLeaveCurrentRoom,
                IsOperationBusy);
        }

        private string ResolveActiveNotice(long nowUnixMs)
        {
            if (string.IsNullOrWhiteSpace(_roomNotice)) return string.Empty;
            if (nowUnixMs < _roomNoticeExpiresAtUnixMs) return _roomNotice;

            _roomNotice = string.Empty;
            return string.Empty;
        }

        private void ExitToStarter()
        {
            StartOperation("Leaving multiplayer", ExitToStarterAsync);
        }

        private void ResetReconnect()
        {
            _gatewayRecoveryControl?.ResetReconnect();
        }

        private void CreateRoom()
        {
            StartOperation("Creating room", CreateRoomAsync);
        }

        private void RefreshRooms()
        {
            StartOperation("Refreshing rooms", RefreshRoomsCoreAsync);
        }

        private void JoinRoom(string roomId)
        {
            StartOperation(
                "Joining room",
                operationContext => JoinRoomAsync(roomId, operationContext));
        }

        private void Ready()
        {
            var roomId = _controller?.CurrentSnapshot?.RoomId ?? string.Empty;
            _runtime.MarkPrepared(roomId);
            StartOperation("Preparing player", PrepareDefaultLoadoutAsync);
        }

        private void NotReady()
        {
            var roomId = _controller?.CurrentSnapshot?.RoomId ?? string.Empty;
            _runtime.MarkPrepared(roomId);
            StartOperation(
                "Cancelling ready state",
                operationContext => _commands.SetReadyAsync(false, operationContext));
        }

        private void StartMatch()
        {
            StartOperation("Starting match", _commands.BeginLoadingAsync);
        }

        private void LeaveAndCreateRoom()
        {
            StartOperation("Leaving and creating room", LeaveAndCreateRoomAsync);
        }

        private void LeaveRoom()
        {
            StartOperation("Leaving room", LeaveRoomAndRefreshAsync);
        }

        private void CancelLoading()
        {
            StartOperation("Cancelling match start", _commands.CancelLoadingAsync);
        }

        private void ReturnToRooms()
        {
            StartOperation("Returning to rooms", ReturnToRoomsAsync);
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

            var previous = _lastObservedRoomSnapshot;
            _lastObservedRoomSnapshot = snapshot;
            _lastSnapshotReceivedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AppendRoomNotice(FormatPhaseRollbackNotice(previous, snapshot));
        }

        private Task LeaveRoomAndRefreshAsync(LobbyOperationContext operationContext)
        {
            return _commands.LeaveRoomAndRefreshAsync(RefreshRoomsCoreAsync, operationContext);
        }

        private Task LeaveAndCreateRoomAsync(LobbyOperationContext operationContext)
        {
            return _commands.LeaveAndCreateRoomAsync(RequireLaunchSpec(), operationContext);
        }

        private Task ReturnToRoomsAsync(LobbyOperationContext operationContext)
        {
            return _commands.ReturnToRoomsAsync(RefreshRoomsCoreAsync, operationContext);
        }

        private async Task ExitToStarterAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrentOperation(operationContext)) return;
            if (_commands != null)
            {
                await _commands.LeaveBeforeExitAsync(operationContext);
                if (!IsCurrentOperation(operationContext)) return;
            }

            _sceneExit.Exit(
                _runtime.CancelLifetime,
                () => _controller?.Cancel(),
                () => _selection?.Clear(),
                _gatewayConfig?.StarterSceneName);
        }
    }
}
