#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    [DisallowMultipleComponent]
    public sealed class ShooterFormalMultiplayerController : MonoBehaviour
    {
        private const float WindowWidth = 420f;
        private const float WindowHeight = 470f;

        [SerializeField] private ShooterMultiplayerProfileSO? profile;

        private readonly DemoRoomListState<DemoRoomSummary> _rooms = new DemoRoomListState<DemoRoomSummary>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private DemoMultiplayerLaunchRequest _request = default!;
        private ShooterClientNetworkLauncher? _roomLauncher;
        private ShooterRoomGatewayRoomClient? _roomClient;
        private ShooterRoomSessionController? _roomController;
        private CancellationTokenSource? _battleWaitCancellation;
        private Task? _battleWaitTask;
        private string _status = "Initializing";
        private string _error = string.Empty;
        private Vector2 _roomScroll;
        private bool _hasRequest;
        private bool _busy;
        private bool _battleLaunchRequested;
        private bool _autoStartSuppressed;
        private bool _returning;

        private void Awake()
        {
            _hasRequest = DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Shooter,
                out _request);
            if (profile != null)
            {
                ShooterRemoteStateSyncPlayModeHost.SetViewBackend(profile.RenderBackend);
            }

            if (!_hasRequest || !_request.IsAuthenticated)
            {
                _status = "Authentication required";
                _error = "Open Shooter from the multiplayer starter after login.";
            }
        }

        private void Start()
        {
            if (_hasRequest && _request.IsAuthenticated && profile != null)
            {
                RunAsync("Loading rooms", RefreshRoomsAsync);
            }
        }

        private void Update()
        {
            DriveAutomaticRoomFlow();
        }

        private void OnGUI()
        {
            if (ShooterRemoteStateSyncPlayModeHost.IsRunning)
            {
                DrawBattleStatus();
                return;
            }

            var rect = new Rect(
                Mathf.Max(16f, (Screen.width - WindowWidth) * 0.5f),
                Mathf.Max(16f, (Screen.height - WindowHeight) * 0.5f),
                WindowWidth,
                WindowHeight);
            GUILayout.BeginArea(rect, "Shooter Multiplayer", GUI.skin.window);
            GUILayout.Space(6f);
            GUILayout.Label($"Account: {ValueOrDash(_request.AccountId)}");
            GUILayout.Label($"Server: {ValueOrDash(_request.Region)} / {ValueOrDash(_request.ServerId)}");
            GUILayout.Label(_busy ? $"Status: {_status}..." : $"Status: {_status}");
            if (!string.IsNullOrWhiteSpace(_error)) GUILayout.Label($"Error: {_error}");

            if (_roomController?.HasActiveRoom == true)
            {
                DrawRoomSession(_roomController);
            }
            else
            {
                DrawRoomDirectory();
            }

            GUILayout.FlexibleSpace();
            if (!_returning && GUILayout.Button("Back", GUILayout.Height(30f)))
            {
                ReturnToStarterAsync();
            }
            GUILayout.EndArea();
        }

        private void DrawRoomDirectory()
        {
            GUILayout.Space(8f);
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_busy && _hasRequest && _request.IsAuthenticated && profile != null;
            if (GUILayout.Button("Create Room", GUILayout.Height(38f)))
            {
                RunAsync("Creating room", () => StartRoomAsync(create: true, string.Empty));
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Rooms ({_rooms.Count})");
            if (GUILayout.Button("Refresh", GUILayout.Width(88f))) RunAsync("Loading rooms", RefreshRoomsAsync);
            GUILayout.EndHorizontal();

            _roomScroll = GUILayout.BeginScrollView(_roomScroll, GUILayout.Height(220f));
            for (var i = 0; i < _rooms.Rooms.Count; i++)
            {
                var room = _rooms.Rooms[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{room.DisplayName}  {room.PlayerCount}/{room.MaxPlayers}");
                GUI.enabled = previousEnabled && !_busy && room.HasOpenSlot;
                if (GUILayout.Button("Join", GUILayout.Width(72f)))
                {
                    RunAsync("Joining room", () => StartRoomAsync(create: false, room.RoomId));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUI.enabled = previousEnabled;
        }

        private void DrawRoomSession(ShooterRoomSessionController controller)
        {
            GUILayout.Space(10f);
            GUILayout.Label($"Room: {controller.CurrentRoomId}");
            GUILayout.Label($"State: {FormatState(controller.CurrentState)}");
            var snapshot = controller.CurrentSnapshot;
            if (snapshot != null)
            {
                GUILayout.Label(controller.IsLocalRoomOwner ? "Role: Owner" : "Role: Member");
                GUILayout.Space(4f);
                for (var i = 0; i < snapshot.Members.Count; i++)
                {
                    var member = snapshot.Members[i];
                    var ready = member.LobbyReady ? "Ready" : "Not ready";
                    var online = member.IsOnline ? string.Empty : " / Offline";
                    var loading = snapshot.Phase == ShooterRoomSessionPhase.Loading
                        ? $" / {member.LoadingProgress}%"
                        : string.Empty;
                    GUILayout.Label($"P{member.PlayerId}  {ready}{loading}{online}");
                }
            }

            DrawLoadingStatus();
            GUILayout.Space(8f);
            if (controller.CurrentState == ShooterRoomSessionState.InLobby)
            {
                var local = snapshot?.FindMember(controller.LocalPlayerId);
                GUI.enabled = !_busy;
                if (GUILayout.Button(local?.LobbyReady == true ? "Not Ready" : "Ready", GUILayout.Height(30f)))
                {
                    RunAsync("Updating ready state", () => controller.SetReadyAsync(local?.LobbyReady != true, _lifetime.Token));
                }

                GUI.enabled = !_busy && controller.IsLocalRoomOwner && snapshot?.CanStart == true;
                if (GUILayout.Button("Start Match", GUILayout.Height(30f)))
                {
                    RunAsync("Starting loading", () => controller.BeginLoadingAsync(_lifetime.Token));
                }
            }
            else if ((controller.CurrentState == ShooterRoomSessionState.LoadingAssets ||
                      controller.CurrentState == ShooterRoomSessionState.WaitingForBattle) &&
                     controller.IsLocalRoomOwner)
            {
                GUI.enabled = true;
                if (GUILayout.Button("Cancel Loading", GUILayout.Height(30f))) CancelLoadingAsync();
            }

            GUI.enabled = controller.CanLeaveCurrentRoom && controller.CurrentState != ShooterRoomSessionState.LeavingRoom;
            if (GUILayout.Button("Leave Room", GUILayout.Height(30f))) LeaveRoomAsync(returnToStarter: false);
            GUI.enabled = true;
        }

        private void DrawBattleStatus()
        {
            var isPaused = ShooterRemoteStateSyncPlayModeHost.IsPaused;
            var areaHeight = isPaused ? 140f : 120f;
            GUILayout.BeginArea(new Rect(12f, 12f, 320f, areaHeight), "Shooter Multiplayer", GUI.skin.window);
            GUILayout.Label($"Room: {ShooterRemoteStateSyncPlayModeHost.Flow?.RoomId ?? string.Empty}");
            GUILayout.Label(isPaused ? "State: Paused (simulating disconnect)" : "State: In battle");

            GUI.enabled = !_busy;
            if (isPaused)
            {
                if (GUILayout.Button("Resume (Reconnect)", GUILayout.Height(28f)))
                {
                    RunAsync("Resuming battle", () => ResumeBattleAsync());
                }
            }
            else
            {
                if (GUILayout.Button("Pause (Simulate Disconnect)", GUILayout.Height(28f)))
                {
                    ShooterRemoteStateSyncPlayModeHost.PauseForReconnectValidation();
                    _status = "Paused: connection closed";
                }
            }
            GUI.enabled = true;

            if (!isPaused && GUILayout.Button("Disconnect and Return", GUILayout.Height(26f))) ReturnFromBattle();
            GUILayout.EndArea();
        }

        private async Task ResumeBattleAsync()
        {
            try
            {
                _status = "Reconnecting...";
                await ShooterRemoteStateSyncPlayModeHost.ResumeFromPauseAsync();
                _status = "Battle resumed";
            }
            catch (Exception ex)
            {
                _status = "Resume failed";
                _error = ex.Message;
            }
        }

        private static void DrawLoadingStatus()
        {
            var loading = ShooterMultiplayerLoadingStatus.Current;
            if (loading.LocalProgress <= 0 && string.IsNullOrWhiteSpace(loading.Stage)) return;
            GUILayout.Space(6f);
            GUILayout.Label($"Loading: {loading.LocalProgress}%  {loading.Stage}");
            var value = Mathf.Clamp(loading.LocalProgress, 0, 100);
            var rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, string.Empty);
            var fill = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * value / 100f, rect.height - 4f);
            if (fill.width > 0f) GUI.Box(fill, string.Empty);
        }

        private async Task RefreshRoomsAsync()
        {
            var selectedProfile = RequireProfile();
            var result = await WithRoomClient(client => client.ListRoomsAsync(
                new DemoRoomDirectoryQuery(
                    _request.SessionToken,
                    _request.Region,
                    _request.ServerId,
                    roomType: ShooterGameplay.RoomType,
                    offset: 0,
                    limit: selectedProfile.RoomListLimit),
                _request.Timeout));
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _rooms.ReplaceRooms(result.Rooms, result.NextOffset);
            _status = "Ready";
        }

        private async Task StartRoomAsync(bool create, string roomId)
        {
            var selectedProfile = RequireProfile();
            EnsureRoomSession();
            var sessionOptions = selectedProfile.BuildSessionOptions();
            var launchSpec = selectedProfile.BuildRoomLaunchSpec(
                sessionOptions,
                _request.Region,
                _request.ServerId);
            var spec = new ShooterRoomSessionLaunchSpec(
                _request.SessionToken,
                in launchSpec,
                (uint)sessionOptions.ControlledPlayerId,
                _request.Timeout);
            var controller = _roomController ?? throw new InvalidOperationException("Shooter room controller is unavailable.");
            _autoStartSuppressed = false;
            if (create)
            {
                await controller.StartCreateRoomAsync(spec, _lifetime.Token);
            }
            else
            {
                await controller.StartJoinRoomAsync(spec, roomId, _lifetime.Token);
            }

            _status = controller.CurrentState == ShooterRoomSessionState.InBattle ? "Joining battle" : "In lobby";
        }

        private void DriveAutomaticRoomFlow()
        {
            var controller = _roomController;
            var selectedProfile = profile;
            if (controller == null || selectedProfile == null || _busy || _returning || _battleLaunchRequested) return;

            var snapshot = controller.CurrentSnapshot;
            if (controller.CurrentState == ShooterRoomSessionState.InLobby)
            {
                var local = snapshot?.FindMember(controller.LocalPlayerId);
                if (selectedProfile.AutoReady && local?.LobbyReady != true)
                {
                    RunAsync("Setting ready", () => controller.SetReadyAsync(true, _lifetime.Token));
                }
                else if (selectedProfile.AutoStart && !_autoStartSuppressed &&
                         controller.IsLocalRoomOwner && snapshot?.CanStart == true)
                {
                    RunAsync("Starting loading", () => controller.BeginLoadingAsync(_lifetime.Token));
                }
            }
            else if (controller.CurrentState == ShooterRoomSessionState.LoadingAssets)
            {
                RunAsync("Loading battle assets", () => controller.PrepareAssetsAsync(_lifetime.Token));
            }
            else if (controller.CurrentState == ShooterRoomSessionState.WaitingForBattle)
            {
                EnsureBattleWait(controller);
            }
            else if (controller.CurrentState == ShooterRoomSessionState.InBattle)
            {
                LaunchBattleAsync();
            }
        }

        private void EnsureBattleWait(ShooterRoomSessionController controller)
        {
            if (_battleWaitTask != null) return;
            _battleWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _battleWaitTask = WaitForBattleAsync(controller, _battleWaitCancellation.Token);
        }

        private async Task WaitForBattleAsync(ShooterRoomSessionController controller, CancellationToken cancellationToken)
        {
            try
            {
                _status = "Waiting for battle";
                await controller.WaitForBattleStartAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _status = "Failed";
                _error = ex.Message;
            }
            finally
            {
                _battleWaitCancellation?.Dispose();
                _battleWaitCancellation = null;
                _battleWaitTask = null;
            }
        }

        private async void LaunchBattleAsync()
        {
            if (_battleLaunchRequested || _roomController == null || _roomLauncher == null) return;
            _battleLaunchRequested = true;
            _busy = true;
            _status = "Connecting battle";
            _error = string.Empty;
            var roomId = _roomController.CurrentRoomId;
            var launcher = _roomLauncher;
            _roomLauncher = null;
            DisposeRoomFlow(disposeLauncher: false);
            try
            {
                var options = RequireProfile().BuildLaunchOptions(
                    _request,
                    ShooterRemoteStateSyncLaunchMode.JoinRoom,
                    roomId);
                await ShooterRemoteStateSyncPlayModeHost.StartAsync(options, launcher);
                _status = "In battle";
            }
            catch (Exception ex)
            {
                _status = "Failed";
                _error = ex.Message;
            }
            finally
            {
                _busy = false;
            }
        }

        private void EnsureRoomSession()
        {
            if (_roomController != null) return;
            _roomLauncher = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
            _roomLauncher.Open(new ShooterClientNetworkEndpoint(_request.Host, _request.Port));
            _roomClient = new ShooterRoomGatewayRoomClient(_roomLauncher.GatewayConnection);
            var store = new ShooterRoomSessionStore(_roomClient);
            var session = new ShooterGatewayRoomSession(_roomClient, store);
            _roomController = new ShooterRoomSessionController(session, store);
            _roomController.StateChanged += HandleRoomStateChanged;
        }

        private void HandleRoomStateChanged(ShooterRoomSessionState state)
        {
            _status = FormatState(state);
        }

        private async Task<T> WithRoomClient<T>(Func<IDemoRoomDirectoryClient, Task<T>> action)
        {
            var launcher = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
            try
            {
                launcher.Open(new ShooterClientNetworkEndpoint(_request.Host, _request.Port));
                using var client = new ShooterRoomGatewayRoomClient(launcher.GatewayConnection);
                return await action(client);
            }
            finally
            {
                launcher.Dispose();
            }
        }

        private ShooterMultiplayerProfileSO RequireProfile()
        {
            return profile != null
                ? profile
                : throw new InvalidOperationException("Shooter multiplayer profile is not assigned.");
        }

        private async void RunAsync(string status, Func<Task> action)
        {
            if (_busy || _returning) return;
            _busy = true;
            _status = status;
            _error = string.Empty;
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _status = "Failed";
                _error = ex.Message;
            }
            finally
            {
                _busy = false;
            }
        }

        private async void CancelLoadingAsync()
        {
            var controller = _roomController;
            if (controller == null || !controller.IsLocalRoomOwner) return;
            _autoStartSuppressed = true;
            _status = "Cancelling loading";
            _error = string.Empty;
            CancelBattleWait();
            try
            {
                await controller.CancelLoadingAsync(_lifetime.Token);
                _status = "In lobby";
            }
            catch (Exception ex)
            {
                _status = "Failed";
                _error = ex.Message;
            }
        }

        private async void LeaveRoomAsync(bool returnToStarter)
        {
            var controller = _roomController;
            if (controller == null || !controller.CanLeaveCurrentRoom) return;
            _status = "Leaving room";
            _error = string.Empty;
            CancelBattleWait();
            try
            {
                await controller.LeaveRoomAsync(_lifetime.Token);
                DisposeRoomFlow(disposeLauncher: true);
                _status = "Ready";
                if (returnToStarter) LoadStarterScene();
                else await RefreshRoomsAsync();
            }
            catch (Exception ex)
            {
                _status = "Failed";
                _error = ex.Message;
                _returning = false;
            }
        }

        private async void ReturnToStarterAsync()
        {
            if (_returning) return;
            _returning = true;
            if (_roomController?.CanLeaveCurrentRoom == true)
            {
                LeaveRoomAsync(returnToStarter: true);
                return;
            }

            DisposeRoomFlow(disposeLauncher: true);
            LoadStarterScene();
            await Task.CompletedTask;
        }

        private void ReturnFromBattle()
        {
            ShooterRemoteStateSyncPlayModeHost.Stop();
            LoadStarterScene();
        }

        private void LoadStarterScene()
        {
            SceneManager.LoadScene(
                profile != null ? profile.StarterSceneName : "MultiplayerStarterScene",
                LoadSceneMode.Single);
        }

        private void CancelBattleWait()
        {
            _battleWaitCancellation?.Cancel();
        }

        private void DisposeRoomFlow(bool disposeLauncher)
        {
            CancelBattleWait();
            if (_roomController != null) _roomController.StateChanged -= HandleRoomStateChanged;
            _roomController?.Dispose();
            _roomController = null;
            _roomClient?.Dispose();
            _roomClient = null;
            if (disposeLauncher) _roomLauncher?.Dispose();
            if (disposeLauncher) _roomLauncher = null;
        }

        private void OnDestroy()
        {
            _lifetime.Cancel();
            DisposeRoomFlow(disposeLauncher: true);
            _lifetime.Dispose();
        }

        private static string FormatState(ShooterRoomSessionState state)
        {
            return state switch
            {
                ShooterRoomSessionState.CreatingRoom => "Creating room",
                ShooterRoomSessionState.JoiningRoom => "Joining room",
                ShooterRoomSessionState.InLobby => "In lobby",
                ShooterRoomSessionState.LoadingAssets => "Loading assets",
                ShooterRoomSessionState.WaitingForBattle => "Waiting for battle",
                ShooterRoomSessionState.InBattle => "In battle",
                ShooterRoomSessionState.LeavingRoom => "Leaving room",
                ShooterRoomSessionState.Failed => "Failed",
                _ => "Ready"
            };
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
