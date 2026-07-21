using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 正式多人大厅 Feature：从 <see cref="MultiplayerRoomFlowController"/> 投影状态，
    /// 提供创建房间、加入房间、选英雄、Ready 等操作。
    /// <para>
    /// 核心逻辑（状态投影、命令提交）委托给纯 C# 的 <see cref="MultiplayerRoomFlowController"/>，
    /// OnGUI 仅作为渲染层，用 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> 包裹。
    /// </para>
    /// </summary>
    public sealed class FormalLobbyFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private MultiplayerRoomFlowController _controller;
        private GatewayMultiplayerRoomSession _session;
        private LobbyBattleEntrySelection _selection;
        private IMultiplayerGatewayRuntime _gatewayRuntime;
        private BattleGatewayConfigSO _gatewayConfig;
        private bool _battleEntryRequested;
        private bool _show = true;
        private string _joinRoomId = string.Empty;
        private int _selectedHeroId = 1001;
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

            _battleEntryRequested = false;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_battleEntryRequested || !ShouldEnterBattle(_selection, _controller)) return;

            var snapshot = _controller.CurrentSnapshot;
            var flow = ctx.Entry?.Get<GameFlowDomain>();
            if (snapshot == null || flow == null || _session == null) return;

            _battleEntryRequested = true;
            var configured = new ConfiguredBattleBootstrapper(_selection.Config, _selection.Preset);
            flow.EnterBattle(new ExistingGatewayRoomBattleBootstrapper(
                configured,
                _session.SessionToken,
                snapshot.RoomId,
                snapshot.BattleId,
                snapshot.NumericRoomId,
                snapshot.WorldId));
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            if (ctx.Entry == null || !ctx.Entry.DebugEnabled) return;

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
            if (GUILayout.Button("隐藏", GUILayout.Width(56)))
            {
                _show = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label($"连接: {_gatewayRuntime?.ConnectionState}");
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

        internal static bool ShouldEnterBattle(
            LobbyBattleEntrySelection selection,
            MultiplayerRoomFlowController controller)
        {
            return ShouldShowFlowWindow(selection) &&
                   controller?.CurrentState == MultiplayerRoomFlowState.InBattle &&
                   controller.CurrentSnapshot?.NumericRoomId > 0UL &&
                   controller.CurrentSnapshot.WorldId > 0UL;
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
                    GUILayout.Label("正在加载资源，请稍候...");
                    if (GUILayout.Button("资源加载完成", GUILayout.Height(28)))
                    {
                        _ = _controller.ReportAssetsLoadedAsync();
                    }
                    break;
                case MultiplayerRoomFlowState.WaitingForBattle:
                    GUILayout.Label("等待战斗开始...");
                    if (GUILayout.Button("等待服务端开战", GUILayout.Height(28)))
                    {
                        _ = _controller.WaitForBattleStartAsync();
                    }
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

            if (GUILayout.Button("开始加载", GUILayout.Height(28)))
            {
                _ = _controller.BeginLoadingAsync();
            }
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
#else
        public void OnGUI(in GamePhaseContext ctx)
        {
            // 非 Development 构建：正式大厅 UI 不渲染（由正式 UI 框架接管）。
        }
#endif
    }
}
