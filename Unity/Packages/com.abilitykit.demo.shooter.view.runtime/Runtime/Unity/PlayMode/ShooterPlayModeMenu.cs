#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.View.Hosting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    [DisallowMultipleComponent]
    public sealed class ShooterPlayModeMenu : MonoBehaviour
    {
        private const float Width = 440f;
        private const float TextFieldWidth = 180f;
        private const string DefaultTemplateId = ShooterSyncTemplateIds.PredictRollbackAuthority;
        private static readonly string[] EnemyBudgetLabels =
        {
            "Playable 512",
            "Stress 2k",
            "Extreme 8k"
        };

        [Header("GUI")]
        [SerializeField] private bool showMenu = true;
        [SerializeField] private Rect windowRect = new Rect(12f, 12f, Width, 720f);

        [Header("Session")]
        [SerializeField] private string templateId = DefaultTemplateId;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private int playerCount = 2;
        [SerializeField] private int controlledPlayerId = 1;
        [SerializeField] private float worldScale = 1f;
        [SerializeField] private int enemyBudget = ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget;
        [SerializeField] private bool enableAuthorityComparison;

        [Header("Rendering")]
        [SerializeField] private ShooterUnityViewRenderBackend renderBackend = ShooterUnityViewRenderBackendCatalog.DefaultBackend;

        [Header("Gateway")]
        [SerializeField] private string host = ShooterRemoteStateSyncDefaults.DefaultHost;
        [SerializeField] private int port = ShooterRemoteStateSyncDefaults.DefaultPort;
        [SerializeField] private string region = ShooterRemoteStateSyncDefaults.DefaultRegion;
        [SerializeField] private string serverId = ShooterRemoteStateSyncDefaults.DefaultServerId;
        [SerializeField] private string sessionToken = string.Empty;
        [SerializeField] private string guestId = string.Empty;
        [SerializeField] private string accountId = string.Empty;
        [SerializeField] private int timeoutSeconds = 5;

        [Header("Room")]
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string roomTitle = "Unity Shooter Room";
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private int roomListLimit = 10;

        private readonly DemoMultiplayerAccountState _accountState = new DemoMultiplayerAccountState(
            "unity-account",
            "unity-guest",
            ShooterRemoteStateSyncDefaults.DefaultSessionToken);
        private readonly DemoRoomListState<ShooterGatewayRoomSummary> _roomState = new DemoRoomListState<ShooterGatewayRoomSummary>();
        private string _status = "Ready";
        private string _error = string.Empty;
        private bool _busy;
        private bool _multiplayerEntry;

        private void Awake()
        {
            _multiplayerEntry = DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Shooter,
                out _);
            EnsureUniqueDefaultIdentity();
        }

        private void OnGUI()
        {
            if (!showMenu)
            {
                if (GUI.Button(new Rect(12f, 12f, 130f, 28f), "Shooter Menu"))
                {
                    showMenu = true;
                }

                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Shooter Play Mode");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            DrawSessionSettings();
            DrawRenderingSettings();
            if (_multiplayerEntry)
            {
                DrawGatewayControls();
                DrawRemoteControls();
                DrawRoomList();
            }
            else
            {
                DrawLocalControls();
            }
            DrawStatus();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hide"))
            {
                showMenu = false;
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, Width, 24f));
        }

        private void DrawSessionSettings()
        {
            GUILayout.Label("Session");
            templateId = TextField("Template", templateId);
            randomSeed = IntField("Seed", randomSeed);
            playerCount = Math.Max(1, IntField("Players", playerCount));
            controlledPlayerId = Math.Min(Math.Max(1, IntField("Player", controlledPlayerId)), playerCount);
            worldScale = Math.Max(0.01f, FloatField("Scale", worldScale));
            enableAuthorityComparison = GUILayout.Toggle(enableAuthorityComparison, "Authority comparison");
            enemyBudget = EnemyBudgetForIndex(GUILayout.SelectionGrid(
                EnemyBudgetIndex(enemyBudget),
                EnemyBudgetLabels,
                EnemyBudgetLabels.Length));
            GUILayout.Label($"Enemies: {enemyBudget}");
        }

        private void DrawRenderingSettings()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Rendering");
            var selected = GUILayout.SelectionGrid(
                ShooterUnityViewRenderBackendCatalog.IndexOf(renderBackend),
                ShooterUnityViewRenderBackendCatalog.GetDisplayNames(),
                Math.Max(1, ShooterUnityViewRenderBackendCatalog.Count));
            var selectedDescriptor = ShooterUnityViewRenderBackendCatalog.Get(selected);
            renderBackend = selectedDescriptor.Backend;
            var effectiveBackend = ShooterUnityViewRenderBackendCatalog.Normalize(renderBackend);
            var effectiveDescriptor = ShooterUnityViewRenderBackendCatalog.Get(effectiveBackend);

            GUILayout.Label($"Selected: {selectedDescriptor.DisplayName}");
            GUILayout.Label(selectedDescriptor.CapabilitySummary);
            GUILayout.Label($"Density: {(selectedDescriptor.IsHighDensity ? "High" : "Debug")}  DOTS packages: {(selectedDescriptor.RequiresDotsPackages ? "Required" : "Not required")}");
            GUILayout.Label(selectedDescriptor.IsAvailable
                ? $"Effective: {effectiveDescriptor.DisplayName}"
                : $"Effective: {effectiveDescriptor.DisplayName} until DOTS packages are installed");
            GUILayout.Label($"Local Backend: {ShooterUnityViewRenderBackendCatalog.Get(ShooterPlayModeSessionHost.ViewBackend).DisplayName}");
            GUILayout.Label($"Remote Backend: {ShooterUnityViewRenderBackendCatalog.Get(ShooterRemoteStateSyncPlayModeHost.ViewBackend).DisplayName}");
        }

        private void DrawLocalControls()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Single Player");
            GUILayout.BeginHorizontal();
            GUI.enabled = !_busy;
            if (GUILayout.Button("Start Local Frame Sync"))
            {
                RunSync("local start", StartLocal);
            }

            if (GUILayout.Button("Stop Local"))
            {
                ShooterPlayModeSessionHost.Stop();
                SetStatus("Local session stopped.");
            }

            if (GUILayout.Button("Rebuild Views"))
            {
                var effectiveBackend = ShooterUnityViewRenderBackendCatalog.Normalize(renderBackend);
                ShooterPlayModeSessionHost.SetViewBackend(effectiveBackend);
                ShooterRemoteStateSyncPlayModeHost.SetViewBackend(effectiveBackend);
                ShooterPlayModeSessionHost.RebuildViews();
                ShooterRemoteStateSyncPlayModeHost.RebuildViews();
                SetStatus("Views rebuilt.");
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Local: {(ShooterPlayModeSessionHost.IsRunning ? "Running" : "Stopped")} Step={ShooterPlayModeSessionHost.StepCount} Render={ShooterPlayModeSessionHost.RenderCount} Drop={ShooterPlayModeSessionHost.DroppedCatchUpTicks}");
            GUILayout.Label("Controls: WASD / Arrow Keys move, mouse aims.");
            GUILayout.Label("Fire: Space / Left Mouse / J primary, K spread, L twin.");
        }

        private void DrawGatewayControls()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Gateway");
            host = TextField("Host", host);
            port = Math.Max(1, IntField("Port", port));
            region = TextField("Region", region);
            serverId = TextField("Server", serverId);
            var nextAccountId = TextField("Account", accountId);
            if (!string.Equals(NormalizeOrDefault(nextAccountId, string.Empty), NormalizeOrDefault(accountId, string.Empty), StringComparison.Ordinal))
            {
                accountId = nextAccountId;
                ClearSession();
            }
            else
            {
                accountId = nextAccountId;
            }
            timeoutSeconds = Math.Max(1, IntField("Timeout", timeoutSeconds));
            GUILayout.Label(HasSessionToken() ? "Session: logged in" : "Session: login required");

            GUILayout.BeginHorizontal();
            GUI.enabled = !_busy;
            if (GUILayout.Button("Login Account"))
            {
                RunAsync("account login", AccountLoginAsync);
            }

            if (GUILayout.Button("List Rooms"))
            {
                RunAsync("list rooms", ListRoomsAsync);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(string.IsNullOrWhiteSpace(_accountState.LoggedAccountId) ? $"Account: {NormalizeOrDefault(accountId, CreateDefaultAccountId())}" : $"Account: {_accountState.LoggedAccountId}");
        }

        private void DrawRemoteControls()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Multiplayer Room");
            roomId = TextField("Room Id", roomId);
            roomTitle = TextField("Title", roomTitle);
            maxPlayers = Math.Max(1, IntField("Max Players", maxPlayers));
            roomListLimit = Math.Max(1, IntField("List Limit", roomListLimit));

            GUILayout.BeginHorizontal();
            GUI.enabled = !_busy;
            if (GUILayout.Button("Create Room"))
            {
                RunAsync("create room", () => StartRemoteAsync(ShooterRemoteStateSyncLaunchMode.CreateNew, string.Empty));
            }

            if (GUILayout.Button("Join Room Id"))
            {
                RunAsync("join room", () => StartRemoteAsync(ShooterRemoteStateSyncLaunchMode.JoinRoom, roomId));
            }

            if (GUILayout.Button("Reconnect"))
            {
                RunAsync("reconnect", () => StartRemoteAsync(ShooterRemoteStateSyncLaunchMode.RestoreOnly, string.Empty));
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !_busy
                && ShooterRemoteStateSyncPlayModeHost.IsRunning
                && !ShooterRemoteStateSyncPlayModeHost.IsPaused
                && !ShooterRemoteStateSyncPlayModeHost.IsAutoReconnecting;
            if (GUILayout.Button("Pause Remote"))
            {
                ShooterRemoteStateSyncPlayModeHost.PauseForReconnectValidation();
                SetStatus("Remote session paused for reconnect validation.");
            }

            GUI.enabled = !_busy && ShooterRemoteStateSyncPlayModeHost.IsPaused;
            if (GUILayout.Button("Resume Remote"))
            {
                RunAsync("resume remote", ResumeRemoteAsync);
            }

            GUI.enabled = !_busy && (ShooterRemoteStateSyncPlayModeHost.IsRunning || ShooterRemoteStateSyncPlayModeHost.IsPaused || ShooterRemoteStateSyncPlayModeHost.IsStarting);
            if (GUILayout.Button("Stop Remote"))
            {
                ShooterRemoteStateSyncPlayModeHost.Stop();
                SetStatus("Remote session stopped.");
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Remote: {RemoteStateLabel()}");
            GUILayout.Label($"Initial Sync: {ShooterRemoteStateSyncPlayModeHost.LastInitialFullStateSyncApplyResult}");
            DrawMultiplayerLoadingStatus();
        }

        private static void DrawMultiplayerLoadingStatus()
        {
            var loading = ShooterMultiplayerLoadingStatus.Current;
            if (loading.LocalProgress <= 0 && string.IsNullOrWhiteSpace(loading.Stage) && loading.Snapshot == null)
            {
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label($"Loading: {loading.LocalProgress}%  {loading.Stage}");
            DrawProgressBar(loading.LocalProgress);

            var players = loading.Snapshot?.Players;
            if (players == null || players.Count == 0)
            {
                return;
            }

            GUILayout.Label("Authoritative player readiness:");
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var state = player.AssetsLoaded ? "Ready" : "Loading";
                GUILayout.Label($"P{player.PlayerId} {player.AccountId}: {player.LoadingProgress}% ({state})");
                DrawProgressBar(player.LoadingProgress);
            }
        }

        private static void DrawProgressBar(int progress)
        {
            var value = Mathf.Clamp(progress, 0, 100);
            var rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, string.Empty);
            var fill = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * value / 100f, rect.height - 4f);
            if (fill.width > 0f) GUI.Box(fill, string.Empty);
            GUI.Label(rect, $"{value}%", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawRoomList()
        {
            GUILayout.Space(6f);
            GUILayout.Label($"Rooms ({_roomState.Count})");

            if (_roomState.Count == 0)
            {
                GUILayout.Label("No rooms loaded.");
                return;
            }

            for (var i = 0; i < _roomState.Rooms.Count; i++)
            {
                var room = _roomState.Rooms[i];
                GUILayout.BeginHorizontal();
                var selected = _roomState.SelectedIndex == i;
                if (GUILayout.Toggle(selected, string.Empty, GUILayout.Width(20f)) != selected)
                {
                    SelectRoom(i);
                }

                GUILayout.Label($"{room.DisplayName} {room.PlayerCount}/{room.MaxPlayers} {(room.HasOpenSlot ? "Open" : "Full")}");
                GUI.enabled = !_busy && room.HasOpenSlot;
                if (GUILayout.Button("Join", GUILayout.Width(72f)))
                {
                    SelectRoom(i);
                    RunAsync("join selected room", () => StartRemoteAsync(ShooterRemoteStateSyncLaunchMode.JoinRoom, room.RoomId));
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawStatus()
        {
            GUILayout.Space(6f);
            GUILayout.Label(_busy ? $"Busy: {_status}" : $"Status: {_status}");
            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Label($"Error: {_error}");
            }

            if (_multiplayerEntry && !_busy && GUILayout.Button("Exit to Multiplayer Starter", GUILayout.Height(28f)))
            {
                ShooterRemoteStateSyncPlayModeHost.Stop();
                SceneManager.LoadScene("MultiplayerStarterScene", LoadSceneMode.Single);
            }
        }

        private void StartLocal()
        {
            ShooterRemoteStateSyncPlayModeHost.Stop();
            ShooterPlayModeSessionHost.SetViewBackend(ShooterUnityViewRenderBackendCatalog.Normalize(renderBackend));
            ShooterPlayModeSessionHost.Start(BuildSessionOptions());
            SetStatus("Local frame-sync session started.");
        }

        private async Task GuestLoginAsync()
        {
            EnsureUniqueDefaultIdentity();
            var result = await WithRoomClient(client => client.GuestLoginAsync(
                new ShooterGatewayGuestLoginRequest(NormalizeOrDefault(guestId, CreateDefaultGuestId())),
                Timeout()));

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            sessionToken = result.SessionToken;
            _accountState.RecordLogin(result.AccountId);
            SetStatus($"Guest login ok: {result.AccountId}");
        }

        private async Task AccountLoginAsync()
        {
            EnsureUniqueDefaultIdentity();
            var result = await WithRoomClient(client => client.AccountLoginAsync(
                new ShooterGatewayAccountLoginRequest(NormalizeOrDefault(accountId, CreateDefaultAccountId())),
                Timeout()));

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            sessionToken = result.SessionToken;
            _accountState.RecordLogin(result.AccountId);
            SetStatus($"Account login ok: {result.AccountId}");
        }

        private async Task ListRoomsAsync()
        {
            await EnsureAccountLoginAsync();
            var result = await WithRoomClient(client => client.ListRoomsAsync(
                new ShooterGatewayListRoomsRequest(SessionToken(), Region(), ServerId(), _roomState.NextOffset, Math.Max(1, roomListLimit)),
                Timeout()));

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            _roomState.ReplaceRooms(result.Rooms, result.NextOffset);
            if (_roomState.Count > 0)
            {
                SelectRoom(0);
            }

            SetStatus($"Loaded {_roomState.Count} room(s).");
        }

        private async Task StartRemoteAsync(ShooterRemoteStateSyncLaunchMode mode, string selectedRoomId)
        {
            await EnsureAccountLoginAsync();
            ShooterPlayModeSessionHost.Stop();
            ShooterRemoteStateSyncPlayModeHost.SetViewBackend(ShooterUnityViewRenderBackendCatalog.Normalize(renderBackend));

            var sessionOptions = BuildSessionOptions();
            var options = new ShooterRemoteStateSyncLaunchOptions(
                sessionOptions,
                Endpoint(),
                SessionToken(),
                Region(),
                ServerId(),
                mode,
                Timeout(),
                selectedRoomId,
                BuildRoomLaunchSpec(sessionOptions));

            var launch = await ShooterRemoteStateSyncPlayModeHost.StartAsync(options);
            var flow = launch.Flow;
            roomId = flow.RoomId;
            SetStatus($"Remote {mode} ok: room={flow.RoomId} battle={flow.BattleId}");
        }

        private async Task ResumeRemoteAsync()
        {
            var launch = await ShooterRemoteStateSyncPlayModeHost.ResumeFromPauseAsync();
            var flow = launch.Flow;
            roomId = flow.RoomId;
            SetStatus($"Remote resumed through reconnect: room={flow.RoomId} battle={flow.BattleId}");
        }

        private static string RemoteStateLabel()
        {
            if (ShooterRemoteStateSyncPlayModeHost.IsWaitingForInitialFullStateSync)
            {
                return "Syncing Latest State";
            }

            if (ShooterRemoteStateSyncPlayModeHost.IsStarting)
            {
                return "Starting";
            }

            if (ShooterRemoteStateSyncPlayModeHost.IsAutoReconnecting)
            {
                return "Auto Reconnecting";
            }

            if (ShooterRemoteStateSyncPlayModeHost.IsPaused)
            {
                return "Paused";
            }

            return ShooterRemoteStateSyncPlayModeHost.IsRunning ? "Running" : "Stopped";
        }

        private ShooterPlayModeSessionOptions BuildSessionOptions()
        {
            var templateOptions = ShooterPlayModeSessionOptions.FromTemplate(
                ShooterAcceptanceCatalog.GetSyncTemplate(NormalizeOrDefault(templateId, DefaultTemplateId)),
                randomSeed,
                Math.Min(Math.Max(1, controlledPlayerId), Math.Max(1, playerCount)),
                Math.Max(0.01f, worldScale));

            return new ShooterPlayModeSessionOptions(
                templateOptions.SyncModel,
                templateOptions.TickRate,
                Math.Max(1, playerCount),
                templateOptions.RandomSeed,
                templateOptions.ControlledPlayerId,
                enableAuthorityComparison,
                templateOptions.LatencyMs,
                templateOptions.JitterMs,
                templateOptions.PacketLossRate,
                templateOptions.ReorderRate,
                templateOptions.BandwidthKbps,
                templateOptions.WorldScale,
                templateOptions.NetworkName,
                templateOptions.SyncTemplateId,
                ShooterPlayModeSessionOptions.CreatePlayModeScenario(enemyBudget));
        }

        private static int EnemyBudgetIndex(int value)
        {
            if (value >= ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget)
            {
                return 2;
            }

            return value >= ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget ? 1 : 0;
        }

        private static int EnemyBudgetForIndex(int index)
        {
            return index switch
            {
                1 => ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget,
                2 => ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget,
                _ => ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget
            };
        }

        private ShooterRoomLaunchSpec BuildRoomLaunchSpec(ShooterPlayModeSessionOptions sessionOptions)
        {
            var defaults = ShooterRoomLaunchSpec.CreateDefault($"unity-{sessionOptions.ControlledPlayerId}");
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(NormalizeOrDefault(sessionOptions.SyncTemplateId, DefaultTemplateId));
            var tags = new Dictionary<string, string>(defaults.Tags, StringComparer.Ordinal)
            {
                [ShooterRoomLaunchTagKeys.SyncTemplateId] = template.Id,
                [ShooterRoomLaunchTagKeys.SyncModel] = ((int)template.SyncModel).ToString(),
                [ShooterRoomLaunchTagKeys.NetworkEnvironmentId] = template.NetworkEnvironmentId,
                [ShooterRoomLaunchTagKeys.CarrierName] = template.ExpectedCarrierName,
                [ShooterRoomLaunchTagKeys.EnableAuthoritativeWorld] = template.EnableAuthoritativeWorld.ToString(),
                [ShooterRoomLaunchTagKeys.InterpolationEnabled] = template.ExpectsInterpolationDiagnostics.ToString(),
                [ShooterRoomLaunchTagKeys.InputDelayFrames] = "0",
                [ShooterRoomLaunchTagKeys.RandomSeed] = sessionOptions.RandomSeed.ToString(),
                [ShooterRoomLaunchTagKeys.DurationFrames] = sessionOptions.GameplayScenario.BattleFlow.DurationFrames.ToString()
            };

            return new ShooterRoomLaunchSpec(
                Region(),
                ServerId(),
                NormalizeOrDefault(roomTitle, defaults.RoomTitle),
                Math.Max(1, maxPlayers),
                defaults.GameplayId,
                defaults.RuleSetId,
                defaults.ConfigVersion,
                defaults.ProtocolVersion,
                defaults.WorldType,
                defaults.ClientId,
                tags,
                template.Id,
                (int)template.SyncModel,
                template.NetworkEnvironmentId,
                template.ExpectedCarrierName,
                template.EnableAuthoritativeWorld,
                template.ExpectsInterpolationDiagnostics,
                inputDelayFrames: 0);
        }

        private async Task<T> WithRoomClient<T>(Func<ShooterRoomGatewayRoomClient, Task<T>> action)
        {
            var launcher = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
            try
            {
                launcher.Open(Endpoint());
                var client = new ShooterRoomGatewayRoomClient(launcher.GatewayConnection);
                return await action(client);
            }
            finally
            {
                launcher.Dispose();
            }
        }

        private void SelectRoom(int index)
        {
            if (_roomState.TrySelect(index, out var room))
            {
                roomId = room.RoomId;
            }
        }

        private void RunSync(string actionName, Action action)
        {
            _busy = true;
            _error = string.Empty;
            _status = actionName;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _status = $"{actionName} failed";
            }
            finally
            {
                _busy = false;
            }
        }

        private async void RunAsync(string actionName, Func<Task> action)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _error = string.Empty;
            _status = actionName;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _status = $"{actionName} failed";
            }
            finally
            {
                _busy = false;
            }
        }

        private void SetStatus(string status)
        {
            _error = string.Empty;
            _status = status;
        }

        private ShooterClientNetworkEndpoint Endpoint()
        {
            return new ShooterClientNetworkEndpoint(NormalizeOrDefault(host, ShooterRemoteStateSyncDefaults.DefaultHost), Math.Max(1, port));
        }

        private TimeSpan Timeout()
        {
            return TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        }

        private string SessionToken()
        {
            return NormalizeOrDefault(sessionToken, string.Empty);
        }

        private async Task EnsureAccountLoginAsync()
        {
            EnsureUniqueDefaultIdentity();
            if (HasSessionToken())
            {
                return;
            }

            var result = await WithRoomClient(client => client.AccountLoginAsync(
                new ShooterGatewayAccountLoginRequest(NormalizeOrDefault(accountId, CreateDefaultAccountId())),
                Timeout()));

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            sessionToken = result.SessionToken;
            _accountState.RecordLogin(result.AccountId);
            SetStatus($"Account login ok: {result.AccountId}");
        }

        private bool HasSessionToken()
        {
            return _accountState.HasSessionToken(SessionToken(), NormalizeOrDefault(accountId, CreateDefaultAccountId()));
        }

        private void ClearSession()
        {
            sessionToken = string.Empty;
            _accountState.ClearSession();
        }

        private void EnsureUniqueDefaultIdentity()
        {
            _accountState.EnsureUniqueDefaultIdentity(ref accountId, ref guestId);
        }

        private string CreateDefaultAccountId()
        {
            return _accountState.CreateDefaultAccountId();
        }

        private string CreateDefaultGuestId()
        {
            return _accountState.CreateDefaultGuestId();
        }

        private string Region()
        {
            return NormalizeOrDefault(region, ShooterRemoteStateSyncDefaults.DefaultRegion);
        }

        private string ServerId()
        {
            return NormalizeOrDefault(serverId, ShooterRemoteStateSyncDefaults.DefaultServerId);
        }

        private static string NormalizeOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string TextField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(96f));
            var next = GUILayout.TextField(value ?? string.Empty, GUILayout.Width(TextFieldWidth));
            GUILayout.EndHorizontal();
            return next;
        }

        private static int IntField(string label, int value)
        {
            var text = TextField(label, value.ToString());
            return int.TryParse(text, out var parsed) ? parsed : value;
        }

        private static float FloatField(string label, float value)
        {
            var text = TextField(label, value.ToString("0.###"));
            return float.TryParse(text, out var parsed) ? parsed : value;
        }
    }
}
