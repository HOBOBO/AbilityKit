#nullable enable

using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Starter
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerStarterController : MonoBehaviour
    {
        private const float WindowWidth = 420f;
        private const float WindowHeight = 340f;

        [SerializeField] private MultiplayerStarterConfigSO? config;

        private DemoMultiplayerAccountState? _accountState;
        private string _accountId = string.Empty;
        private string _guestId = string.Empty;
        private string _sessionToken = string.Empty;
        private string _status = "Login required";
        private string _error = string.Empty;
        private bool _busy;
        private bool _loadingScene;

        private void Awake()
        {
            if (config == null)
            {
                _error = "Multiplayer starter config is not assigned.";
                return;
            }

            _accountState = new DemoMultiplayerAccountState(
                config.DefaultAccountPrefix,
                config.DefaultGuestPrefix);
            _accountState.EnsureUniqueDefaultIdentity(ref _accountId, ref _guestId);
            if (MultiplayerStarterSessionState.TryRestore(
                    config.Host,
                    config.Port,
                    out var restoredAccountId,
                    out var restoredSessionToken))
            {
                _accountId = restoredAccountId;
                _sessionToken = restoredSessionToken;
                _accountState.RecordLogin(restoredAccountId);
                _status = "Select a game";
            }
        }

        private void OnGUI()
        {
            var rect = new Rect(
                Mathf.Max(16f, (Screen.width - WindowWidth) * 0.5f),
                Mathf.Max(16f, (Screen.height - WindowHeight) * 0.5f),
                WindowWidth,
                WindowHeight);
            GUILayout.BeginArea(rect, "AbilityKit Multiplayer", GUI.skin.window);
            GUILayout.Space(8f);
            GUILayout.Label("Account");
            var nextAccount = GUILayout.TextField(_accountId);
            if (!string.Equals(nextAccount, _accountId, StringComparison.Ordinal))
            {
                _accountId = nextAccount;
                ClearAuthentication();
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_busy && !_loadingScene && config != null;
            if (GUILayout.Button(IsAuthenticated ? "Logout" : "Login", GUILayout.Height(36f)))
            {
                if (IsAuthenticated)
                {
                    ClearAuthentication();
                }
                else
                {
                    RunAsync("Logging in", LoginAsync);
                }
            }

            GUILayout.Space(12f);
            GUILayout.Label("Game");
            GUI.enabled = GUI.enabled && IsAuthenticated;
            if (GUILayout.Button("MOBA", GUILayout.Height(48f)))
            {
                LaunchMoba();
            }

            if (GUILayout.Button("Shooter", GUILayout.Height(48f)))
            {
                LaunchShooter();
            }
            GUI.enabled = previousEnabled;

            GUILayout.Space(10f);
            GUILayout.Label(_busy ? $"Status: {_status}..." : $"Status: {_status}");
            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Label($"Error: {_error}");
            }
            GUILayout.EndArea();
        }

        private bool IsAuthenticated =>
            _accountState?.HasSessionToken(_sessionToken, _accountId) == true;

        private async Task LoginAsync()
        {
            var selectedConfig = RequireConfig();
            _accountState!.EnsureUniqueDefaultIdentity(ref _accountId, ref _guestId);
            var result = await DemoRoomGatewayAccountClient.LoginTcpAsync(
                selectedConfig.Host,
                selectedConfig.Port,
                _accountId,
                selectedConfig.RequestTimeout);
            if (!result.Success || string.IsNullOrWhiteSpace(result.SessionToken))
            {
                throw new InvalidOperationException(result.Message);
            }

            _sessionToken = result.SessionToken;
            _accountId = result.AccountId;
            _accountState.RecordLogin(result.AccountId);
            MultiplayerStarterSessionState.Record(
                selectedConfig.Host,
                selectedConfig.Port,
                result.AccountId,
                result.SessionToken);
            _status = "Select a game";
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
            LoadGame(selectedConfig.MobaSceneName, "Opening MOBA");
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
            LoadGame(selectedConfig.ShooterSceneName, "Opening Shooter");
        }

        private void LoadGame(string sceneName, string status)
        {
            _loadingScene = true;
            _status = status;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private MultiplayerStarterConfigSO RequireAuthenticatedConfig()
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("Login is required.");
            }

            return RequireConfig();
        }

        private MultiplayerStarterConfigSO RequireConfig()
        {
            return config != null
                ? config
                : throw new InvalidOperationException("Multiplayer starter config is not assigned.");
        }

        private void ClearAuthentication()
        {
            _sessionToken = string.Empty;
            _accountState?.ClearSession();
            MultiplayerStarterSessionState.Clear();
            _status = "Login required";
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
                _status = "Failed";
                _error = ex.Message;
            }
            finally
            {
                _busy = false;
            }
        }
    }

    internal static class MultiplayerStarterSessionState
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
