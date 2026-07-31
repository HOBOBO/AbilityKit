using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 正式多人大厅 Feature：从 <see cref="MultiplayerRoomFlowController"/> 投影状态，
    /// 提供创建房间、加入房间、选英雄、Ready 等操作。
    /// </summary>
    public sealed class FormalLobbyFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private MultiplayerRoomFlowController _controller;
        private GatewayMultiplayerRoomSession _session;
        private LobbyBattleEntrySelection _selection;
        private IMultiplayerGatewayRuntime _gatewayRuntime;
        private BattleGatewayConfigSO _gatewayConfig;
        private readonly MultiplayerBattleEntryGate _battleEntryGate = new MultiplayerBattleEntryGate();
        private bool _show = true;
        private string _joinRoomId = string.Empty;
        private int _selectedHeroId = 10001;
        private readonly List<MultiplayerRoomFlowState> _stateHistory = new List<MultiplayerRoomFlowState>(16);

        public void OnAttach(in GamePhaseContext ctx)
        {
            _controller = ResolveController(ctx);
            if (ctx.Entry != null)
            {
                ctx.Entry.TryGet(out _gatewayConfig);
                ctx.Entry.TryGet(out _session);
                ctx.Entry.TryGet(out _selection);
                ctx.Entry.TryGet(out _gatewayRuntime);
            }

            if (_controller != null)
            {
                _controller.StateChanged += HandleStateChanged;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_controller != null)
            {
                _controller.StateChanged -= HandleStateChanged;
            }

            _battleEntryGate.Reset();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
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
                    _session));
            }
            catch
            {
                _battleEntryGate.Reset();
                throw;
            }
        }

        private void HandleStateChanged(MultiplayerRoomFlowState state)
        {
            _stateHistory.Add(state);
        }

        private static MultiplayerRoomFlowController ResolveController(in GamePhaseContext ctx)
        {
            // Gateway 模块是可选装配；未配置时正式大厅保持不可用而非中断 Lobby。
            if (ctx.Entry == null) return null;
            return ctx.Entry.TryGet(out MultiplayerRoomFlowController controller)
                ? controller
                : null;
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

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            if (ctx.Entry == null) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle) return;

            if (!ShouldShowFlowWindow(_selection)) return;

            if (_controller == null)
            {
                _controller = ResolveController(ctx);
                if (_controller == null) return;
                _controller.StateChanged += HandleStateChanged;
            }

            GUILayout.BeginArea(new Rect(390, 10, 380, 460), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("正式多人大厅");
            if (GUILayout.Button("Exit", GUILayout.Width(56)))
            {
                ExitToStarter();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label($"连接: {_gatewayRuntime?.ConnectionState}");
            if (_gatewayRuntime != null &&
                _gatewayRuntime.RecoveryState != MultiplayerRecoveryState.None)
            {
                GUILayout.Label($"恢复: {_gatewayRuntime.RecoveryState}");
                if (_gatewayRuntime.RecoveryState == MultiplayerRecoveryState.ReconnectExhausted &&
                    GUILayout.Button("重新连接", GUILayout.Height(28)))
                {
                    _gatewayRuntime.ResetReconnect();
                }
            }
            GUILayout.Label($"状态: {_controller.CurrentState}");
            if (!string.IsNullOrEmpty(_controller.LastError))
            {
                GUILayout.Label($"错误: {_controller.LastError}");
            }

            GUILayout.Space(6);
            DrawByState();

            GUILayout.EndArea();
        }

        internal static bool ShouldShowFlowWindow(LobbyBattleEntrySelection selection)
        {
            return selection?.IsRemoteSelected == true;
        }

        private void DrawByState()
        {
            var state = _controller.CurrentState;
            switch (state)
            {
                case MultiplayerRoomFlowState.Idle:
                    DrawIdle();
                    break;
                case MultiplayerRoomFlowState.InLobby:
                    DrawLobby();
                    break;
                case MultiplayerRoomFlowState.LoadingAssets:
                    GUILayout.Label("正在加载战斗场景和资源...");
                    DrawLocalLoadingProgress();
                    DrawLoadingDeadline(_controller.CurrentSnapshot);
                    DrawPlayers(_controller.CurrentSnapshot);
                    var previousEnabled = GUI.enabled;
                    GUI.enabled = previousEnabled && _controller.IsLocalRoomOwner;
                    if (GUILayout.Button("取消加载", GUILayout.Height(28)))
                    {
                        _ = _controller.CancelLoadingAsync();
                    }
                    GUI.enabled = previousEnabled;
                    break;
                case MultiplayerRoomFlowState.WaitingForBattle:
                    GUILayout.Label("等待其他客户端与战斗服就绪...");
                    DrawLocalLoadingProgress();
                    DrawLoadingDeadline(_controller.CurrentSnapshot);
                    DrawPlayers(_controller.CurrentSnapshot);
                    break;
                case MultiplayerRoomFlowState.Failed:
                    DrawFailed();
                    break;
                default:
                    GUILayout.Label($"处理中... ({state})");
                    break;
            }
        }

        private void DrawIdle()
        {
            var spec = BuildLaunchSpec(_gatewayConfig);

            GUILayout.Label("房间标题:");
            spec.RoomTitle = GUILayout.TextField(spec.RoomTitle);

            GUILayout.Label("加入房间 Id:");
            _joinRoomId = GUILayout.TextField(_joinRoomId);

            GUILayout.Space(4);
            var canSubmit = _gatewayRuntime?.ConnectionState == AbilityKit.Network.Abstractions.ConnectionState.Connected;
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canSubmit;
            if (GUILayout.Button("创建房间", GUILayout.Height(30)))
            {
                _ = _controller.StartCreateRoomAsync(spec);
            }

            if (GUILayout.Button("加入房间", GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(_joinRoomId))
                {
                    _ = _controller.StartJoinRoomAsync(spec, _joinRoomId.Trim());
                }
            }

            if (GUILayout.Button("Restore Room", GUILayout.Height(30)))
            {
                var fallbackPlayerId = _gatewayConfig != null
                    ? _gatewayConfig.RestoreFallbackPlayerId
                    : 1u;
                _ = _controller.RestoreAsync(spec, fallbackPlayerId == 0u ? 1u : fallbackPlayerId);
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawLobby()
        {
            var snapshot = _controller.CurrentSnapshot;
            if (snapshot != null)
            {
                GUILayout.Label($"房间: {snapshot.RoomId} ({snapshot.NumericRoomId})");
                GUILayout.Label($"阶段: {snapshot.Phase}");
                GUILayout.Label($"可开始: {snapshot.CanStart}");
                GUILayout.Label($"房主: {snapshot.OwnerAccountId}");
                DrawPlayers(snapshot);
            }

            GUILayout.Space(4);
            GUILayout.Label("英雄 Id:");
            var heroText = GUILayout.TextField(_selectedHeroId.ToString());
            if (int.TryParse(heroText, out var parsed))
            {
                _selectedHeroId = parsed;
            }

            GUILayout.Space(4);
            if (GUILayout.Button("选择英雄", GUILayout.Height(28)))
            {
                var loadout = new MultiplayerLoadoutSpec(
                    _selectedHeroId, teamId: 1, spawnPointId: 0, level: 1,
                    attributeTemplateId: 0, basicAttackSkillId: 0, skillIds: null);
                _ = _controller.PickHeroAsync(loadout);
            }

            if (GUILayout.Button("准备", GUILayout.Height(28)))
            {
                _ = _controller.SetReadyAsync(true);
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled &&
                          _controller.IsLocalRoomOwner &&
                          snapshot?.CanStart == true;
            if (GUILayout.Button("开始加载", GUILayout.Height(28)))
            {
                _ = _controller.BeginLoadingAsync();
            }
            GUI.enabled = previousEnabled;
        }

        private static void DrawPlayers(MultiplayerRoomSnapshot snapshot)
        {
            if (snapshot == null) return;
            var players = snapshot.Players;
            if (players == null || players.Count == 0)
            {
                GUILayout.Label("等待权威成员快照...");
                return;
            }

            GUILayout.Space(4);
            GUILayout.Label("成员:");
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var owner = string.Equals(player.AccountId, snapshot.OwnerAccountId)
                    ? " [房主]"
                    : string.Empty;
                var presence = player.IsOnline ? "在线" : "离线";
                var ready = player.LobbyReady ? "已准备" : "未准备";
                var loaded = snapshot.Phase == MultiplayerRoomPhase.Lobby
                    ? string.Empty
                    : player.AssetsLoaded
                        ? " / 已加载 100%"
                        : $" / 加载中 {player.LoadingProgress}%";
                GUILayout.Label(
                    $"P{player.PlayerId} {player.AccountId}{owner} | 英雄 {player.HeroId} | {presence} | {ready}{loaded}");
                if (snapshot.Phase != MultiplayerRoomPhase.Lobby)
                {
                    DrawProgressBar(player.LoadingProgress);
                }
            }
        }

        private void DrawLocalLoadingProgress()
        {
            var progress = _controller.LocalLoadingProgress;
            var assetKey = _controller.CurrentLoadingAssetKey;
            GUILayout.Label(string.IsNullOrWhiteSpace(assetKey)
                ? $"本地加载: {progress}%"
                : $"本地加载: {progress}%  {assetKey}");
            DrawProgressBar(progress);
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

        private static void DrawLoadingDeadline(MultiplayerRoomSnapshot snapshot)
        {
            if (snapshot == null || snapshot.LoadingDeadlineUnixMs <= 0) return;
            var remainingMs = snapshot.LoadingDeadlineUnixMs -
                              System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            GUILayout.Label($"剩余加载时间: {System.Math.Max(0, remainingMs) / 1000}s");
        }

        private void DrawFailed()
        {
            GUILayout.Label("流程失败，可重试或取消。");
            if (GUILayout.Button("重试（重置为 Idle）", GUILayout.Height(30)))
            {
                _controller.Cancel();
            }
        }

        private static MultiplayerRoomLaunchSpec BuildLaunchSpec(BattleGatewayConfigSO config)
        {
            return new MultiplayerRoomLaunchSpec
            {
                SessionToken = config != null ? config.SessionToken : string.Empty,
                Region = config != null ? config.Region : "dev",
                ServerId = config != null ? config.ServerId : "local",
                RoomType = "default",
                RoomTitle = "Dev Room",
                MaxPlayers = 2
            };
        }

        private void ExitToStarter()
        {
            _controller?.Cancel();
            _selection?.Clear();
            if (GameEntry.IsInitialized)
            {
                Object.Destroy(GameEntry.Instance.gameObject);
            }

            SceneManager.LoadScene("MultiplayerStarterScene", LoadSceneMode.Single);
        }
    }
}
