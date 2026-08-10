#nullable enable

using System;
using System.Collections.Generic;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.IO;
using System.Net.Sockets;
using AbilityKit.Demo.Shooter.Editor.Diagnostics;
using AbilityKit.Demo.Shooter.Editor.Input;
using AbilityKit.Demo.Shooter.Editor.Sink;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.Network;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Shooter;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Shooter.Editor.Windows
{
    /// <summary>
    /// Shooter 网络同步演示主窗口。
    /// 可通过 <see cref="EditorApplication.update"/> 驱动模拟，
    /// 通过 <see cref="ShooterEditorSceneViewSink"/> 在 SceneView 中渲染实体，
    /// 并提供同步模式、网络环境与运行诊断配置。
    /// </summary>
    public sealed partial class ShooterDemoWindow : EditorWindow
    {
        // --- Session state ---
        private ShooterAcceptanceSession? _session;
        private ShooterPlaySessionRunner? _editorRunner;
        private ShooterEditorSceneViewSink _sink = new();
        private ShooterEditorInputProvider _inputProvider = new();
        private ShooterDemoDiagnostics _diagnostics = new();
        private ShooterHostFrameInput _lastEditorRunnerInput;

        // --- Drive mode ---
        private ShooterDemoDriveMode _driveMode = ShooterDemoDriveMode.EditorDirect;
        private IShooterSessionHost? _attachedHost;

        // --- Remote state-sync ---
        private string _remoteHost = ShooterRemoteStateSyncDefaults.DefaultHost;
        private int _remotePort = ShooterRemoteStateSyncDefaults.DefaultPort;
        private string _remoteSessionToken = ShooterRemoteStateSyncDefaults.DefaultSessionToken;
        private string _remoteAccountId = string.Empty;
        private string _remoteRegion = ShooterRemoteStateSyncDefaults.DefaultRegion;
        private string _remoteServerId = ShooterRemoteStateSyncDefaults.DefaultServerId;
        private string _remoteRoomTitle = "Unity Shooter Room";
        private int _remoteRoomMaxPlayers = 1;
        private string _remoteSelectedRoomId = string.Empty;
        private string _remoteRoomFilter = string.Empty;
        private bool _remoteShowFullRooms = true;
        private int _remoteSelectedRoomIndex;
        private readonly List<ShooterGatewayRoomSummary> _remoteRooms = new();
        private readonly List<ShooterGatewayRoomSummary> _remoteVisibleRooms = new();
        private bool _remoteLaunchPending;
        private bool _remoteLobbyPending;
        private bool _remoteServerReachable;
        private double _nextRemoteServerProbeTime;
        private string _remoteServerStatusText = "服务器: 未检查";

        // --- Run state ---
        private bool _running;
        private bool _paused;
        private bool _showingSpecBaseline;
        private float _timeScale = 1f;
        private double _lastTime;

        // --- Configuration ---
        private int _selectedSyncIndex;
        private int _selectedNetworkPresetIndex;
        private int _selectedNetworkProviderIndex;
        private bool _enableAuthoritativeWorld = true;
        private bool _showDivergence = true;
        private int _playerCount = 2;
        private int _initialHp = ShooterGameplay.DefaultPlayerHp;
        private int _randomSeed = 3901;
        private int _controlledPlayerId = 1;

        // --- Network parameters (built-in provider) ---
        private int _latencyMs;
        private int _jitterMs;
        private double _packetLossRate;
        private double _reorderRate;
        private int _bandwidthKbps;

        // --- UI state ---
        private Vector2 _diagnosticsScroll;
        private Vector2 _eventsScroll;
        private string _lastError = string.Empty;

        // --- 缓存目录数据 ---
        private static readonly IReadOnlyList<ShooterAcceptanceSyncOption> SyncModes =
            ShooterAcceptanceCatalog.SyncModes;
        private static readonly IReadOnlyList<ShooterAcceptanceNetworkOption> NetworkPresets =
            ShooterAcceptanceCatalog.NetworkEnvironments;

        // --- 跨 PlayMode 切换保留的宿主启动配置 ---
        private const string PendingHostAttachKey = "AbilityKit.ShooterDemo.PendingPlayModeAttach";
        private const string PendingRemoteStateSyncKey = "AbilityKit.ShooterDemo.PendingRemoteStateSync";
        private const string HasSavedConfigKey = "AbilityKit.ShooterDemo.HasSavedConfig";
        private const string SyncIndexKey = "AbilityKit.ShooterDemo.SyncIndex";
        private const string NetworkProviderIndexKey = "AbilityKit.ShooterDemo.NetworkProviderIndex";
        private const string NetworkPresetIndexKey = "AbilityKit.ShooterDemo.NetworkPresetIndex";
        private const string AuthoritativeWorldKey = "AbilityKit.ShooterDemo.AuthoritativeWorld";
        private const string ShowDivergenceKey = "AbilityKit.ShooterDemo.ShowDivergence";
        private const string PlayerCountKey = "AbilityKit.ShooterDemo.PlayerCount";
        private const string InitialHpKey = "AbilityKit.ShooterDemo.InitialHp";
        private const string RandomSeedKey = "AbilityKit.ShooterDemo.RandomSeed";
        private const string ControlledPlayerIdKey = "AbilityKit.ShooterDemo.ControlledPlayerId";
        private const string LatencyMsKey = "AbilityKit.ShooterDemo.LatencyMs";
        private const string JitterMsKey = "AbilityKit.ShooterDemo.JitterMs";
        private const string PacketLossRateKey = "AbilityKit.ShooterDemo.PacketLossRate";
        private const string ReorderRateKey = "AbilityKit.ShooterDemo.ReorderRate";
        private const string BandwidthKbpsKey = "AbilityKit.ShooterDemo.BandwidthKbps";
        private const string RemoteHostKey = "AbilityKit.ShooterDemo.RemoteHost";
        private const string RemotePortKey = "AbilityKit.ShooterDemo.RemotePort";
        private const string RemoteSessionTokenKey = "AbilityKit.ShooterDemo.RemoteSessionToken";
        private const string RemoteAccountIdKey = "AbilityKit.ShooterDemo.RemoteAccountId";
        private const string RemoteRegionKey = "AbilityKit.ShooterDemo.RemoteRegion";
        private const string RemoteServerIdKey = "AbilityKit.ShooterDemo.RemoteServerId";
        private const string RemoteRoomTitleKey = "AbilityKit.ShooterDemo.RemoteRoomTitle";
        private const string RemoteRoomMaxPlayersKey = "AbilityKit.ShooterDemo.RemoteRoomMaxPlayers";
        private const string RemoteSelectedRoomIdKey = "AbilityKit.ShooterDemo.RemoteSelectedRoomId";
        private const string RemoteRoomFilterKey = "AbilityKit.ShooterDemo.RemoteRoomFilter";
        private const string RemoteShowFullRoomsKey = "AbilityKit.ShooterDemo.RemoteShowFullRooms";

        [MenuItem("Tools/AbilityKit/Demos/Shooter/Shooter Demo")]
        private static void Open()
        {
            GetWindow<ShooterDemoWindow>("Shooter Demo");
        }

        private void OnEnable()
        {
            _lastTime = EditorApplication.timeSinceStartup;
            if (SessionState.GetBool(HasSavedConfigKey, false))
            {
                RestoreConfigFromSessionState();
                ApplyCustomNetwork();
            }
            else
            {
                ApplyNetworkPreset(0);
            }

            // 宿主生命周期独立于窗口挂接状态，订阅后状态栏可以持续显示最新运行状态。
            ShooterHostSessionRegistry.HostsChanged += OnHostLifecycleChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ShooterRemoteStateSyncPlayModeHost.StateChanged += OnRemoteStateSyncHostChanged;

            if (SessionState.GetBool(PendingHostAttachKey, false) && Application.isPlaying)
            {
                EditorApplication.delayCall += TryStartPendingHostSession;
            }

            if (SessionState.GetBool(PendingRemoteStateSyncKey, false) && Application.isPlaying)
            {
                EditorApplication.delayCall += TryStartPendingRemoteStateSyncSession;
            }
        }

        private void OnDisable()
        {
            StopInternal();
            ShooterHostSessionRegistry.HostsChanged -= OnHostLifecycleChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ShooterRemoteStateSyncPlayModeHost.StateChanged -= OnRemoteStateSyncHostChanged;
        }

        private void OnHostLifecycleChanged()
        {
            // 宿主可能由 PlayMode 生命周期或其他窗口改变，这里只负责刷新可见状态。
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingHostAttachKey, false))
            {
                EditorApplication.delayCall += TryStartPendingHostSession;
            }

            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingRemoteStateSyncKey, false))
            {
                EditorApplication.delayCall += TryStartPendingRemoteStateSyncSession;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(PendingHostAttachKey, false);
                SessionState.SetBool(PendingRemoteStateSyncKey, false);
            }

            // 进入或退出 Play 模式会改变宿主状态与按钮可用性。
            Repaint();
        }

        private void OnRemoteStateSyncHostChanged()
        {
            Repaint();
        }

        // =====================================================================
        // 主界面
        // =====================================================================

        private void OnGUI()
        {
            CaptureWindowKeyboardInput();
            DrawToolbar();
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            // 左侧：运行配置
            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            DrawConfigPanel();
            EditorGUILayout.EndVertical();

            // 右侧：运行诊断
            EditorGUILayout.BeginVertical();
            DrawDiagnosticsPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            DrawStatusBar();
        }

        // =====================================================================
        // Toolbar
        // =====================================================================

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 会话运行中不允许切换驱动模式。
            EditorGUI.BeginDisabledGroup(_running);
            var newMode = (ShooterDemoDriveMode)EditorGUILayout.EnumPopup(
                _driveMode, EditorStyles.toolbarPopup, GUILayout.Width(130));
            if (newMode != _driveMode)
            {
                _driveMode = newMode;
            }
            EditorGUI.EndDisabledGroup();

            var attachMode = _driveMode == ShooterDemoDriveMode.HostAttach;
            var remoteMode = _driveMode == ShooterDemoDriveMode.RemoteStateSync;

            EditorGUI.BeginDisabledGroup(_running || _remoteLaunchPending || ShooterRemoteStateSyncPlayModeHost.IsStarting);
            var startLabel = attachMode ? "🔗 启动并挂接" : remoteMode ? "🌐 连接/重连" : "▶ 启动";
            if (GUILayout.Button(startLabel, EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                StartInternal();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_running && !_remoteLaunchPending);
            var stopLabel = attachMode ? "✂ 断开" : remoteMode ? "■ 断开远程" : "■ 停止";
            if (GUILayout.Button(stopLabel, EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                StopInternal();
            }
            EditorGUI.EndDisabledGroup();

            if (attachMode)
            {
                // 只依赖发布接口停止宿主，避免窗口绑定具体 PlayMode 宿主实现。
                var hasStoppableHost = ShooterHostSessionRegistry.Active != null;
                EditorGUI.BeginDisabledGroup(!Application.isPlaying || !hasStoppableHost);
                if (GUILayout.Button("■ 停止宿主", EditorStyles.toolbarButton, GUILayout.Width(95)))
                {
                    StopPlayModeHostInternal();
                }
                EditorGUI.EndDisabledGroup();
            }

            var canRebuildViews = Application.isPlaying &&
                ((attachMode && ShooterPlayModeSessionHost.IsRunning) ||
                 (remoteMode && ShooterRemoteStateSyncPlayModeHost.IsRunning));
            EditorGUI.BeginDisabledGroup(!canRebuildViews);
            if (GUILayout.Button("↻ 重建显示", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                RebuildPlayModeViewsInternal();
            }
            EditorGUI.EndDisabledGroup();

            // 暂停与单步只适用于窗口自驱的 Editor Direct 模式。
            EditorGUI.BeginDisabledGroup(!_running || attachMode || remoteMode);
            _paused = GUILayout.Toggle(_paused, "‖ 暂停", EditorStyles.toolbarButton, GUILayout.Width(70));

            if (GUILayout.Button("▶ 单步", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                StepInternal();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            // 速度只影响 Editor 自驱循环。
            EditorGUI.BeginDisabledGroup(attachMode || remoteMode);
            GUILayout.Label("速度", GUILayout.Width(35));
            _timeScale = EditorGUILayout.Slider(_timeScale, 0f, 4f, GUILayout.Width(160));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // 配置面板
        // =====================================================================

        private void DrawConfigPanel()
        {
            DrawSyncModeSection();
            EditorGUILayout.Space(4);
            DrawNetworkSection();
            EditorGUILayout.Space(4);
            DrawPlayerConfigSection();
            EditorGUILayout.Space(4);
            DrawRemoteStateSyncSection();
            EditorGUILayout.Space(4);
            DrawInputSection();
            EditorGUILayout.Space(4);
            DrawAcceptanceSpecSection();
        }

        private void DrawSyncModeSection()
        {
            EditorGUILayout.LabelField("同步模式", EditorStyles.boldLabel);

            var syncNames = new string[SyncModes.Count];
            for (int i = 0; i < SyncModes.Count; i++)
            {
                syncNames[i] = SyncModes[i].Implemented
                    ? SyncModes[i].DisplayName
                    : SyncModes[i].DisplayName + " (未接入)";
            }

            EditorGUI.BeginDisabledGroup(_running);
            var newSyncIdx = EditorGUILayout.Popup("模式", _selectedSyncIndex, syncNames);
            if (newSyncIdx != _selectedSyncIndex)
            {
                _selectedSyncIndex = newSyncIdx;
            }
            EditorGUI.EndDisabledGroup();

            if (_selectedSyncIndex < SyncModes.Count)
            {
                var mode = SyncModes[_selectedSyncIndex];
                if (!mode.Implemented)
                {
                    EditorGUILayout.HelpBox($"'{mode.DisplayName}' 尚未完成接入。", MessageType.Warning);
                }
            }
        }

        private void DrawNetworkSection()
        {
            EditorGUILayout.LabelField("网络环境", EditorStyles.boldLabel);

            // 网络配置来源下拉框。
            var providers = ShooterNetworkConditionRegistry.All;
            var providerNames = new string[providers.Count];
            for (int i = 0; i < providers.Count; i++)
            {
                providerNames[i] = providers[i].DisplayName;
                if (!providers[i].IsActive) providerNames[i] += " (未激活)";
            }

            var newProviderIdx = EditorGUILayout.Popup("来源", _selectedNetworkProviderIndex, providerNames);
            if (newProviderIdx != _selectedNetworkProviderIndex)
            {
                _selectedNetworkProviderIndex = newProviderIdx;
            }

            // 只有内置来源才显示可调网络参数。
            if (_selectedNetworkProviderIndex == 0)
            {
                DrawBuiltinNetworkSliders();
            }
            else if (_selectedNetworkProviderIndex < providers.Count)
            {
                var provider = providers[_selectedNetworkProviderIndex];
                if (!provider.IsActive)
                {
                    EditorGUILayout.HelpBox(
                        $"外部网络配置来源 '{provider.DisplayName}' 未激活，请确认对应工具已运行。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("配置", DescribeProfile(provider.Profile));
                }
            }
        }

        private void DrawBuiltinNetworkSliders()
        {
            // 预设按钮。
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("预设", GUILayout.Width(45));
            for (int i = 0; i < NetworkPresets.Count; i++)
            {
                var preset = NetworkPresets[i];
                var shortName = GetShortPresetName(preset.DisplayName);
                if (GUILayout.Button(shortName, EditorStyles.miniButton, GUILayout.Width(52)))
                {
                    ApplyNetworkPreset(i);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 网络参数滑条。
            EditorGUI.BeginChangeCheck();
            _latencyMs = EditorGUILayout.IntSlider("延迟 (ms)", _latencyMs, 0, 500);
            _jitterMs = EditorGUILayout.IntSlider("抖动 (ms)", _jitterMs, 0, 200);
            _packetLossRate = EditorGUILayout.Slider("丢包率", (float)_packetLossRate, 0f, 0.5f);
            _reorderRate = EditorGUILayout.Slider("乱序率", (float)_reorderRate, 0f, 0.5f);
            _bandwidthKbps = EditorGUILayout.IntSlider("带宽 (kbps)", _bandwidthKbps, 0, 10000);

            if (EditorGUI.EndChangeCheck())
            {
                ApplyCustomNetwork();
            }
        }

        private void DrawPlayerConfigSection()
        {
            EditorGUILayout.LabelField("玩家", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(_running);
            _playerCount = EditorGUILayout.IntSlider("数量", _playerCount, 1, ShooterGameplay.DefaultMaxPlayers);
            _initialHp = EditorGUILayout.IntField("初始 HP", _initialHp);
            _randomSeed = EditorGUILayout.IntField("随机种子", _randomSeed);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(2);

            _enableAuthoritativeWorld = EditorGUILayout.ToggleLeft(
                "显示权威世界（对比）", _enableAuthoritativeWorld);

            if (_enableAuthoritativeWorld)
            {
                _showDivergence = EditorGUILayout.ToggleLeft(
                    "  显示偏差连线", _showDivergence);
            }
        }

        private void DrawRemoteStateSyncSection()
        {
            if (_driveMode != ShooterDemoDriveMode.RemoteStateSync)
            {
                return;
            }

            EditorGUILayout.LabelField("远程状态同步", EditorStyles.boldLabel);
            var busy = _running || _remoteLaunchPending || _remoteLobbyPending || ShooterRemoteStateSyncPlayModeHost.IsStarting;
            DrawRemoteServerControls(busy);
            EditorGUI.BeginDisabledGroup(busy);
            _remoteHost = EditorGUILayout.TextField("Host", _remoteHost);
            _remotePort = EditorGUILayout.IntField("Port", _remotePort);
            _remoteSessionToken = EditorGUILayout.TextField("Session", _remoteSessionToken);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Account", string.IsNullOrWhiteSpace(_remoteAccountId) ? "<not logged in>" : _remoteAccountId);
            EditorGUI.EndDisabledGroup();
            _remoteRegion = EditorGUILayout.TextField("Region", _remoteRegion);
            _remoteServerId = EditorGUILayout.TextField("Server", _remoteServerId);
            _remoteRoomTitle = EditorGUILayout.TextField("Room Title", _remoteRoomTitle);
            _remoteRoomMaxPlayers = EditorGUILayout.IntSlider("Max Players", _remoteRoomMaxPlayers, 1, ShooterGameplay.DefaultMaxPlayers);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(busy);
            if (GUILayout.Button("游客登录", GUILayout.Height(22)))
            {
                GuestLoginRemoteAsync();
            }
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_remoteSessionToken));
            if (GUILayout.Button("刷新房间", GUILayout.Height(22)))
            {
                RefreshRemoteRoomsAsync();
            }
            if (GUILayout.Button("创建房间", GUILayout.Height(22)))
            {
                CreateRemoteRoomAsync(false);
            }
            if (GUILayout.Button("一人房间并开始", GUILayout.Height(22)))
            {
                CreateRemoteRoomAsync(true);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            DrawRemoteRoomSelection(busy);

            var launchModeText = string.IsNullOrWhiteSpace(_remoteSelectedRoomId)
                ? "未选择房间：连接时先 RestoreRoom，失败后自动 Create/Ready/Start/Subscribe。"
                : $"已选择房间 {_remoteSelectedRoomId}：若 Web/Sandbox 已开战则 Join 后直接 Subscribe，否则 Join/Ready/Start/Subscribe。";
            EditorGUILayout.HelpBox(
                launchModeText + " 默认连接 Server/Orleans/tools/restart_shooter_state_sync.bat 启动的 127.0.0.1:41001。",
                MessageType.Info);
        }

        private void DrawInputSection()
        {
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);

            _inputProvider.EnableKeyboardInput = EditorGUILayout.ToggleLeft(
                "启用键盘（WASD / 方向键 + Space）", _inputProvider.EnableKeyboardInput);

            EditorGUI.BeginDisabledGroup(!_inputProvider.EnableKeyboardInput);
            EditorGUILayout.LabelField("Editor按键", _inputProvider.GetDebugKeyString());
            EditorGUI.EndDisabledGroup();

            DrawInputDiagnostics();

            EditorGUILayout.Space(2);

            var playerOptions = new string[_playerCount];
            for (int i = 0; i < _playerCount; i++)
            {
                playerOptions[i] = $"玩家 {i + 1}";
            }
            _controlledPlayerId = EditorGUILayout.Popup("控制", _controlledPlayerId - 1, playerOptions) + 1;
            _inputProvider.ControlledPlayerId = _controlledPlayerId;
        }

        private void DrawInputDiagnostics()
        {
            if (!_running)
            {
                return;
            }

            var remoteMode = _driveMode == ShooterDemoDriveMode.RemoteStateSync;
            var input = remoteMode
                ? ShooterRemoteStateSyncPlayModeHost.LastInput
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.LastInput
                    : _lastEditorRunnerInput;
            var stepCount = remoteMode
                ? ShooterRemoteStateSyncPlayModeHost.StepCount
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.StepCount
                    : _editorRunner?.StepCount ?? 0L;
            var renderCount = remoteMode
                ? ShooterRemoteStateSyncPlayModeHost.RenderCount
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.RenderCount
                    : _editorRunner?.RenderCount ?? 0L;

            var submit = remoteMode
                ? ShooterRemoteStateSyncPlayModeHost.LastSubmitResult
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.LastSubmitResult
                    : _editorRunner?.LastSubmitResult ?? default;
            var tick = remoteMode
                ? ShooterRemoteStateSyncPlayModeHost.LastTickResult
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.LastTickResult
                    : _editorRunner?.LastTickResult ?? default;
            var authorityAccepted = remoteMode
                ? 0
                : _driveMode == ShooterDemoDriveMode.HostAttach
                    ? ShooterPlayModeSessionHost.LastAuthorityAcceptedInputs
                    : _editorRunner?.LastAuthorityAcceptedInputs ?? 0;

            EditorGUILayout.LabelField("最后输入", FormatInput(input), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Runner", $"Step:{stepCount} Render:{renderCount} Submit:{submit.AcceptedInputs}@{submit.RequestedFrame} Tick:{tick.Ticks}@{tick.Frame} Hash:{tick.StateHash:X8} AuthSubmit:{authorityAccepted}", EditorStyles.miniLabel);
            if (remoteMode)
            {
                var gateway = ShooterRemoteStateSyncPlayModeHost.LastGatewaySubmitResult.Remote;
                var gatewayError = ShooterRemoteStateSyncPlayModeHost.LastGatewayInputError;
                var gatewayStatus = gatewayError != null
                    ? gatewayError.Message
                    : $"Pending:{ShooterRemoteStateSyncPlayModeHost.HasPendingGatewayInput} Queued:{ShooterRemoteStateSyncPlayModeHost.HasQueuedGatewayInput} Sent:{ShooterRemoteStateSyncPlayModeHost.GatewayInputSubmittedCount} Done:{ShooterRemoteStateSyncPlayModeHost.GatewayInputCompletedCount} Replaced:{ShooterRemoteStateSyncPlayModeHost.GatewayInputReplacedCount} Failed:{ShooterRemoteStateSyncPlayModeHost.GatewayInputFailedCount} Resyncs:{ShooterRemoteStateSyncPlayModeHost.GatewayInputResyncRequestedCount} Accepted:{gateway.AcceptedFrame} Current:{gateway.CurrentFrame} {gateway.Status}";
                EditorGUILayout.LabelField("Gateway输入", gatewayStatus, EditorStyles.miniLabel);
            }
        }

        private static string FormatInput(in ShooterHostFrameInput input)
        {
            return $"Move({input.MoveX:F1},{input.MoveY:F1}) Aim({input.AimX:F1},{input.AimY:F1}) Fire:{input.Fire}";
        }

        private void DrawAcceptanceSpecSection()
        {
            EditorGUILayout.LabelField("验收规格", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "运行纯 C# BasicCombat 规格，并把结果写入当前诊断面板。该结果与自动化测试使用同一份规格。",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(_running);
            if (GUILayout.Button("运行 BasicCombat 基线", GUILayout.Height(24)))
            {
                RunBasicCombatSpecBaseline();
            }
            EditorGUI.EndDisabledGroup();
        }

        // =====================================================================
        // 诊断面板
        // =====================================================================

        private void DrawDiagnosticsPanel()
        {
            EditorGUILayout.LabelField("运行诊断", EditorStyles.boldLabel);

            if (!_running && !_showingSpecBaseline)
            {
                EditorGUILayout.HelpBox(
                    "选择同步模式与网络环境后，点击 '▶ 启动' 开始 Shooter 演示。\n" +
                    "Host 挂接模式会自动进入 Play 模式并启动宿主。\n" +
                    "也可以在左侧运行验收规格，直接查看与自动化测试一致的纯 C# 基线结果。",
                    MessageType.Info);
                return;
            }

            // 关键运行统计。
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"帧: {_diagnostics.Frame}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"玩家: {_diagnostics.PlayerCount}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"子弹: {_diagnostics.BulletCount}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"敌人: {_diagnostics.EnemyCount}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"回滚: {_diagnostics.TotalRollbacks}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // 客户端与权威世界偏差。
            if (_enableAuthoritativeWorld)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"最大偏差: {_diagnostics.MaxDivergence:F4}", GUILayout.Width(200));
                EditorGUILayout.LabelField($"偏差数: {_diagnostics.Divergences.Count}", GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
            }

            // 实体列表。
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("实体", EditorStyles.miniBoldLabel);

            _diagnosticsScroll = EditorGUILayout.BeginScrollView(_diagnosticsScroll, GUILayout.Height(140));
            if (_driveMode == ShooterDemoDriveMode.HostAttach && _session != null)
            {
                var snapshot = _session.Runtime.GetSnapshot();
                DrawRuntimeSnapshotEntities(in snapshot);
            }
            else
            {
                var clientEntities = _sink.ClientEntities;
                for (int i = 0; i < clientEntities.Count; i++)
                {
                    var e = clientEntities[i];
                    EditorGUILayout.LabelField(FormatEntityLabel(in e), EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();

            // 事件日志。
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"事件（总计: {_diagnostics.TotalEvents}）", EditorStyles.miniBoldLabel);

            _eventsScroll = EditorGUILayout.BeginScrollView(_eventsScroll, GUILayout.Height(120));
            var events = _diagnostics.RecentEvents;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                var evt = events[i];
                string label;
                if (evt.EventType == (int)ShooterEventType.Hit)
                {
                    label = $"  [命中] P{evt.SourcePlayerId}→P{evt.TargetPlayerId} 子弹#{evt.BulletId} 位置({evt.X:F1},{evt.Y:F1}) 伤害:{evt.Value}";
                }
                else if (evt.EventType == (int)ShooterEventType.Fire)
                {
                    label = $"  [开火] P{evt.SourcePlayerId} 子弹#{evt.BulletId} 位置({evt.X:F1},{evt.Y:F1})";
                }
                else
                {
                    label = $"  [事件{evt.EventType}] 来源:{evt.SourcePlayerId} 目标:{evt.TargetPlayerId} 值:{evt.Value}";
                }
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawRuntimeSnapshotEntities(in ShooterStateSnapshotPayload snapshot)
        {
            var players = snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>();
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                EditorGUILayout.LabelField(
                    $"  玩家 #{player.PlayerId}  HP:{player.Hp}  分数:{player.Score}  ({player.X:F1}, {player.Y:F1})  Aim:({player.AimX:F1}, {player.AimY:F1}) Alive:{player.Alive}",
                    EditorStyles.miniLabel);
            }

            var bullets = snapshot.Bullets ?? Array.Empty<ShooterBulletSnapshot>();
            for (int i = 0; i < bullets.Length; i++)
            {
                var bullet = bullets[i];
                EditorGUILayout.LabelField(
                    $"  子弹 #{bullet.BulletId}  归属:{bullet.OwnerPlayerId}  ({bullet.X:F1}, {bullet.Y:F1})  速度:({bullet.VelocityX:F1}, {bullet.VelocityY:F1})  剩余帧:{bullet.RemainingFrames}",
                    EditorStyles.miniLabel);
            }
        }

        private static string FormatEntityLabel(in ShooterEditorSceneViewSink.EntityDrawData entity)
        {
            if (entity.Kind == ShooterViewEntityKind.Player)
            {
                return $"  玩家 #{entity.EntityId}  HP:{entity.Hp}  分数:{entity.Score}  ({entity.X:F1}, {entity.Y:F1})";
            }

            if (entity.Kind == ShooterViewEntityKind.Enemy)
            {
                return $"  敌人 #{entity.EntityId}  HP:{entity.Hp}  ({entity.X:F1}, {entity.Y:F1})  速度:({entity.VelocityX:F1}, {entity.VelocityY:F1})";
            }

            return $"  子弹 #{entity.EntityId}  归属:{entity.OwnerEntityId}  ({entity.X:F1}, {entity.Y:F1})  速度:({entity.VelocityX:F1}, {entity.VelocityY:F1})  剩余帧:{entity.RemainingFrames}";
        }

        // =====================================================================
        // 状态栏
        // =====================================================================

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(_diagnostics.StatusText, EditorStyles.miniLabel, GUILayout.Width(120));

            if (_running && _session != null)
            {
                EditorGUILayout.LabelField($"同步: {_session.SyncModel}", EditorStyles.miniLabel, GUILayout.Width(160));
                EditorGUILayout.LabelField($"网络: {_session.NetworkName}", EditorStyles.miniLabel, GUILayout.Width(140));
            }

            GUILayout.FlexibleSpace();

            // PlayMode 宿主生命周期独立于窗口挂接状态，这里直接暴露给使用者。
            if (_driveMode == ShooterDemoDriveMode.HostAttach)
            {
                EditorGUILayout.LabelField(GetPlayModeHostStatusText(), EditorStyles.miniLabel, GUILayout.Width(150));
            }
            else if (_driveMode == ShooterDemoDriveMode.RemoteStateSync)
            {
                EditorGUILayout.LabelField(GetRemoteStateSyncStatusText(), EditorStyles.miniLabel, GUILayout.Width(220));
            }

            if (!string.IsNullOrEmpty(_lastError))
            {
                EditorGUILayout.LabelField(_lastError, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 生成 PlayMode 宿主生命周期提示；宿主运行状态与当前窗口是否已挂接是两个独立概念。
        /// </summary>
        private string GetPlayModeHostStatusText()
        {
            if (SessionState.GetBool(PendingHostAttachKey, false))
            {
                return "宿主: 等待进入 Play";
            }

            if (!Application.isPlaying)
            {
                return "宿主: 未进入 Play";
            }

            if (ShooterPlayModeSessionHost.IsRunning)
            {
                return _attachedHost?.IsRunning == true
                    ? "宿主: 运行中 / 已挂接"
                    : "宿主: 运行中 / 可挂接";
            }

            if (ShooterPlayModeSessionHost.IsInstalled)
            {
                return "宿主: 已安装 / 未启动";
            }

            return "宿主: 空闲";
        }

        private string GetRemoteStateSyncStatusText()
        {
            if (SessionState.GetBool(PendingRemoteStateSyncKey, false))
            {
                return "远程: 等待进入 Play";
            }

            if (!Application.isPlaying)
            {
                return "远程: 未进入 Play";
            }

            if (ShooterRemoteStateSyncPlayModeHost.IsStarting || _remoteLaunchPending)
            {
                return "远程: 连接中";
            }

            if (ShooterRemoteStateSyncPlayModeHost.IsRunning)
            {
                var flow = ShooterRemoteStateSyncPlayModeHost.Flow;
                if (!flow.HasValue)
                {
                    return "远程: 已连接";
                }

                var flowValue = flow.Value;
                return $"远程: {flowValue.EntryKind} Room:{flowValue.RoomId}";
            }

            if (ShooterRemoteStateSyncPlayModeHost.LastError != null)
            {
                return "远程: 连接失败";
            }

            return ShooterRemoteStateSyncPlayModeHost.IsInstalled ? "远程: 已安装 / 未连接" : "远程: 空闲";
        }

        // =====================================================================
        // 键盘输入捕获
        // =====================================================================

        private void CaptureWindowKeyboardInput()
        {
            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.KeyDown)
            {
                if (_inputProvider.OnKeyDown(e))
                {
                    e.Use();
                    Repaint();
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                if (_inputProvider.OnKeyUp(e))
                {
                    e.Use();
                    Repaint();
                }
            }
        }

        // =====================================================================
        // Session Lifecycle
        // =====================================================================

        private void StartInternal()
        {
            if (_running) return;

            if (_driveMode == ShooterDemoDriveMode.HostAttach)
            {
                AttachInternal();
                return;
            }

            if (_driveMode == ShooterDemoDriveMode.RemoteStateSync)
            {
                StartRemoteStateSyncInternal();
                return;
            }

            try
            {
                _lastError = string.Empty;
                _showingSpecBaseline = false;

                if (!TryBuildSessionOptions(out var options))
                {
                    Repaint();
                    return;
                }

                _editorRunner = new ShooterPlaySessionRunner(_inputProvider, _sink);
                _session = _editorRunner.Start(options);

                // Configure sink
                _sink.Clear();
                _sink.ShowAuthorityWorld = _enableAuthoritativeWorld;
                _sink.ShowDivergence = _showDivergence;

                // Configure diagnostics
                _diagnostics.Reset();
                _diagnostics.IsRunning = true;

                // Apply initial snapshot to sink
                var initialSnapshot = _session.Runtime.GetSnapshot();
                _session.Presentation.ApplyLocalPredictionSnapshot(in initialSnapshot);

                // Register SceneView callback
                SceneView.duringSceneGui += OnSceneViewGUI;

                // Start editor update loop
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += OnEditorUpdate;

                _running = true;
                _paused = false;

                // Focus SceneView for best experience
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    sv.LookAt(new Vector3(2f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f), 20f);
                }

                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"Start failed: {ex.Message}";
                Debug.LogException(ex);
                StopInternal();
            }
        }

        private void StopInternal()
        {
            if (_driveMode == ShooterDemoDriveMode.RemoteStateSync || _remoteLaunchPending)
            {
                StopRemoteStateSyncInternal();
                return;
            }

            if (_attachedHost != null)
            {
                DetachInternal();
                return;
            }

            if (_running || _showingSpecBaseline)
            {
                EditorApplication.update -= OnEditorUpdate;
                SceneView.duringSceneGui -= OnSceneViewGUI;
            }

            _running = false;
            _paused = false;
            _showingSpecBaseline = false;
            _editorRunner?.Dispose();
            _editorRunner = null;
            _session = null;
            _sink.Clear();
            _inputProvider.Reset();
            _diagnostics.Reset();

            SceneView.RepaintAll();
            Repaint();
        }

        private void RebuildPlayModeViewsInternal()
        {
            try
            {
                if (_driveMode == ShooterDemoDriveMode.RemoteStateSync)
                {
                    ShooterRemoteStateSyncPlayModeHost.RebuildViews();
                }
                else
                {
                    ShooterPlayModeSessionHost.RebuildViews();
                }

                _lastError = "已从最新投影状态重建 Shooter GameObject 显示层。";
                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"Rebuild views failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void StopPlayModeHostInternal()
        {
            try
            {
                if (_attachedHost != null)
                {
                    DetachInternal();
                }

                // Stop the host through the published interface so this window does not
                // depend on the concrete PlayMode host implementation. Any registered
                // host (now or in the future) can be stopped uniformly.
                var host = ShooterHostSessionRegistry.Active;
                if (host != null)
                {
                    host.Stop();
                }

                _lastError = string.Empty;
                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"Stop Play-mode host failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void RunBasicCombatSpecBaseline()
        {
            try
            {
                var runner = new ShooterAcceptanceSpecRunner();
                var result = runner.Run(ShooterAcceptanceSpecs.BasicCombat);

                _sink.Clear();
                _sink.ShowAuthorityWorld = false;
                _sink.ShowDivergence = false;

                using var session = ShooterAcceptanceLab.Create(
                    NetworkSyncModel.PredictRollback,
                    NetworkConditionProfile.Ideal,
                    players: ShooterAcceptanceSpecs.BasicCombat.Start.Players,
                    randomSeed: ShooterAcceptanceSpecs.BasicCombat.Start.RandomSeed,
                    enableAuthoritativeWorld: false);
                var snapshot = result.Snapshot;
                session.Presentation.ApplyLocalPredictionSnapshot(in snapshot);
                var clientBatch = session.Presentation.ViewModel.Current;
                ShooterHostPresentationFrame frame = new ShooterHostPresentationFrame(
                    clientBatch,
                    ShooterSnapshotViewBatch.Empty,
                    false,
                    1,
                    1f,
                    null,
                    null,
                    default,
                    default,
                    null,
                    null,
                    default,
                    default,
                    default,
                    false,
                    ShooterPureStateResyncReason.None,
                    0,
                    0u,
                    0,
                    0u);
                _sink.Render(in frame);

                _diagnostics.Reset();
                _diagnostics.Frame = result.Frame;
                _diagnostics.PlayerCount = result.Snapshot.Players.Length;
                _diagnostics.BulletCount = result.Snapshot.Bullets.Length;
                _diagnostics.RecentEvents = result.Events;
                _diagnostics.TotalEvents = result.Events.Count;

                _showingSpecBaseline = true;
                SceneView.duringSceneGui -= OnSceneViewGUI;
                SceneView.duringSceneGui += OnSceneViewGUI;

                _lastError = $"BasicCombat 规格通过：Frame={result.Frame}, Hash={result.StateHash:X8}";
                SceneView.RepaintAll();
                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"BasicCombat spec failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        // =====================================================================
        // Remote State Sync Lifecycle
        // =====================================================================

        private async void StartRemoteStateSyncInternal()
        {
            if (_remoteLaunchPending || _running)
            {
                return;
            }

            _lastError = string.Empty;
            _showingSpecBaseline = false;

            if (!TryBuildSessionOptions(out var options) || !TryBuildRemoteEndpoint(out var endpoint))
            {
                Repaint();
                return;
            }

            SaveConfigToSessionState();

            if (!Application.isPlaying)
            {
                SessionState.SetBool(PendingRemoteStateSyncKey, true);
                _lastError = "已保存当前配置，正在进入 Play 模式并连接 Shooter 状态同步服务器。";
                EditorApplication.isPlaying = true;
                Repaint();
                return;
            }

            await StartRemoteStateSyncInPlayModeAsync(BuildRemoteLaunchOptions(options, endpoint));
        }

        private async void TryStartPendingRemoteStateSyncSession()
        {
            if (!SessionState.GetBool(PendingRemoteStateSyncKey, false) || !Application.isPlaying)
            {
                return;
            }

            RestoreConfigFromSessionState();
            ApplyCustomNetwork();
            SessionState.SetBool(PendingRemoteStateSyncKey, false);

            if (!TryBuildSessionOptions(out var options) || !TryBuildRemoteEndpoint(out var endpoint))
            {
                Repaint();
                return;
            }

            _driveMode = ShooterDemoDriveMode.RemoteStateSync;
            await StartRemoteStateSyncInPlayModeAsync(BuildRemoteLaunchOptions(options, endpoint));
        }

        private ShooterRemoteStateSyncLaunchOptions BuildRemoteLaunchOptions(
            ShooterPlayModeSessionOptions options,
            ShooterClientNetworkEndpoint endpoint)
        {
            return string.IsNullOrWhiteSpace(_remoteSelectedRoomId)
                ? ShooterRemoteStateSyncLaunchOptions.RestoreFirst(
                    options,
                    endpoint,
                    _remoteSessionToken,
                    _remoteRegion,
                    _remoteServerId)
                : ShooterRemoteStateSyncLaunchOptions.JoinRoom(
                    options,
                    endpoint,
                    _remoteSessionToken,
                    _remoteRegion,
                    _remoteServerId,
                    _remoteSelectedRoomId);
        }

        private async System.Threading.Tasks.Task StartRemoteStateSyncInPlayModeAsync(
            ShooterRemoteStateSyncLaunchOptions launchOptions)
        {
            try
            {
                _remoteLaunchPending = true;
                _running = false;
                _paused = false;
                _diagnostics.Reset();
                _diagnostics.IsRunning = true;
                _lastError = $"正在连接 {launchOptions.Endpoint.Host}:{launchOptions.Endpoint.Port} ...";
                Repaint();

                await ShooterRemoteStateSyncPlayModeHost.StartAsync(launchOptions);

                _remoteLaunchPending = false;
                _running = true;
                _paused = false;
                _lastError = string.Empty;

                EditorApplication.update -= OnRemoteStateSyncUpdate;
                EditorApplication.update += OnRemoteStateSyncUpdate;
                OnRemoteStateSyncUpdate();
            }
            catch (Exception ex)
            {
                _remoteLaunchPending = false;
                _running = false;
                _diagnostics.Reset();
                _lastError = $"Remote state-sync failed: {ex.Message}";
                Debug.LogException(ex);
                Repaint();
            }
        }

        private void StopRemoteStateSyncInternal()
        {
            EditorApplication.update -= OnRemoteStateSyncUpdate;
            SessionState.SetBool(PendingRemoteStateSyncKey, false);
            _remoteLaunchPending = false;
            _running = false;
            _paused = false;
            _showingSpecBaseline = false;
            ShooterRemoteStateSyncPlayModeHost.Stop();
            _diagnostics.Reset();
            _lastError = string.Empty;
            Repaint();
        }

        private void OnRemoteStateSyncUpdate()
        {
            if (_driveMode != ShooterDemoDriveMode.RemoteStateSync)
            {
                return;
            }

            var session = ShooterRemoteStateSyncPlayModeHost.Session;
            if (!_remoteLaunchPending && !ShooterRemoteStateSyncPlayModeHost.IsRunning && session == null)
            {
                var error = ShooterRemoteStateSyncPlayModeHost.LastError;
                if (error != null)
                {
                    _lastError = $"Remote state-sync stopped: {error.Message}";
                }
                _running = false;
                _diagnostics.IsRunning = false;
                Repaint();
                return;
            }

            UpdateRemoteDiagnostics(session);
            Repaint();
        }

        private void UpdateRemoteDiagnostics(ShooterClientSession? session)
        {
            _diagnostics.IsRunning = _remoteLaunchPending || ShooterRemoteStateSyncPlayModeHost.IsRunning;
            _diagnostics.IsPaused = false;

            if (session == null)
            {
                return;
            }

            CountViewEntities(session.Presentation.ViewModel.Current.EntityChanges, out var players, out var bullets, out var enemies);

            _diagnostics.Frame = session.CurrentFrame;
            _diagnostics.PlayerCount = players;
            _diagnostics.BulletCount = bullets;
            _diagnostics.EnemyCount = enemies;
            _diagnostics.TotalRollbacks = 0;
            _diagnostics.MaxDivergence = 0d;
        }

        private static void CountViewEntities(
            IReadOnlyList<ShooterViewEntityChange> entities,
            out int players,
            out int bullets,
            out int enemies)
        {
            players = 0;
            bullets = 0;
            enemies = 0;
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.Alive)
                {
                    continue;
                }

                if (entity.Kind == ShooterViewEntityKind.Player)
                {
                    players++;
                }
                else if (entity.Kind == ShooterViewEntityKind.Bullet)
                {
                    bullets++;
                }
                else if (entity.Kind == ShooterViewEntityKind.Enemy)
                {
                    enemies++;
                }
            }
        }

        private bool TryBuildRemoteEndpoint(out ShooterClientNetworkEndpoint endpoint)
        {
            endpoint = default;
            var host = string.IsNullOrWhiteSpace(_remoteHost)
                ? ShooterRemoteStateSyncDefaults.DefaultHost
                : _remoteHost.Trim();
            var port = _remotePort;
            if (port <= 0 || port > 65535)
            {
                _lastError = "远程状态同步端口必须在 1-65535 范围内。";
                return false;
            }

            _remoteHost = host;
            _remotePort = port;
            _remoteSessionToken = NormalizeRemoteText(_remoteSessionToken, ShooterRemoteStateSyncDefaults.DefaultSessionToken);
            _remoteRegion = NormalizeRemoteText(_remoteRegion, ShooterRemoteStateSyncDefaults.DefaultRegion);
            _remoteServerId = NormalizeRemoteText(_remoteServerId, ShooterRemoteStateSyncDefaults.DefaultServerId);
            endpoint = new ShooterClientNetworkEndpoint(host, port);
            return true;
        }

        private static string NormalizeRemoteText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private void DrawRemoteServerControls(bool busy)
        {
            ProbeRemoteServerIfNeeded();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Shooter 状态同步服务器", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(_remoteServerStatusText, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(busy);
            if (GUILayout.Button("开启/重启服务器", GUILayout.Height(22)))
            {
                StartRemoteServerProcess();
            }

            if (GUILayout.Button("检查服务器", GUILayout.Height(22)))
            {
                ProbeRemoteServerNow();
            }

            if (GUILayout.Button("停止服务器", GUILayout.Height(22)))
            {
                StopRemoteServerProcess();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("会打开独立命令行窗口运行 Server/Orleans/tools/restart_shooter_state_sync.bat；状态通过当前 Host/Port 的 TCP 探测判断。", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void ProbeRemoteServerIfNeeded()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextRemoteServerProbeTime)
            {
                return;
            }

            _nextRemoteServerProbeTime = now + 2d;
            ProbeRemoteServerNow();
        }

        private void ProbeRemoteServerNow()
        {
            var host = string.IsNullOrWhiteSpace(_remoteHost) ? ShooterRemoteStateSyncDefaults.DefaultHost : _remoteHost.Trim();
            var port = _remotePort;
            if (port <= 0 || port > 65535)
            {
                _remoteServerReachable = false;
                _remoteServerStatusText = "服务器: 端口无效";
                return;
            }

            _remoteServerReachable = CanConnectTcp(host, port, 80);
            _remoteServerStatusText = _remoteServerReachable
                ? $"服务器: 已开启 ({host}:{port})"
                : $"服务器: 未连接 ({host}:{port})";
        }

        private static bool CanConnectTcp(string host, int port, int timeoutMilliseconds)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                {
                    return false;
                }

                client.EndConnect(result);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private void StartRemoteServerProcess()
        {
            try
            {
                NormalizeRemoteLobbyFields();
                var scriptPath = ResolveWorkspacePath("Server/Orleans/tools/restart_shooter_state_sync.bat");
                if (!File.Exists(scriptPath))
                {
                    _lastError = $"未找到服务器启动脚本：{scriptPath}";
                    Repaint();
                    return;
                }

                var arguments = $"/c start \"AbilityKit Shooter Server\" cmd /k \"\"{scriptPath}\" -TcpPort {_remotePort}\"";
                DiagnosticsProcess.Start(new DiagnosticsProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    WorkingDirectory = ResolveWorkspacePath(string.Empty),
                    UseShellExecute = false,
                    CreateNoWindow = false
                });

                _nextRemoteServerProbeTime = EditorApplication.timeSinceStartup + 1d;
                _remoteServerStatusText = $"服务器: 正在启动 ({_remoteHost}:{_remotePort})";
                _lastError = "已打开 Shooter 状态同步服务器命令行窗口。";
                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"Start Shooter server failed: {ex.Message}";
                UnityEngine.Debug.LogException(ex);
                Repaint();
            }
        }

        private void StopRemoteServerProcess()
        {
            try
            {
                var scriptPath = ResolveWorkspacePath("Server/Orleans/tools/abilitykit_process_utils.ps1");
                if (!File.Exists(scriptPath))
                {
                    _lastError = $"未找到服务器停止工具：{scriptPath}";
                    Repaint();
                    return;
                }

                var command = $". '{scriptPath}'; Stop-AbilityKitServices -Ports @({_remotePort},12111,31001) -CommandPatterns @('AbilityKit.Orleans.ShooterSmoke.csproj') -GraceSeconds 2";
                DiagnosticsProcess.Start(new DiagnosticsProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                    WorkingDirectory = ResolveWorkspacePath(string.Empty),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                _nextRemoteServerProbeTime = EditorApplication.timeSinceStartup + 1d;
                _remoteServerStatusText = "服务器: 正在停止";
                _lastError = "已发送 Shooter 状态同步服务器停止命令。";
                Repaint();
            }
            catch (Exception ex)
            {
                _lastError = $"Stop Shooter server failed: {ex.Message}";
                UnityEngine.Debug.LogException(ex);
                Repaint();
            }
        }

        private static string ResolveWorkspacePath(string relativePath)
        {
            var unityRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var workspaceRoot = Directory.GetParent(unityRoot)?.FullName ?? unityRoot;
            return string.IsNullOrWhiteSpace(relativePath)
                ? workspaceRoot
                : Path.GetFullPath(Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private void DrawRemoteRoomSelection(bool busy)
        {
            EditorGUI.BeginDisabledGroup(busy);
            _remoteRoomFilter = EditorGUILayout.TextField("房间筛选", _remoteRoomFilter);
            _remoteShowFullRooms = EditorGUILayout.ToggleLeft("显示满员/运行中房间（Web/Sandbox 可能已开战）", _remoteShowFullRooms);
            EditorGUI.EndDisabledGroup();

            BuildRemoteVisibleRooms();
            if (_remoteVisibleRooms.Count == 0)
            {
                var emptyText = _remoteRooms.Count == 0 ? "<none>" : "<no matched rooms>";
                EditorGUILayout.LabelField("房间", string.IsNullOrWhiteSpace(_remoteSelectedRoomId) ? emptyText : _remoteSelectedRoomId, EditorStyles.miniLabel);
                return;
            }

            var labels = new string[_remoteVisibleRooms.Count];
            for (var i = 0; i < _remoteVisibleRooms.Count; i++)
            {
                labels[i] = FormatRemoteRoomLabel(_remoteVisibleRooms[i]);
            }

            if (!SelectRemoteVisibleRoom(_remoteSelectedRoomId))
            {
                _remoteSelectedRoomIndex = 0;
                _remoteSelectedRoomId = _remoteVisibleRooms[0].RoomId;
            }

            EditorGUI.BeginDisabledGroup(busy);
            var nextIndex = EditorGUILayout.Popup("房间", ClampIndex(_remoteSelectedRoomIndex, _remoteVisibleRooms.Count), labels);
            if (nextIndex != _remoteSelectedRoomIndex || string.IsNullOrWhiteSpace(_remoteSelectedRoomId))
            {
                _remoteSelectedRoomIndex = nextIndex;
                _remoteSelectedRoomId = _remoteVisibleRooms[nextIndex].RoomId;
            }

            var selected = FindRemoteRoom(_remoteSelectedRoomId);
            if (selected.HasValue)
            {
                EditorGUILayout.HelpBox(FormatRemoteRoomDetail(selected.Value), MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_remoteSelectedRoomId));
            if (GUILayout.Button("进入所选房间", GUILayout.Height(20)))
            {
                StartRemoteStateSyncInternal();
            }
            if (GUILayout.Button("清除选择", GUILayout.Height(20)))
            {
                _remoteSelectedRoomId = string.Empty;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        private async void GuestLoginRemoteAsync()
        {
            await ExecuteRemoteLobbyOperationAsync(async client =>
            {
                var result = await client.GuestLoginAsync(new ShooterGatewayGuestLoginRequest($"unity-shooter-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"));
                if (!result.Success || string.IsNullOrWhiteSpace(result.SessionToken))
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message) ? "GuestLogin failed." : result.Message);
                }

                _remoteSessionToken = result.SessionToken;
                _remoteAccountId = result.AccountId;
                _lastError = "游客登录成功。";
            });
        }

        private async void RefreshRemoteRoomsAsync()
        {
            await ExecuteRemoteLobbyOperationAsync(async client =>
            {
                NormalizeRemoteLobbyFields();
                var result = await client.ListRoomsAsync(new ShooterGatewayListRoomsRequest(_remoteSessionToken, _remoteRegion, _remoteServerId, roomType: ShooterGameplay.RoomType));
                if (!result.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message) ? "ListRooms failed." : result.Message);
                }

                _remoteRooms.Clear();
                _remoteRooms.AddRange(result.Rooms);
                SelectRemoteRoom(_remoteSelectedRoomId);
                BuildRemoteVisibleRooms();
                _lastError = $"已刷新 {_remoteRooms.Count} 个 Shooter 房间，当前显示 {_remoteVisibleRooms.Count} 个。";
            });
        }

        private async void CreateRemoteRoomAsync(bool startAfterCreate)
        {
            await ExecuteRemoteLobbyOperationAsync(async client =>
            {
                NormalizeRemoteLobbyFields();
                var spec = ShooterRoomLaunchSpec.CreateDefault($"unity-{_controlledPlayerId}");
                var request = new ShooterGatewayCreateRoomRequest(
                    _remoteSessionToken,
                    _remoteRegion,
                    _remoteServerId,
                    ShooterGameplay.RoomType,
                    _remoteRoomTitle,
                    isPublic: true,
                    _remoteRoomMaxPlayers,
                    spec.Tags);
                var result = await client.CreateRoomAsync(request);
                if (!result.Success || string.IsNullOrWhiteSpace(result.RoomId))
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message) ? "CreateRoom failed." : result.Message);
                }

                _remoteSelectedRoomId = result.RoomId;
                await RefreshRemoteRoomsForClientAsync(client);
                _lastError = startAfterCreate ? "房间已创建，正在启动。" : $"房间已创建：{result.RoomId}";
            });

            if (startAfterCreate && !string.IsNullOrWhiteSpace(_remoteSelectedRoomId))
            {
                StartRemoteStateSyncInternal();
            }
        }

        private async System.Threading.Tasks.Task ExecuteRemoteLobbyOperationAsync(Func<ShooterRoomGatewayRoomClient, System.Threading.Tasks.Task> operation)
        {
            if (_remoteLobbyPending || _remoteLaunchPending || _running)
            {
                return;
            }

            if (!TryBuildRemoteEndpoint(out var endpoint))
            {
                Repaint();
                return;
            }

            try
            {
                _remoteLobbyPending = true;
                _lastError = $"正在连接 {endpoint.Host}:{endpoint.Port} ...";
                Repaint();

                using var launcher = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
                launcher.Open(endpoint);
                var client = new ShooterRoomGatewayRoomClient(launcher.GatewayConnection);
                await operation(client);
                SaveConfigToSessionState();
            }
            catch (Exception ex)
            {
                _lastError = $"Remote lobby failed: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                _remoteLobbyPending = false;
                Repaint();
            }
        }

        private async System.Threading.Tasks.Task RefreshRemoteRoomsForClientAsync(ShooterRoomGatewayRoomClient client)
        {
            var result = await client.ListRoomsAsync(new ShooterGatewayListRoomsRequest(_remoteSessionToken, _remoteRegion, _remoteServerId, roomType: ShooterGameplay.RoomType));
            if (result.Success)
            {
                _remoteRooms.Clear();
                _remoteRooms.AddRange(result.Rooms);
                SelectRemoteRoom(_remoteSelectedRoomId);
                BuildRemoteVisibleRooms();
            }
        }

        private void NormalizeRemoteLobbyFields()
        {
            _remoteSessionToken = NormalizeRemoteText(_remoteSessionToken, ShooterRemoteStateSyncDefaults.DefaultSessionToken);
            _remoteRegion = NormalizeRemoteText(_remoteRegion, ShooterRemoteStateSyncDefaults.DefaultRegion);
            _remoteServerId = NormalizeRemoteText(_remoteServerId, ShooterRemoteStateSyncDefaults.DefaultServerId);
            _remoteRoomTitle = NormalizeRemoteText(_remoteRoomTitle, "Unity Shooter Room");
            _remoteRoomMaxPlayers = Math.Max(1, Math.Min(ShooterGameplay.DefaultMaxPlayers, _remoteRoomMaxPlayers));
        }

        private void SelectRemoteRoom(string roomId)
        {
            BuildRemoteVisibleRooms();
            if (string.IsNullOrWhiteSpace(roomId))
            {
                _remoteSelectedRoomIndex = 0;
                _remoteSelectedRoomId = _remoteVisibleRooms.Count == 0 ? string.Empty : _remoteVisibleRooms[0].RoomId;
                return;
            }

            if (SelectRemoteVisibleRoom(roomId))
            {
                return;
            }

            _remoteSelectedRoomIndex = 0;
        }

        private void BuildRemoteVisibleRooms()
        {
            _remoteVisibleRooms.Clear();
            var filter = _remoteRoomFilter?.Trim() ?? string.Empty;
            for (var i = 0; i < _remoteRooms.Count; i++)
            {
                var room = _remoteRooms[i];
                if (!_remoteShowFullRooms && !room.HasOpenSlot)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(filter) && !RemoteRoomMatchesFilter(in room, filter))
                {
                    continue;
                }

                _remoteVisibleRooms.Add(room);
            }
        }

        private bool SelectRemoteVisibleRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            for (var i = 0; i < _remoteVisibleRooms.Count; i++)
            {
                if (string.Equals(_remoteVisibleRooms[i].RoomId, roomId, StringComparison.Ordinal))
                {
                    _remoteSelectedRoomIndex = i;
                    _remoteSelectedRoomId = roomId;
                    return true;
                }
            }

            return false;
        }

        private ShooterGatewayRoomSummary? FindRemoteRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            for (var i = 0; i < _remoteRooms.Count; i++)
            {
                if (string.Equals(_remoteRooms[i].RoomId, roomId, StringComparison.Ordinal))
                {
                    return _remoteRooms[i];
                }
            }

            return null;
        }

        private static bool RemoteRoomMatchesFilter(in ShooterGatewayRoomSummary room, string filter)
        {
            return ContainsIgnoreCase(room.RoomId, filter)
                || ContainsIgnoreCase(room.Title, filter)
                || ContainsIgnoreCase(room.OwnerAccountId, filter)
                || ContainsIgnoreCase(GetRemoteRoomSource(in room), filter);
        }

        private static bool ContainsIgnoreCase(string value, string filter)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatRemoteRoomLabel(in ShooterGatewayRoomSummary room)
        {
            var title = string.IsNullOrWhiteSpace(room.Title) ? room.RoomId : room.Title;
            var slot = room.MaxPlayers <= 0 ? $"{room.PlayerCount}/∞" : $"{room.PlayerCount}/{room.MaxPlayers}";
            var state = room.HasOpenSlot ? "可加入" : "满员/运行中";
            return $"[{GetRemoteRoomSource(in room)}] {title} [{slot}] {state} {room.RoomId}";
        }

        private static string FormatRemoteRoomDetail(in ShooterGatewayRoomSummary room)
        {
            var owner = string.IsNullOrWhiteSpace(room.OwnerAccountId) ? "<unknown>" : room.OwnerAccountId;
            return $"来源：{GetRemoteRoomSource(in room)}\n房间：{room.DisplayName}\n人数：{room.PlayerCount}/{(room.MaxPlayers <= 0 ? "∞" : room.MaxPlayers.ToString())}\nOwner：{owner}\n提示：Web/Sandbox 已开战房间会在进入后直接订阅运行中战斗。";
        }

        private static string GetRemoteRoomSource(in ShooterGatewayRoomSummary room)
        {
            if (room.Tags != null && room.Tags.TryGetValue("source", out var source) && !string.IsNullOrWhiteSpace(source))
            {
                if (source.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0) return "Web";
                if (source.IndexOf("sandbox", StringComparison.OrdinalIgnoreCase) >= 0) return "Sandbox";
                if (source.IndexOf("unity", StringComparison.OrdinalIgnoreCase) >= 0) return "Unity";
                return source;
            }

            if (!string.IsNullOrWhiteSpace(room.Title) && room.Title.IndexOf("Sandbox", StringComparison.OrdinalIgnoreCase) >= 0) return "Sandbox";
            if (!string.IsNullOrWhiteSpace(room.Title) && room.Title.IndexOf("Admin", StringComparison.OrdinalIgnoreCase) >= 0) return "Web";
            if (!string.IsNullOrWhiteSpace(room.Title) && room.Title.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0) return "Unity";
            return "Room";
        }

        // =====================================================================
        // Play Mode Attach Lifecycle
        // =====================================================================

        private void AttachInternal()
        {
            _lastError = string.Empty;

            if (!Application.isPlaying)
            {
                if (!TryBuildSessionOptions(out _))
                {
                    Repaint();
                    return;
                }

                SaveConfigToSessionState();
                SessionState.SetBool(PendingHostAttachKey, true);
                _lastError = "已保存当前配置，正在进入 Play 模式并启动 Shooter 宿主。";
                EditorApplication.isPlaying = true;
                Repaint();
                return;
            }

            if (!TryBuildSessionOptions(out var options))
            {
                Repaint();
                return;
            }

            var host = EnsurePlayModeHost(options);
            if (host == null)
            {
                _lastError = "未能发布 PlayMode 会话宿主。";
                Repaint();
                return;
            }

            _attachedHost = host;
            _session = host.Session;
            _running = true;
            _paused = false;
            _diagnostics.Reset();
            _diagnostics.IsRunning = host.IsRunning;

            // 窗口只观察 PlayMode 宿主，不再自行推进逻辑；网络调参通过共享 registry 热更新。
            EditorApplication.update -= OnAttachUpdate;
            ShooterHostSessionRegistry.HostsChanged -= OnRegistryHostsChanged;
            EditorApplication.update += OnAttachUpdate;
            ShooterHostSessionRegistry.HostsChanged += OnRegistryHostsChanged;

            Repaint();
        }

        private void DetachInternal()
        {
            EditorApplication.update -= OnAttachUpdate;
            ShooterHostSessionRegistry.HostsChanged -= OnRegistryHostsChanged;

            _attachedHost = null;
            _session = null;
            _running = false;
            _paused = false;
            _diagnostics.Reset();

            Repaint();
        }

        private void OnRegistryHostsChanged()
        {
            // 宿主随 Play 模式退出而消失时，窗口自动解除挂接。
            if (_attachedHost != null && !_attachedHost.IsRunning &&
                ShooterHostSessionRegistry.Active == null)
            {
                DetachInternal();
            }
        }

        private void OnAttachUpdate()
        {
            if (_attachedHost == null)
            {
                return;
            }

            // 宿主已销毁或 Play 模式退出时，窗口自动回到未运行状态。
            if (!_attachedHost.IsRunning && _attachedHost.Session == null)
            {
                _lastError = "PlayMode 会话已结束。";
                DetachInternal();
                return;
            }

            var session = _attachedHost.Session;
            if (!ReferenceEquals(_session, session))
            {
                _session = session;
                _diagnostics.Reset();
            }

            _diagnostics.IsRunning = _attachedHost.IsRunning;

            if (session != null)
            {
                var snapshot = session.Runtime.GetSnapshot();
                UpdateDiagnostics(in snapshot, null!);
            }

            Repaint();
        }

        private bool TryBuildSessionOptions(out ShooterPlayModeSessionOptions options)
        {
            options = default;

            if (_selectedSyncIndex >= SyncModes.Count || !SyncModes[_selectedSyncIndex].Implemented)
            {
                _lastError = "当前选择的同步模式尚未实现。";
                return false;
            }

            var syncMode = SyncModes[_selectedSyncIndex];
            var networkProfile = GetCurrentNetworkProfile();
            options = new ShooterPlayModeSessionOptions(
                syncMode.Model,
                ShooterGameplay.DefaultTickRate,
                _playerCount,
                _randomSeed,
                _controlledPlayerId,
                _enableAuthoritativeWorld,
                networkProfile.BaseLatencyMs,
                networkProfile.JitterMs,
                (float)networkProfile.PacketLossRate,
                (float)networkProfile.ReorderRate,
                networkProfile.BandwidthKbps,
                worldScale: 1f,
                networkName: GetCurrentNetworkName());
            return true;
        }

        private void TryStartPendingHostSession()
        {
            if (!SessionState.GetBool(PendingHostAttachKey, false) || !Application.isPlaying)
            {
                return;
            }

            RestoreConfigFromSessionState();
            ApplyCustomNetwork();
            SessionState.SetBool(PendingHostAttachKey, false);

            if (!TryBuildSessionOptions(out var options))
            {
                Repaint();
                return;
            }

            var host = EnsurePlayModeHost(options);
            if (host == null)
            {
                _lastError = "进入 Play 模式后未能启动 Shooter 演示宿主。";
                Repaint();
                return;
            }

            _driveMode = ShooterDemoDriveMode.HostAttach;
            _attachedHost = host;
            _session = host.Session;
            _running = true;
            _paused = false;
            _diagnostics.Reset();
            _diagnostics.IsRunning = host.IsRunning;

            EditorApplication.update -= OnAttachUpdate;
            ShooterHostSessionRegistry.HostsChanged -= OnRegistryHostsChanged;
            EditorApplication.update += OnAttachUpdate;
            ShooterHostSessionRegistry.HostsChanged += OnRegistryHostsChanged;

            _lastError = string.Empty;
            Repaint();
        }

        private static IShooterSessionHost? EnsurePlayModeHost(ShooterPlayModeSessionOptions options)
        {
            var host = ShooterHostSessionRegistry.Active;
            if (host == null || !host.IsRunning)
            {
                ShooterPlayModeSessionHost.Start(options);
                host = ShooterHostSessionRegistry.Active;
            }

            return host;
        }

        private void StepInternal()
        {
            if (!_running || _session == null) return;
            TickSession(1f / ShooterGameplay.DefaultTickRate);
        }

        // =====================================================================
        // Editor Update Loop
        // =====================================================================

        private void OnEditorUpdate()
        {
            if (!_running || _session == null) return;
            if (_paused) return;

            var now = EditorApplication.timeSinceStartup;
            var delta = (float)((now - _lastTime) * _timeScale);
            _lastTime = now;

            // 限制单帧推进量，避免编辑器卡顿后一次性追帧过多。
            if (delta > 0.1f) delta = 0.1f;

            TickSession(delta);

            SceneView.RepaintAll();
            Repaint();
        }

        private void TickSession(float deltaSeconds)
        {
            if (_session == null || _editorRunner == null) return;

            _editorRunner.Tick(deltaSeconds);
            _lastEditorRunnerInput = _editorRunner.LastInput;

            var snapshot = _session.Runtime.GetSnapshot();
            UpdateDiagnostics(in snapshot, null!);
        }

        // =====================================================================
        // SceneView Rendering
        // =====================================================================

        private void OnSceneViewGUI(SceneView sceneView)
        {
            if (!_running && !_showingSpecBaseline) return;
            _sink.DrawSceneView();
        }

        // =====================================================================
        // Diagnostics Update
        // =====================================================================

        private void UpdateDiagnostics(in ShooterStateSnapshotPayload snapshot, object tickResult)
        {
            if (_session == null) return;

            var diagnostics = ShooterHostDiagnosticsProjector.ProjectFromSession(
                _session,
                in snapshot,
                _diagnostics.TotalEvents);
            _diagnostics.Apply(in diagnostics);
        }

        // =====================================================================
        // Network Helpers
        // =====================================================================

        private void SaveConfigToSessionState()
        {
            SessionState.SetBool(HasSavedConfigKey, true);
            SessionState.SetInt(SyncIndexKey, _selectedSyncIndex);
            SessionState.SetInt(NetworkProviderIndexKey, _selectedNetworkProviderIndex);
            SessionState.SetInt(NetworkPresetIndexKey, _selectedNetworkPresetIndex);
            SessionState.SetBool(AuthoritativeWorldKey, _enableAuthoritativeWorld);
            SessionState.SetBool(ShowDivergenceKey, _showDivergence);
            SessionState.SetInt(PlayerCountKey, _playerCount);
            SessionState.SetInt(InitialHpKey, _initialHp);
            SessionState.SetInt(RandomSeedKey, _randomSeed);
            SessionState.SetInt(ControlledPlayerIdKey, _controlledPlayerId);
            SessionState.SetInt(LatencyMsKey, _latencyMs);
            SessionState.SetInt(JitterMsKey, _jitterMs);
            SessionState.SetFloat(PacketLossRateKey, (float)_packetLossRate);
            SessionState.SetFloat(ReorderRateKey, (float)_reorderRate);
            SessionState.SetInt(BandwidthKbpsKey, _bandwidthKbps);
            SessionState.SetString(RemoteHostKey, _remoteHost);
            SessionState.SetInt(RemotePortKey, _remotePort);
            SessionState.SetString(RemoteSessionTokenKey, _remoteSessionToken);
            SessionState.SetString(RemoteAccountIdKey, _remoteAccountId);
            SessionState.SetString(RemoteRegionKey, _remoteRegion);
            SessionState.SetString(RemoteServerIdKey, _remoteServerId);
            SessionState.SetString(RemoteRoomTitleKey, _remoteRoomTitle);
            SessionState.SetInt(RemoteRoomMaxPlayersKey, _remoteRoomMaxPlayers);
            SessionState.SetString(RemoteSelectedRoomIdKey, _remoteSelectedRoomId);
            SessionState.SetString(RemoteRoomFilterKey, _remoteRoomFilter);
            SessionState.SetBool(RemoteShowFullRoomsKey, _remoteShowFullRooms);
        }

        private void RestoreConfigFromSessionState()
        {
            _selectedSyncIndex = ClampIndex(SessionState.GetInt(SyncIndexKey, _selectedSyncIndex), SyncModes.Count);
            _selectedNetworkProviderIndex = Math.Max(0, SessionState.GetInt(NetworkProviderIndexKey, _selectedNetworkProviderIndex));
            _selectedNetworkPresetIndex = ClampIndex(SessionState.GetInt(NetworkPresetIndexKey, _selectedNetworkPresetIndex), NetworkPresets.Count);
            _enableAuthoritativeWorld = SessionState.GetBool(AuthoritativeWorldKey, _enableAuthoritativeWorld);
            _showDivergence = SessionState.GetBool(ShowDivergenceKey, _showDivergence);
            _playerCount = Math.Max(1, Math.Min(ShooterGameplay.DefaultMaxPlayers, SessionState.GetInt(PlayerCountKey, _playerCount)));
            _initialHp = Math.Max(1, SessionState.GetInt(InitialHpKey, _initialHp));
            _randomSeed = SessionState.GetInt(RandomSeedKey, _randomSeed);
            _controlledPlayerId = Math.Max(1, Math.Min(_playerCount, SessionState.GetInt(ControlledPlayerIdKey, _controlledPlayerId)));
            _latencyMs = Math.Max(0, SessionState.GetInt(LatencyMsKey, _latencyMs));
            _jitterMs = Math.Max(0, SessionState.GetInt(JitterMsKey, _jitterMs));
            _packetLossRate = Clamp01(SessionState.GetFloat(PacketLossRateKey, (float)_packetLossRate));
            _reorderRate = Clamp01(SessionState.GetFloat(ReorderRateKey, (float)_reorderRate));
            _bandwidthKbps = Math.Max(0, SessionState.GetInt(BandwidthKbpsKey, _bandwidthKbps));
            _remoteHost = SessionState.GetString(RemoteHostKey, _remoteHost);
            _remotePort = SessionState.GetInt(RemotePortKey, _remotePort);
            _remoteSessionToken = SessionState.GetString(RemoteSessionTokenKey, _remoteSessionToken);
            _remoteAccountId = SessionState.GetString(RemoteAccountIdKey, _remoteAccountId);
            _remoteRegion = SessionState.GetString(RemoteRegionKey, _remoteRegion);
            _remoteServerId = SessionState.GetString(RemoteServerIdKey, _remoteServerId);
            _remoteRoomTitle = SessionState.GetString(RemoteRoomTitleKey, _remoteRoomTitle);
            _remoteRoomMaxPlayers = Math.Max(1, Math.Min(ShooterGameplay.DefaultMaxPlayers, SessionState.GetInt(RemoteRoomMaxPlayersKey, _remoteRoomMaxPlayers)));
            _remoteSelectedRoomId = SessionState.GetString(RemoteSelectedRoomIdKey, _remoteSelectedRoomId);
            _remoteRoomFilter = SessionState.GetString(RemoteRoomFilterKey, _remoteRoomFilter);
            _remoteShowFullRooms = SessionState.GetBool(RemoteShowFullRoomsKey, _remoteShowFullRooms);
            _inputProvider.ControlledPlayerId = _controlledPlayerId;
        }

        private static int ClampIndex(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(count - 1, value));
        }

        private static double Clamp01(double value)
        {
            if (value < 0d)
            {
                return 0d;
            }

            return value > 1d ? 1d : value;
        }

        private void ApplyNetworkPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= NetworkPresets.Count) return;
            _selectedNetworkPresetIndex = presetIndex;

            var preset = NetworkPresets[presetIndex];
            _latencyMs = preset.Profile.BaseLatencyMs;
            _jitterMs = preset.Profile.JitterMs;
            _packetLossRate = preset.Profile.PacketLossRate;
            _reorderRate = preset.Profile.ReorderRate;
            _bandwidthKbps = preset.Profile.BandwidthKbps;

            ApplyCustomNetwork();
        }

        private void ApplyCustomNetwork()
        {
            var profile = new NetworkConditionProfile(
                _latencyMs, _jitterMs, _packetLossRate, _reorderRate, _bandwidthKbps);

            ShooterNetworkConditionRegistry.Builtin.ApplyProfile(profile);

            // 会话运行中立即通过共享 runner 路径热更新网络配置。
            if (_editorRunner != null)
            {
                _editorRunner.ApplyNetwork(profile);
            }
            else if (_session != null)
            {
                _session.ApplyNetwork(profile, GetCurrentNetworkName());
            }
        }

        private NetworkConditionProfile GetCurrentNetworkProfile()
        {
            var providers = ShooterNetworkConditionRegistry.All;
            if (_selectedNetworkProviderIndex > 0 && _selectedNetworkProviderIndex < providers.Count)
            {
                var provider = providers[_selectedNetworkProviderIndex];
                if (provider.IsActive) return provider.Profile;
            }

            return new NetworkConditionProfile(
                _latencyMs, _jitterMs, _packetLossRate, _reorderRate, _bandwidthKbps);
        }

        private string GetCurrentNetworkName()
        {
            var providers = ShooterNetworkConditionRegistry.All;
            if (_selectedNetworkProviderIndex > 0 && _selectedNetworkProviderIndex < providers.Count)
            {
                return providers[_selectedNetworkProviderIndex].DisplayName;
            }

            // 优先显示匹配的预设名称，否则显示自定义参数摘要。
            foreach (var preset in NetworkPresets)
            {
                if (preset.Profile.BaseLatencyMs == _latencyMs
                    && preset.Profile.JitterMs == _jitterMs
                    && Math.Abs(preset.Profile.PacketLossRate - _packetLossRate) < 0.0001d
                    && Math.Abs(preset.Profile.ReorderRate - _reorderRate) < 0.0001d
                    && preset.Profile.BandwidthKbps == _bandwidthKbps)
                {
                    return preset.DisplayName;
                }
            }

            return $"自定义 ({_latencyMs}ms/{_jitterMs}ms)";
        }

        private static string DescribeProfile(NetworkConditionProfile p)
        {
            return $"{p.BaseLatencyMs}ms ±{p.JitterMs}ms  丢包:{p.PacketLossRate:P1}  乱序:{p.ReorderRate:P1}  带宽:{p.BandwidthKbps}kbps";
        }

        private static string GetShortPresetName(string displayName)
        {
            // 从预设显示名里取括号前的短名，保持按钮宽度稳定。
            var paren = displayName.IndexOf('(');
            if (paren > 0) return displayName.Substring(0, paren).Trim();
            return displayName;
        }
    }
}
