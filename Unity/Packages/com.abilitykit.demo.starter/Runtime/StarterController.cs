#nullable enable

using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Starter
{
    [DisallowMultipleComponent]
    public sealed class StarterController : MonoBehaviour
    {
        private const float WindowWidth = 420f;
        private const float WindowHeight = 340f;
        private const float LocalMenuWidth = 220f;
        private const float LocalMenuHeight = 176f;

        [SerializeField] private StarterConfigSO? config;

        private DemoMultiplayerAccountState? _accountState;
        private string _accountId = string.Empty;
        private string _guestId = string.Empty;
        private string _sessionToken = string.Empty;
        private string _status = "需要登录";
        private string _error = string.Empty;
        private bool _busy;
        private bool _loadingScene;
        private bool _showLocalMenu;

        private void Awake()
        {
            if (config == null)
            {
                _error = "多人启动器配置未赋值。";
                return;
            }

            _accountState = new DemoMultiplayerAccountState(
                config.DefaultAccountPrefix,
                config.DefaultGuestPrefix);
            _accountState.EnsureUniqueDefaultIdentity(ref _accountId, ref _guestId);
            if (StarterSessionState.TryRestore(
                    config.Host,
                    config.Port,
                    out var restoredAccountId,
                    out var restoredSessionToken))
            {
                _accountId = restoredAccountId;
                _sessionToken = restoredSessionToken;
                _accountState.RecordLogin(restoredAccountId);
                _status = "请选择玩法";
            }
        }

        private void OnGUI()
        {
            DrawLocalModeMenu();

            var rect = new Rect(
                Mathf.Max(16f, (Screen.width - WindowWidth) * 0.5f),
                Mathf.Max(16f, (Screen.height - WindowHeight) * 0.5f),
                WindowWidth,
                WindowHeight);
            GUILayout.BeginArea(rect, "AbilityKit 启动器", GUI.skin.window);
            GUILayout.Space(8f);
            GUILayout.Label("账号");
            var nextAccount = GUILayout.TextField(_accountId);
            if (!string.Equals(nextAccount, _accountId, StringComparison.Ordinal))
            {
                _accountId = nextAccount;
                ClearAuthentication();
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_busy && !_loadingScene && config != null;
            if (GUILayout.Button(IsAuthenticated ? "退出登录" : "登录", GUILayout.Height(36f)))
            {
                if (IsAuthenticated)
                {
                    ClearAuthentication();
                }
                else
                {
                    RunAsync("正在登录", LoginAsync);
                }
            }

            GUILayout.Space(12f);
            GUILayout.Label("选择玩法（多人）");
            GUI.enabled = GUI.enabled && IsAuthenticated;
            if (GUILayout.Button("MOBA（帧同步示例）", GUILayout.Height(48f)))
            {
                LaunchMoba();
            }

            if (GUILayout.Button("Shooter（状态同步示例）", GUILayout.Height(48f)))
            {
                LaunchShooter();
            }
            GUI.enabled = previousEnabled;

            GUILayout.Space(10f);
            GUILayout.Label(_busy ? $"状态：{_status}……" : $"状态：{_status}");
            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Label($"错误：{_error}");
            }
            GUILayout.EndArea();
        }

        private void DrawLocalModeMenu()
        {
            var buttonRect = new Rect(
                Mathf.Max(16f, Screen.width - LocalMenuWidth - 16f),
                16f,
                LocalMenuWidth,
                40f);
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_busy && !_loadingScene && config != null;
            if (GUI.Button(buttonRect, _showLocalMenu ? "收起本地模式" : "本地模式"))
            {
                _showLocalMenu = !_showLocalMenu;
            }

            if (_showLocalMenu)
            {
                var menuRect = new Rect(
                    buttonRect.x,
                    buttonRect.yMax + 8f,
                    LocalMenuWidth,
                    LocalMenuHeight);
                GUILayout.BeginArea(menuRect, "选择玩法（本地）", GUI.skin.window);
                GUILayout.Space(8f);
                if (GUILayout.Button("MOBA（帧同步示例）", GUILayout.Height(48f)))
                {
                    LaunchLocal(DemoGameplayId.Moba);
                }
                if (GUILayout.Button("Shooter（状态同步示例）", GUILayout.Height(48f)))
                {
                    LaunchLocal(DemoGameplayId.Shooter);
                }
                GUILayout.EndArea();
            }

            GUI.enabled = previousEnabled;
        }

        public string AuthenticatedAccountId => IsAuthenticated ? _accountId : string.Empty;
        public string SessionToken => IsAuthenticated ? _sessionToken : string.Empty;

        private bool IsAuthenticated =>
            _accountState?.HasSessionToken(_sessionToken, _accountId) == true;

        private async Task LoginAsync()
        {
            var selectedConfig = RequireConfig();
            _accountState!.EnsureUniqueDefaultIdentity(ref _accountId, ref _guestId);
            await AuthenticateAsync(
                selectedConfig.Host,
                selectedConfig.Port,
                _accountId,
                selectedConfig.RequestTimeout);
        }

        public void LaunchLocalAutomated(DemoGameplayId gameplay)
        {
            LaunchLocal(gameplay, "（自动化）");
        }

        public async Task LaunchMobaAutomatedAsync(
            string accountId,
            string host,
            int port,
            string region,
            string serverId,
            TimeSpan requestTimeout,
            bool suppressAutomaticLobbyActions = false)
        {
            var selectedConfig = RequireConfig();
            _accountId = string.IsNullOrWhiteSpace(accountId)
                ? throw new ArgumentException("自动化启动需要账号 ID。", nameof(accountId))
                : accountId.Trim();
            await AuthenticateAsync(host, port, _accountId, requestTimeout);
            DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Moba, new DemoMultiplayerLaunchRequest(
                host,
                port,
                region,
                serverId,
                _accountId,
                _sessionToken,
                requestTimeout,
                suppressAutomaticLobbyActions));
            DemoLaunchIntent.Request(new DemoLaunchRequest(
                DemoGameplayId.Moba,
                DemoLaunchMode.Multiplayer,
                selectedConfig.MobaProfileId));
            LoadGame(selectedConfig.MobaSceneName, "正在打开 MOBA（自动化）");
        }

        private async Task AuthenticateAsync(
            string host,
            int port,
            string accountId,
            TimeSpan requestTimeout)
        {
            var result = await DemoRoomGatewayAccountClient.LoginTcpAsync(
                host,
                port,
                accountId,
                requestTimeout);
            if (!result.Success || string.IsNullOrWhiteSpace(result.SessionToken))
            {
                throw new InvalidOperationException(result.Message);
            }

            _sessionToken = result.SessionToken;
            _accountId = result.AccountId;
            _accountState!.RecordLogin(result.AccountId);
            StarterSessionState.Record(
                host,
                port,
                result.AccountId,
                result.SessionToken);
            _status = "请选择玩法";
        }

        private void LaunchLocal(DemoGameplayId gameplay, string statusSuffix = "")
        {
            var selectedConfig = RequireConfig();
            DemoMultiplayerLaunchIntent.Clear();
            DemoLaunchIntent.Request(new DemoLaunchRequest(
                gameplay,
                DemoLaunchMode.Local,
                string.Empty));
            _showLocalMenu = false;
            var sceneName = gameplay == DemoGameplayId.Moba
                ? selectedConfig.MobaSceneName
                : selectedConfig.ShooterSceneName;
            LoadGame(sceneName, $"正在打开 {gameplay} 本地模式{statusSuffix}");
        }

        private void LaunchMoba()
        {
            var selectedConfig = RequireAuthenticatedConfig();
            DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Moba, new DemoMultiplayerLaunchRequest(
                selectedConfig.Host,
                selectedConfig.Port,
                selectedConfig.Region,
                selectedConfig.ServerId,
                _accountId,
                _sessionToken,
                selectedConfig.RequestTimeout));
            DemoLaunchIntent.Request(new DemoLaunchRequest(
                DemoGameplayId.Moba,
                DemoLaunchMode.Multiplayer,
                selectedConfig.MobaProfileId));
            LoadGame(selectedConfig.MobaSceneName, "正在打开 MOBA");
        }

        private void LaunchShooter()
        {
            var selectedConfig = RequireAuthenticatedConfig();
            DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Shooter, new DemoMultiplayerLaunchRequest(
                selectedConfig.Host,
                selectedConfig.Port,
                selectedConfig.Region,
                selectedConfig.ServerId,
                _accountId,
                _sessionToken,
                selectedConfig.RequestTimeout));
            DemoLaunchIntent.Request(new DemoLaunchRequest(
                DemoGameplayId.Shooter,
                DemoLaunchMode.Multiplayer,
                selectedConfig.ShooterProfileId));
            LoadGame(selectedConfig.ShooterSceneName, "正在打开 Shooter");
        }

        private void LoadGame(string sceneName, string status)
        {
            _loadingScene = true;
            _status = status;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private StarterConfigSO RequireAuthenticatedConfig()
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("需要先登录。");
            }

            return RequireConfig();
        }

        private StarterConfigSO RequireConfig()
        {
            return config != null
                ? config
                : throw new InvalidOperationException("多人启动器配置未赋值。");
        }

        private void ClearAuthentication()
        {
            _sessionToken = string.Empty;
            _accountState?.ClearSession();
            StarterSessionState.Clear();
            _status = "需要登录";
        }

        private async void RunAsync(string status, Func<Task> action)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _status = status;
            _error = string.Empty;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _status = "操作失败";
                _error = ex.Message;
            }
            finally
            {
                _busy = false;
            }
        }
    }

    internal static class StarterSessionState
    {
        private static string _host = string.Empty;
        private static int _port;
        private static string _accountId = string.Empty;
        private static string _sessionToken = string.Empty;

        public static void Record(
            string host,
            int port,
            string accountId,
            string sessionToken)
        {
            _host = host ?? string.Empty;
            _port = port;
            _accountId = accountId ?? string.Empty;
            _sessionToken = sessionToken ?? string.Empty;
        }

        public static bool TryRestore(
            string host,
            int port,
            out string accountId,
            out string sessionToken)
        {
            accountId = _accountId;
            sessionToken = _sessionToken;
            return string.Equals(_host, host, StringComparison.OrdinalIgnoreCase)
                   && _port == port
                   && !string.IsNullOrWhiteSpace(accountId)
                   && !string.IsNullOrWhiteSpace(sessionToken);
        }

        public static void Clear()
        {
            _host = string.Empty;
            _port = 0;
            _accountId = string.Empty;
            _sessionToken = string.Empty;
        }
    }
}
