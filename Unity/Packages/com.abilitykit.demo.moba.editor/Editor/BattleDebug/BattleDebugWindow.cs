using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Editor.Diagnostics;
using AbilityKit.Game.Flow.Battle.Replay;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public sealed class BattleDebugWindow : EditorWindow
    {
        private const string PreferencesPrefix = "AbilityKit.BattleDebug.";
        private const float DefaultRefreshIntervalSeconds = 0.25f;
        private const float MinRefreshIntervalSeconds = 0.05f;
        private const float MaxRefreshIntervalSeconds = 5f;
        private static readonly float[] RefreshIntervalOptions = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f };
        private static readonly string[] RefreshIntervalLabels = { "20Hz", "10Hz", "4Hz", "2Hz", "1Hz", "0.5Hz", "0.2Hz" };
        private const float MinEntityPaneWidth = 160f;
        private const float MaxEntityPaneWidth = 420f;
        private const float MinInspectorPaneWidth = 260f;
        private const float MaxInspectorPaneWidth = 480f;
        private const float InspectorColumnThreshold = 960f;
        private const float SplitterWidth = 5f;

        private string _filter;
        private string _jumpId;
        private string _selectionStatus;
        private Vector2 _entityScroll;
        private Vector2 _detailScroll;
        private Vector2 _secondaryDetailScroll;

        private readonly List<BattleDebugEntityId> _visibleEntities =
            new List<BattleDebugEntityId>(256);
        private readonly List<BattleDebugEntityId> _entityRefreshBuffer =
            new List<BattleDebugEntityId>(256);
        private readonly List<IBattleDebugPanel> _visiblePanels = new List<IBattleDebugPanel>(16);
        private readonly Dictionary<IBattleDebugPanel, BattleDebugPanelWidgetAdapter> _widgetAdapters =
            new Dictionary<IBattleDebugPanel, BattleDebugPanelWidgetAdapter>();
        private int _selectedActorId;
        private int _totalEntityCount;
        private double _nextRefreshAt;
        private float _entityPaneWidth = 220f;
        private float _inspectorPaneWidth = 320f;
        private bool _resizingEntityPane;
        private bool _resizingInspectorPane;
        private bool _resizingDiagnosticSplit;
        private bool _autoRefresh = true;
        private float _refreshIntervalSeconds = DefaultRefreshIntervalSeconds;
        private bool _renderReplayPresentation = true;
        private bool _showEntityPane = true;
        private bool _showStatusArea = true;
        private bool _showSelectionInspector = true;
        private readonly BattleDebugDiagnosticSource _diagnosticSource = new BattleDebugDiagnosticSource();
        private readonly BattleDiagnosticWorkspaceState _diagnosticWorkspaceState =
            new BattleDiagnosticWorkspaceState();
        private readonly BattleDebugSelectionInspector _selectionInspector =
            new BattleDebugSelectionInspector();
        private string _fileStatus;
        private MessageType _fileStatusType = MessageType.None;

        private BattleDebugWorkspace _workspace;
        private int _selectedActorPanelIndex;
        private int _selectedDiagnosticsPanelIndex;
        private int _selectedSecondaryDiagnosticsPanelIndex = 1;
        private bool _showSecondaryDiagnostics;
        private float _diagnosticSplitRatio = 0.58f;
        private string _primaryDiagnosticModuleId = string.Empty;
        private string _secondaryDiagnosticModuleId = string.Empty;
        private string _primaryDiagnosticWidgetId = string.Empty;
        private string _secondaryDiagnosticWidgetId = string.Empty;
        private string _activeWorkspacePresetId = string.Empty;

        [MenuItem("Tools/AbilityKit/Demos/Moba/Battle/战斗调试")]
        private static void Open()
        {
            GetWindow<BattleDebugWindow>("战斗调试");
        }

        private void OnEnable()
        {
            _entityPaneWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(PreferencesPrefix + "EntityPaneWidth", 220f),
                MinEntityPaneWidth,
                MaxEntityPaneWidth);
            _inspectorPaneWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(PreferencesPrefix + "InspectorPaneWidth", 320f),
                MinInspectorPaneWidth,
                MaxInspectorPaneWidth);
            _workspace = (BattleDebugWorkspace)Mathf.Clamp(
                EditorPrefs.GetInt(PreferencesPrefix + "Workspace", 0),
                0,
                1);
            _selectedActorPanelIndex = Mathf.Max(
                0,
                EditorPrefs.GetInt(PreferencesPrefix + "ActorPanelIndex", 0));
            _selectedDiagnosticsPanelIndex = Mathf.Max(
                0,
                EditorPrefs.GetInt(PreferencesPrefix + "DiagnosticsPanelIndex", 0));
            _selectedSecondaryDiagnosticsPanelIndex = Mathf.Max(
                0,
                EditorPrefs.GetInt(PreferencesPrefix + "SecondaryDiagnosticsPanelIndex", 1));
            _showSecondaryDiagnostics = EditorPrefs.GetBool(
                PreferencesPrefix + "ShowSecondaryDiagnostics",
                false);
            _diagnosticSplitRatio = Mathf.Clamp(
                EditorPrefs.GetFloat(PreferencesPrefix + "DiagnosticSplitRatio", 0.58f),
                0.3f,
                0.7f);
            _primaryDiagnosticModuleId = EditorPrefs.GetString(
                PreferencesPrefix + "PrimaryDiagnosticModuleId",
                string.Empty);
            _secondaryDiagnosticModuleId = EditorPrefs.GetString(
                PreferencesPrefix + "SecondaryDiagnosticModuleId",
                string.Empty);
            _primaryDiagnosticWidgetId = EditorPrefs.GetString(
                PreferencesPrefix + "PrimaryDiagnosticWidgetId",
                string.Empty);
            _secondaryDiagnosticWidgetId = EditorPrefs.GetString(
                PreferencesPrefix + "SecondaryDiagnosticWidgetId",
                string.Empty);
            _activeWorkspacePresetId = EditorPrefs.GetString(
                PreferencesPrefix + "WorkspacePresetId",
                string.Empty);
            _renderReplayPresentation = EditorPrefs.GetBool(PreferencesPrefix + "RenderReplayPresentation", true);
            _refreshIntervalSeconds = Mathf.Clamp(
                EditorPrefs.GetFloat(PreferencesPrefix + "RefreshIntervalSeconds", DefaultRefreshIntervalSeconds),
                MinRefreshIntervalSeconds,
                MaxRefreshIntervalSeconds);
            _showEntityPane = EditorPrefs.GetBool(PreferencesPrefix + "ShowEntityPane", true);
            _showStatusArea = EditorPrefs.GetBool(PreferencesPrefix + "ShowStatusArea", true);
            _showSelectionInspector = EditorPrefs.GetBool(PreferencesPrefix + "ShowSelectionInspector", true);
            _nextRefreshAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorPrefs.SetFloat(PreferencesPrefix + "EntityPaneWidth", _entityPaneWidth);
            EditorPrefs.SetFloat(PreferencesPrefix + "InspectorPaneWidth", _inspectorPaneWidth);
            EditorPrefs.SetFloat(PreferencesPrefix + "RefreshIntervalSeconds", _refreshIntervalSeconds);
            EditorPrefs.SetInt(PreferencesPrefix + "Workspace", (int)_workspace);
            EditorPrefs.SetInt(PreferencesPrefix + "ActorPanelIndex", _selectedActorPanelIndex);
            EditorPrefs.SetInt(PreferencesPrefix + "DiagnosticsPanelIndex", _selectedDiagnosticsPanelIndex);
            EditorPrefs.SetInt(
                PreferencesPrefix + "SecondaryDiagnosticsPanelIndex",
                _selectedSecondaryDiagnosticsPanelIndex);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowSecondaryDiagnostics", _showSecondaryDiagnostics);
            EditorPrefs.SetFloat(PreferencesPrefix + "DiagnosticSplitRatio", _diagnosticSplitRatio);
            EditorPrefs.SetString(
                PreferencesPrefix + "PrimaryDiagnosticModuleId",
                _primaryDiagnosticModuleId ?? string.Empty);
            EditorPrefs.SetString(
                PreferencesPrefix + "SecondaryDiagnosticModuleId",
                _secondaryDiagnosticModuleId ?? string.Empty);
            EditorPrefs.SetString(
                PreferencesPrefix + "PrimaryDiagnosticWidgetId",
                _primaryDiagnosticWidgetId ?? string.Empty);
            EditorPrefs.SetString(
                PreferencesPrefix + "SecondaryDiagnosticWidgetId",
                _secondaryDiagnosticWidgetId ?? string.Empty);
            EditorPrefs.SetString(
                PreferencesPrefix + "WorkspacePresetId",
                _activeWorkspacePresetId ?? string.Empty);
            EditorPrefs.SetBool(PreferencesPrefix + "RenderReplayPresentation", _renderReplayPresentation);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowEntityPane", _showEntityPane);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowStatusArea", _showStatusArea);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowSelectionInspector", _showSelectionInspector);
        }

        private void OnDestroy()
        {
            _diagnosticSource.Dispose();
        }

        private void OnEditorUpdate()
        {
            AutoRefresh();
        }

        private void OnGUI()
        {
            var isOffline = _diagnosticSource.IsOffline;
            var facade = isOffline ? null : BattleDebugFacadeProvider.Current;
            var diagnosticResolution = isOffline
                ? new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.Ready,
                    _diagnosticSource.Session,
                    null,
                    healthSnapshot: _diagnosticSource.HealthSnapshot)
                : BattleDebugDiagnosticSessionResolver.Resolve(facade, EditorApplication.isPlaying);
            SynchronizeDiagnosticWorkspace(in diagnosticResolution, isOffline);
            var hasLiveSession = !isOffline &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.NotPlaying &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.FacadeUnavailable &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.LogicSessionUnavailable;

            var selectedId = _selectedActorId != 0
                ? new BattleDebugEntityId(_selectedActorId)
                : default;
            IUnitFacade selectedUnit = null;
            if (hasLiveSession && selectedId.IsValid)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                requestRepaint: Repaint,
                selectActor: SelectActor,
                openTrace: OpenTrace,
                openEvents: OpenEvents,
                openEvent: OpenEvent,
                openRecentFailures: OpenRecentFailures,
                openConfig: OpenConfig,
                seekReplayFrame: CanSeekReplayFrame() ? SeekReplayFrame : null,
                diagnosticSession: diagnosticResolution.Session,
                skillRuntimeService: diagnosticResolution.SkillRuntimeService,
                diagnosticResolution: diagnosticResolution,
                isOffline: isOffline,
                workspaceState: _diagnosticWorkspaceState,
                availableContentWidth: ResolveAvailableContentWidth());

            DrawToolbar(in ctx);
            DrawFrameCursor(in ctx);
            if (_showStatusArea)
            {
                DrawSourceStatus(hasLiveSession, in diagnosticResolution);
                DrawLiveControls(hasLiveSession);
                DrawReplayControls();
            }
            else
            {
                DrawCriticalStatusMessages(in diagnosticResolution);
            }

            if (!isOffline && !hasLiveSession)
            {
                var message = EditorApplication.isPlaying
                    ? "当前没有活动中的 BattleLogicSession。可以启动战斗，或打开诊断 Artifact 进行离线浏览。"
                    : "当前处于编辑模式。打开诊断 Artifact 可离线浏览，或进入播放模式连接实时会话。";
                EditorGUILayout.HelpBox(message, MessageType.Info);
                return;
            }

            DrawWorkspace(in ctx, facade);

            AutoRefresh();
        }

        private void DrawToolbar(in BattleDebugContext ctx)
        {
            DrawViewToolbar(in ctx);
            DrawDataToolbar(in ctx);
        }

        private void DrawViewToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("过滤", GUILayout.Width(35));
            var newFilter = GUILayout.TextField(
                _filter ?? string.Empty,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(100),
                GUILayout.MaxWidth(260));
            if (!string.Equals(newFilter, _filter, StringComparison.Ordinal))
            {
                _filter = newFilter;
                RefreshEntities();
            }
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_filter));
            if (GUILayout.Button(new GUIContent("×", "清除实体过滤"), EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                _filter = string.Empty;
                RefreshEntities();
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Label(
                $"{_visibleEntities.Count}/{_totalEntityCount}",
                EditorStyles.miniLabel,
                GUILayout.Width(58));

            _showEntityPane = GUILayout.Toggle(
                _showEntityPane,
                new GUIContent("实体", "显示或收起 Actor 实体侧栏"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42));
            _showStatusArea = GUILayout.Toggle(
                _showStatusArea,
                new GUIContent("状态", "显示或收起数据源、现场和回放控制区"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42));
            _showSelectionInspector = GUILayout.Toggle(
                _showSelectionInspector,
                new GUIContent("检查器", "显示或收起持久 Selection Inspector"),
                EditorStyles.toolbarButton,
                GUILayout.Width(54));

            EditorGUI.BeginDisabledGroup(!_diagnosticWorkspaceState.Navigation.CanGoBack);
            if (GUILayout.Button(new GUIContent("◀", "返回上一个诊断选择"), EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                NavigateDiagnosticHistory(back: true);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!_diagnosticWorkspaceState.Navigation.CanGoForward);
            if (GUILayout.Button(new GUIContent("▶", "前进到下一个诊断选择"), EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                NavigateDiagnosticHistory(back: false);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    new GUIContent("↻", "立即刷新实体和当前面板"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(26)))
            {
                _nextRefreshAt = EditorApplication.timeSinceStartup + _refreshIntervalSeconds;
                RefreshEntities();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDataToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label(new GUIContent("数据", "诊断数据源、回放和导入导出操作"), GUILayout.Width(28));

            _renderReplayPresentation = GUILayout.Toggle(
                _renderReplayPresentation,
                new GUIContent("渲染表现", "关闭后 Replay 仅运行逻辑世界，不创建或驱动 View、HUD、VFX 和相机"),
                EditorStyles.toolbarButton,
                GUILayout.Width(68));

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || _diagnosticSource.IsOffline && BattleReplayControlProvider.Current == null);
            if (GUILayout.Button(new GUIContent("录像", "加载标准 FrameRecord 并驱动当前逻辑世界"), EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                OpenReplay();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(new GUIContent("打开", "打开 abilitykit-analysis.v1 诊断 Artifact"), EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                OpenArtifact();
            }

            EditorGUI.BeginDisabledGroup(!CanExportLiveSnapshot());
            if (GUILayout.Button(new GUIContent("导出", "捕获并导出当前实时 Battle Diagnostics"), EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                ExportLiveArtifact();
            }
            EditorGUI.EndDisabledGroup();

            if (_diagnosticSource.IsOffline &&
                GUILayout.Button(new GUIContent("返回实时", "关闭离线 Artifact 并返回实时会话"), EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                ReturnToLive();
            }

            var cmds = BattleDebugToolbarCommandRegistry.GetAll();
            for (int i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];
                if (cmd == null) continue;
                if (!cmd.IsVisible(in ctx)) continue;

                EditorGUI.BeginDisabledGroup(!cmd.IsEnabled(in ctx));
                if (GUILayout.Button(cmd.Label, EditorStyles.toolbarButton))
                {
                    cmd.Execute(in ctx);
                }
                EditorGUI.EndDisabledGroup();
            }

            GUILayout.FlexibleSpace();
            _autoRefresh = GUILayout.Toggle(
                _autoRefresh,
                new GUIContent("自动刷新", "仅控制此窗口的周期轮询，不影响底层诊断采集"),
                EditorStyles.toolbarButton,
                GUILayout.Width(70));
            DrawRefreshIntervalControl();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFrameCursor(in BattleDebugContext ctx)
        {
            var workspace = ctx.WorkspaceState;
            if (workspace == null || !workspace.Scope.IsValid)
            {
                return;
            }

            var cursor = workspace.FrameCursor;
            var latestFrame = ResolveLatestCompleteFrame(in ctx);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("帧游标", EditorStyles.miniBoldLabel, GUILayout.Width(42));

            EditorGUI.BeginChangeCheck();
            var requestedFrame = EditorGUILayout.DelayedIntField(
                cursor.HasFrame ? cursor.Frame : BattleDiagnosticFrames.Invalid,
                GUILayout.Width(72));
            if (EditorGUI.EndChangeCheck() && BattleDiagnosticFrames.IsValid(requestedFrame))
            {
                workspace.SetFrame(requestedFrame);
                cursor = workspace.FrameCursor;
            }

            GUILayout.Label(
                cursor.FollowsLive ? "跟随最新" : ResolveFrameCursorReason(cursor.ChangeReason),
                EditorStyles.miniLabel,
                GUILayout.Width(72));
            if (BattleDiagnosticFrames.IsValid(latestFrame))
            {
                GUILayout.Label($"最新 F{latestFrame}", EditorStyles.miniLabel, GUILayout.Width(72));
            }
            else
            {
                GUILayout.Label("最新帧不可用", EditorStyles.miniLabel, GUILayout.Width(82));
            }

            var selection = workspace.Selection;
            if (selection.IsValid)
            {
                GUILayout.Label(
                    $"{selection.Kind} #{selection.Id}",
                    EditorStyles.miniLabel,
                    GUILayout.MinWidth(110));
            }

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(
                cursor.FollowsLive || !BattleDiagnosticFrames.IsValid(latestFrame));
            if (GUILayout.Button(
                    new GUIContent("跟随最新", "将诊断帧游标恢复到当前数据源的最新完整帧"),
                    GUILayout.Width(68)))
            {
                workspace.SetFollowLive(true, latestFrame);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(ctx.SeekReplayFrame == null || !cursor.HasFrame);
            if (GUILayout.Button(
                    new GUIContent("定位 Replay", "暂停 Replay 并将逻辑世界定位到当前诊断帧游标"),
                    GUILayout.Width(82)))
            {
                if (ctx.SeekReplayFrame != null && ctx.SeekReplayFrame(cursor.Frame))
                {
                    _fileStatus = $"Replay 已定位到诊断帧 F{cursor.Frame}。";
                    _fileStatusType = MessageType.Info;
                }
                else
                {
                    _fileStatus = $"无法将 Replay 定位到诊断帧 F{cursor.Frame}；该帧可能超出录像范围。";
                    _fileStatusType = MessageType.Warning;
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private int ResolveLatestCompleteFrame(in BattleDebugContext ctx)
        {
            if (ctx.DiagnosticResolution.HasHealthSnapshot)
            {
                return ctx.DiagnosticResolution.HealthSnapshot.Value.LastSuccessfulStateFrame;
            }

            return ctx.IsOffline
                ? _diagnosticSource.LatestCompleteFrame
                : BattleDiagnosticFrames.Invalid;
        }

        private static string ResolveFrameCursorReason(BattleDiagnosticFrameCursorChangeReason reason)
        {
            switch (reason)
            {
                case BattleDiagnosticFrameCursorChangeReason.UserSelectedFrame:
                    return "手工设定";
                case BattleDiagnosticFrameCursorChangeReason.SelectionNavigation:
                    return "选择定位";
                case BattleDiagnosticFrameCursorChangeReason.RetainedRangeClamped:
                    return "保留区约束";
                case BattleDiagnosticFrameCursorChangeReason.SessionChanged:
                    return "会话切换";
                case BattleDiagnosticFrameCursorChangeReason.FollowLiveAdvanced:
                    return "最新推进";
                default:
                    return "固定帧";
            }
        }

        private void DrawSourceStatus(
            bool hasLiveSession,
            in BattleDebugDiagnosticSessionResolution diagnosticResolution)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("数据源", EditorStyles.miniBoldLabel, GUILayout.Width(42));
            if (_diagnosticSource.IsOffline)
            {
                GUILayout.Label("离线", GUILayout.Width(30));
                GUILayout.Label(_diagnosticSource.DisplayName, EditorStyles.miniLabel);
                var info = _diagnosticSource.Session.SessionInfo;
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Session={info.Scope.SessionId}  World={info.Scope.WorldId}  {info.ConnectionState}/{info.CaptureState}", EditorStyles.miniLabel);
                DrawDiagnosticRevisions(_diagnosticSource.Session, null, null, null);
            }
            else
            {
                var replay = BattleReplayControlProvider.Current;
                if (hasLiveSession && replay != null && replay.IsReplaySession)
                {
                    GUILayout.Label("录像回放", GUILayout.Width(52));
                    GUILayout.Label(replay.RenderPresentation ? "表现渲染" : "纯逻辑", GUILayout.Width(52));
                    GUILayout.Label(Path.GetFileName(replay.ReplayPath), EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label(hasLiveSession ? "实时会话" : "未连接", EditorStyles.miniLabel);
                }

                if (diagnosticResolution.IsReady)
                {
                    DrawDiagnosticRevisions(
                        diagnosticResolution.Session,
                        diagnosticResolution.SkillRuntimeService,
                        diagnosticResolution.StateSampler,
                        diagnosticResolution.EventCollector);
                }
                else
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(diagnosticResolution.StatusMessage, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!_diagnosticSource.IsOffline && diagnosticResolution.IsReady)
            {
                DrawProducerHealthWarning(in diagnosticResolution);
            }

            var panelLoadErrors = BattleDebugPanelRegistry.LoadErrors;
            if (panelLoadErrors != null && panelLoadErrors.Count > 0)
            {
                var suffix = panelLoadErrors.Count > 1
                    ? $"\n另有 {panelLoadErrors.Count - 1} 个面板加载失败。"
                    : string.Empty;
                EditorGUILayout.HelpBox(
                    "Battle Debug 面板加载失败: " + panelLoadErrors[0] + suffix,
                    MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_fileStatus))
            {
                EditorGUILayout.HelpBox(_fileStatus, _fileStatusType);
            }
        }

        private void DrawCriticalStatusMessages(
            in BattleDebugDiagnosticSessionResolution diagnosticResolution)
        {
            if (!_diagnosticSource.IsOffline && diagnosticResolution.IsReady)
            {
                DrawProducerHealthWarning(in diagnosticResolution);
            }

            var panelLoadErrors = BattleDebugPanelRegistry.LoadErrors;
            if (panelLoadErrors != null && panelLoadErrors.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Battle Debug 面板加载失败: " + panelLoadErrors[0],
                    MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_fileStatus))
            {
                EditorGUILayout.HelpBox(_fileStatus, _fileStatusType);
            }
        }

        private static void DrawDiagnosticRevisions(
            IBattleDiagnosticReadOnlySession session,
            MobaSkillCastRuntimeService skillRuntimeService,
            MobaBattleDiagnosticStateSampler stateSampler,
            MobaBattleDiagnosticEventCollector eventCollector)
        {
            if (session == null) return;

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"Cap={session.SessionInfo.Capabilities}  E{session.EventStoreRevision} S{session.StateStoreRevision} T{session.TraceStoreRevision}",
                EditorStyles.miniLabel);
            if (stateSampler != null || eventCollector != null)
            {
                GUILayout.Label(
                    $"Frame={stateSampler?.LastSuccessfulSampleFrame ?? BattleDiagnosticFrames.Invalid} Seq={eventCollector?.LastSequence ?? 0L} Fail={stateSampler?.SampleFailureCount ?? 0L}/{eventCollector?.CollectFailureCount ?? 0L}",
                    EditorStyles.miniLabel);
            }
            if (skillRuntimeService != null)
            {
                var scan = skillRuntimeService.ScanDiagnostics();
                GUILayout.Label(
                    $"Skill={scan.ActiveRuntimes} Waiting={scan.WaitingChildrenRuntimes} Child={scan.PendingChildren}",
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawProducerHealthWarning(
            in BattleDebugDiagnosticSessionResolution resolution)
        {
            var stateError = resolution.StateSampler?.LastSampleError;
            var eventError = resolution.EventCollector?.LastCollectError;
            if (string.IsNullOrEmpty(stateError) && string.IsNullOrEmpty(eventError)) return;

            var message = string.Empty;
            if (!string.IsNullOrEmpty(stateError))
            {
                message = "状态采样失败: " + stateError;
            }
            if (!string.IsNullOrEmpty(eventError))
            {
                if (message.Length > 0) message += "\n";
                message += "事件采集失败: " + eventError;
            }
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private void DrawLiveControls(bool hasLiveSession)
        {
            if (_diagnosticSource.IsOffline || !hasLiveSession) return;

            var replay = BattleReplayControlProvider.Current;
            if (replay != null && replay.IsReplaySession) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("现场", EditorStyles.miniBoldLabel, GUILayout.Width(32));
            var isPaused = EditorApplication.isPaused;
            if (GUILayout.Button(
                    new GUIContent(isPaused ? "恢复现场" : "冻结现场", "暂停或恢复 Unity 播放循环；冻结后可稳定检查实时对象与溯源信息。"),
                    GUILayout.Width(72)))
            {
                EditorApplication.isPaused = !isPaused;
                Repaint();
            }

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPaused);
            if (GUILayout.Button(new GUIContent("单帧", "在保持暂停的前提下推进 Unity 播放循环一帧。"), GUILayout.Width(46)))
            {
                EditorApplication.Step();
                RefreshEntities();
                Repaint();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Label(
                EditorApplication.isPaused ? "已冻结：可检查当前对象" : "运行中",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReplayControls()
        {
            if (_diagnosticSource.IsOffline) return;

            var replay = BattleReplayControlProvider.Current;
            if (replay == null || !replay.IsReplaySession) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("回放", EditorStyles.miniBoldLabel, GUILayout.Width(32));

            if (GUILayout.Button(replay.IsPlaying ? "暂停" : "播放", GUILayout.Width(46)))
            {
                if (replay.IsPlaying) replay.Pause();
                else replay.Play();
            }

            EditorGUI.BeginDisabledGroup(replay.CurrentFrame <= 0);
            if (GUILayout.Button(new GUIContent("<", "后退一帧；必要时回滚或重建逻辑世界"), GUILayout.Width(26)))
            {
                ReplayAction(replay.StepBackward, "后退");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(replay.CurrentFrame >= replay.LastFrame);
            if (GUILayout.Button(new GUIContent(">", "前进一帧"), GUILayout.Width(26)))
            {
                ReplayAction(replay.StepForward, "前进");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            var targetFrame = EditorGUILayout.IntSlider(
                replay.CurrentFrame,
                0,
                Mathf.Max(0, replay.LastFrame),
                GUILayout.MinWidth(180));
            if (EditorGUI.EndChangeCheck())
            {
                if (!replay.SeekToFrame(targetFrame))
                {
                    _fileStatus = $"跳转失败：无法将 Replay 世界定位到第 {targetFrame} 帧。";
                    _fileStatusType = MessageType.Error;
                }
                RefreshEntities();
            }

            GUILayout.Label($"{replay.CurrentFrame}/{replay.LastFrame}", EditorStyles.miniLabel, GUILayout.Width(78));
            GUILayout.Label("速度", EditorStyles.miniLabel, GUILayout.Width(28));
            replay.PlaybackSpeed = EditorGUILayout.FloatField(replay.PlaybackSpeed, GUILayout.Width(42));
            GUILayout.Label("x", EditorStyles.miniLabel, GUILayout.Width(10));
            EditorGUILayout.EndHorizontal();
        }

        private bool CanSeekReplayFrame()
        {
            var replay = BattleReplayControlProvider.Current;
            return !_diagnosticSource.IsOffline && replay != null && replay.IsReplaySession;
        }

        private bool SeekReplayFrame(int frame)
        {
            var replay = BattleReplayControlProvider.Current;
            if (_diagnosticSource.IsOffline || replay == null || !replay.IsReplaySession ||
                frame < 0 || frame > replay.LastFrame)
            {
                return false;
            }

            replay.Pause();
            if (!replay.SeekToFrame(frame)) return false;

            RefreshEntities();
            Repaint();
            return true;
        }

        private void ReplayAction(Func<bool> action, string label)
        {
            if (action == null || !action())
            {
                _fileStatus = $"{label}失败：Replay Session 未能推进到目标帧。";
                _fileStatusType = MessageType.Error;
            }
            RefreshEntities();
            Repaint();
        }

        private void DrawEntityList(IBattleDebugFacade facade)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_entityPaneWidth));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ID", GUILayout.Width(22));
            _jumpId = GUILayout.TextField(_jumpId ?? string.Empty, GUILayout.MinWidth(45));
            if (GUILayout.Button("跳转", GUILayout.Width(40)))
            {
                if (long.TryParse(_jumpId, out var actorId))
                {
                    SelectActor(actorId);
                    GUI.FocusControl(null);
                }
                else
                {
                    _selectionStatus = "请输入有效的 Actor ID。";
                }
            }
            EditorGUI.BeginDisabledGroup(_visibleEntities.Count == 0);
            if (GUILayout.Button(new GUIContent("<", "选择上一个可见 Actor"), GUILayout.Width(24)))
            {
                SelectRelativeEntity(-1);
            }
            if (GUILayout.Button(new GUIContent(">", "选择下一个可见 Actor"), GUILayout.Width(24)))
            {
                SelectRelativeEntity(1);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(_selectedActorId == 0);
            if (GUILayout.Button(new GUIContent("×", "清除 Actor 选择"), GUILayout.Width(24)))
            {
                ClearActorSelection();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_selectionStatus))
            {
                EditorGUILayout.HelpBox(_selectionStatus, MessageType.Info);
            }

            _entityScroll = EditorGUILayout.BeginScrollView(_entityScroll);

            if (_visibleEntities.Count == 0)
            {
                EditorGUILayout.LabelField("暂无实体", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < _visibleEntities.Count; i++)
                {
                    var id = _visibleEntities[i];
                    var selected = id.ActorId == _selectedActorId;
                    var label = id.ToString();

                    if (facade != null && facade.TryResolveUnit(id, out var unit) && unit != null)
                    {
                        var tags = unit.Tags?.Count ?? 0;
                        var effects = unit.Effects?.Active?.Count ?? 0;
                        label = $"{label}  T{tags} E{effects}";
                    }

                    var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(label, style))
                    {
                        SelectActor(id.ActorId);
                        GUI.FocusControl(null);
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawEntityPaneSplitter()
        {
            var splitterRect = GUILayoutUtility.GetRect(
                SplitterWidth,
                SplitterWidth,
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.18f));

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                splitterRect.Contains(currentEvent.mousePosition))
            {
                _resizingEntityPane = true;
                currentEvent.Use();
            }
            else if (_resizingEntityPane && currentEvent.type == EventType.MouseDrag)
            {
                _entityPaneWidth = Mathf.Clamp(
                    _entityPaneWidth + currentEvent.delta.x,
                    MinEntityPaneWidth,
                    Mathf.Min(MaxEntityPaneWidth, Mathf.Max(MinEntityPaneWidth, position.width - 260f)));
                Repaint();
                currentEvent.Use();
            }
            else if (_resizingEntityPane &&
                     (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp))
            {
                _resizingEntityPane = false;
                EditorPrefs.SetFloat(PreferencesPrefix + "EntityPaneWidth", _entityPaneWidth);
                currentEvent.Use();
            }
        }

        private void DrawWorkspace(in BattleDebugContext ctx, IBattleDebugFacade facade)
        {
            var useInspectorColumn = _showSelectionInspector && position.width >= InspectorColumnThreshold;
            if (useInspectorColumn)
            {
                EditorGUILayout.BeginHorizontal();
                if (_showEntityPane)
                {
                    DrawEntityList(facade);
                    DrawEntityPaneSplitter();
                }
                DrawEntityDetails(in ctx);
                DrawInspectorPaneSplitter();
                EditorGUILayout.BeginVertical(GUILayout.Width(_inspectorPaneWidth));
                _selectionInspector.Draw(in ctx);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            if (_showEntityPane)
            {
                DrawEntityList(facade);
                DrawEntityPaneSplitter();
            }
            DrawEntityDetails(in ctx);
            EditorGUILayout.EndHorizontal();
            if (_showSelectionInspector)
            {
                EditorGUILayout.Space(3f);
                _selectionInspector.Draw(in ctx);
            }
            EditorGUILayout.EndVertical();
        }

        private float ResolveAvailableContentWidth()
        {
            var width = position.width;
            if (_showEntityPane)
                width -= _entityPaneWidth + SplitterWidth;
            if (_showSelectionInspector && position.width >= InspectorColumnThreshold)
                width -= _inspectorPaneWidth + SplitterWidth;
            return Mathf.Max(260f, width - 8f);
        }

        private void DrawInspectorPaneSplitter()
        {
            var splitterRect = GUILayoutUtility.GetRect(
                SplitterWidth,
                SplitterWidth,
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.18f));

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                splitterRect.Contains(currentEvent.mousePosition))
            {
                _resizingInspectorPane = true;
                currentEvent.Use();
            }
            else if (_resizingInspectorPane && currentEvent.type == EventType.MouseDrag)
            {
                _inspectorPaneWidth = Mathf.Clamp(
                    _inspectorPaneWidth - currentEvent.delta.x,
                    MinInspectorPaneWidth,
                    Mathf.Min(MaxInspectorPaneWidth, Mathf.Max(MinInspectorPaneWidth, position.width - 420f)));
                Repaint();
                currentEvent.Use();
            }
            else if (_resizingInspectorPane &&
                     (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp))
            {
                _resizingInspectorPane = false;
                EditorPrefs.SetFloat(PreferencesPrefix + "InspectorPaneWidth", _inspectorPaneWidth);
                currentEvent.Use();
            }
        }

        private void DrawEntityDetails(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginVertical();

            var workspaceNames = new[] { "Actor", "Diagnostics" };
            var nextWorkspace = (BattleDebugWorkspace)GUILayout.Toolbar(
                (int)_workspace,
                workspaceNames,
                GUILayout.Height(22));
            if (nextWorkspace != _workspace)
            {
                _workspace = nextWorkspace;
                _detailScroll = Vector2.zero;
            }

            CollectVisiblePanels(in ctx);
            if (_visiblePanels.Count == 0)
            {
                EditorGUILayout.HelpBox("当前工作区没有可显示的面板。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var selectedIndex = GetSelectedPanelIndex();
            selectedIndex = Mathf.Clamp(selectedIndex, 0, _visiblePanels.Count - 1);
            var names = new string[_visiblePanels.Count];
            for (var i = 0; i < _visiblePanels.Count; i++)
            {
                names[i] = _visiblePanels[i].Name;
            }

            if (_workspace == BattleDebugWorkspace.Diagnostics)
            {
                DrawDiagnosticsWorkspace(in ctx, names);
                EditorGUILayout.EndVertical();
                return;
            }

            var nextIndex = GUILayout.Toolbar(
                selectedIndex,
                names,
                EditorStyles.toolbarButton,
                GUILayout.Height(22));
            if (nextIndex != selectedIndex)
            {
                _detailScroll = Vector2.zero;
            }
            SetSelectedPanelIndex(nextIndex);

            var selected = _visiblePanels[nextIndex];
            var ownsScroll = selected is IBattleDebugPanelLayout layout && layout.OwnsScrollView;
            if (ownsScroll)
            {
                selected.Draw(in ctx);
            }
            else
            {
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                selected.Draw(in ctx);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDiagnosticsWorkspace(in BattleDebugContext ctx, string[] names)
        {
            RestoreDiagnosticModuleSelections();
            DrawDiagnosticsWorkspaceControls(in ctx, names);

            var primaryIndex = Mathf.Clamp(
                _selectedDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);
            var secondaryIndex = Mathf.Clamp(
                _selectedSecondaryDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);
            var showSecondary = _showSecondaryDiagnostics &&
                                HasDistinctDiagnosticSlots(
                                    _visiblePanels[primaryIndex],
                                    _primaryDiagnosticWidgetId,
                                    _visiblePanels[secondaryIndex],
                                    _secondaryDiagnosticWidgetId);
            var availableWidth = Mathf.Max(260f, ctx.AvailableContentWidth);

            if (!showSecondary)
            {
                DrawDiagnosticSlot(
                    in ctx,
                    _visiblePanels[primaryIndex],
                    BattleDebugWidgetSlot.Primary,
                    availableWidth,
                    ref _detailScroll,
                    ref _primaryDiagnosticWidgetId);
                return;
            }

            if (availableWidth >= 760f)
            {
                var contentWidth = Mathf.Max(520f, availableWidth - SplitterWidth);
                var primaryWidth = Mathf.Clamp(
                    contentWidth * _diagnosticSplitRatio,
                    260f,
                    contentWidth - 260f);
                var secondaryWidth = Mathf.Max(260f, contentWidth - primaryWidth);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(primaryWidth));
                DrawDiagnosticSlot(
                    in ctx,
                    _visiblePanels[primaryIndex],
                    BattleDebugWidgetSlot.Primary,
                    primaryWidth,
                    ref _detailScroll,
                    ref _primaryDiagnosticWidgetId);
                EditorGUILayout.EndVertical();
                DrawDiagnosticWorkspaceSplitter(contentWidth);
                EditorGUILayout.BeginVertical(GUILayout.Width(secondaryWidth));
                DrawDiagnosticSlot(
                    in ctx,
                    _visiblePanels[secondaryIndex],
                    BattleDebugWidgetSlot.Secondary,
                    secondaryWidth,
                    ref _secondaryDetailScroll,
                    ref _secondaryDiagnosticWidgetId);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                return;
            }

            DrawDiagnosticSlot(
                in ctx,
                _visiblePanels[primaryIndex],
                BattleDebugWidgetSlot.Primary,
                availableWidth,
                ref _detailScroll,
                ref _primaryDiagnosticWidgetId);
            EditorGUILayout.Space(4f);
            DrawDiagnosticSlot(
                in ctx,
                _visiblePanels[secondaryIndex],
                BattleDebugWidgetSlot.Secondary,
                availableWidth,
                ref _secondaryDetailScroll,
                ref _secondaryDiagnosticWidgetId);
        }

        private void DrawDiagnosticsWorkspaceControls(
            in BattleDebugContext ctx,
            string[] names)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var presets = BattleDebugWorkspacePresets.All;
            var presetNames = new string[presets.Count + 1];
            presetNames[0] = "自定义";
            var presetIndex = 0;
            for (var i = 0; i < presets.Count; i++)
            {
                presetNames[i + 1] = presets[i].DisplayName;
                if (string.Equals(
                        presets[i].StableId,
                        _activeWorkspacePresetId,
                        StringComparison.Ordinal))
                {
                    presetIndex = i + 1;
                }
            }

            GUILayout.Label("布局", GUILayout.Width(28));
            var nextPresetIndex = EditorGUILayout.Popup(
                presetIndex,
                presetNames,
                EditorStyles.toolbarPopup,
                GUILayout.Width(100));
            if (nextPresetIndex != presetIndex)
            {
                if (nextPresetIndex == 0)
                {
                    _activeWorkspacePresetId = string.Empty;
                }
                else
                {
                    ApplyWorkspacePreset(presets[nextPresetIndex - 1]);
                }
            }

            GUILayout.Label("主", GUILayout.Width(16));
            var primaryIndex = Mathf.Clamp(
                _selectedDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);
            var nextPrimaryIndex = EditorGUILayout.Popup(
                primaryIndex,
                names,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(100),
                GUILayout.MaxWidth(190));
            if (nextPrimaryIndex != primaryIndex)
            {
                SelectPrimaryDiagnosticPanel(nextPrimaryIndex, true);
            }

            var nextShowSecondary = GUILayout.Toggle(
                _showSecondaryDiagnostics,
                new GUIContent("辅", "并排或上下显示第二个分析模块"),
                EditorStyles.toolbarButton,
                GUILayout.Width(34));
            if (nextShowSecondary != _showSecondaryDiagnostics)
            {
                _showSecondaryDiagnostics = nextShowSecondary;
                _activeWorkspacePresetId = string.Empty;
                EnsureDistinctSecondarySelection();
            }

            EditorGUI.BeginDisabledGroup(!_showSecondaryDiagnostics);
            var secondaryIndex = Mathf.Clamp(
                _selectedSecondaryDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);
            var nextSecondaryIndex = EditorGUILayout.Popup(
                secondaryIndex,
                names,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(100),
                GUILayout.MaxWidth(190));
            if (nextSecondaryIndex != secondaryIndex)
            {
                _selectedSecondaryDiagnosticsPanelIndex = nextSecondaryIndex;
                _secondaryDiagnosticModuleId = GetVisibleModuleId(nextSecondaryIndex);
                _secondaryDiagnosticWidgetId = string.Empty;
                _secondaryDetailScroll = Vector2.zero;
                _activeWorkspacePresetId = string.Empty;
                EnsureDistinctSecondarySelection();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var selectedIndex = Mathf.Clamp(
                _selectedDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);

            EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
            if (GUILayout.Button(
                    new GUIContent("<", "上一个诊断面板"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(26)))
            {
                SelectPrimaryDiagnosticPanel(selectedIndex - 1, true);
                selectedIndex = _selectedDiagnosticsPanelIndex;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(selectedIndex >= names.Length - 1);
            if (GUILayout.Button(
                    new GUIContent(">", "下一个诊断面板"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(26)))
            {
                SelectPrimaryDiagnosticPanel(selectedIndex + 1, true);
                selectedIndex = _selectedDiagnosticsPanelIndex;
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Label(
                $"{selectedIndex + 1}/{names.Length}",
                EditorStyles.miniLabel,
                GUILayout.Width(42));

            GUILayout.FlexibleSpace();
            if (EditorGUIUtility.currentViewWidth >= 760f)
            {
                selectedIndex = DrawDiagnosticsShortcut<IBattleDebugEventsTarget>(
                    selectedIndex,
                    "Events",
                    "打开诊断事件");
                selectedIndex = DrawDiagnosticsShortcut<IBattleDebugTraceTarget>(
                    selectedIndex,
                    "Trace",
                    "打开 Trace 调查");
                selectedIndex = DrawDiagnosticsShortcut<BattleDebugRuntimeObjectsPanel>(
                    selectedIndex,
                    "Objects",
                    "打开运行时对象目录");
            }

            EditorGUILayout.EndHorizontal();
            if (selectedIndex != _selectedDiagnosticsPanelIndex)
            {
                SelectPrimaryDiagnosticPanel(selectedIndex, true);
            }

            BattleDebugTimeRangeSelector.Draw(
                ctx.WorkspaceState,
                ctx.AvailableContentWidth,
                ctx.RequestRepaint);
        }

        private void DrawDiagnosticSlot(
            in BattleDebugContext ctx,
            IBattleDebugPanel panel,
            BattleDebugWidgetSlot slot,
            float availableWidth,
            ref Vector2 scroll,
            ref string selectedWidgetId)
        {
            var widget = ResolveWidget(panel, selectedWidgetId, out var widgets, out var widgetIndex);
            selectedWidgetId = widget.StableId;
            var descriptor = widget.Descriptor;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                slot == BattleDebugWidgetSlot.Primary ? "主视图" : "辅视图",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(46));
            GUILayout.Label(descriptor.DisplayName, EditorStyles.boldLabel, GUILayout.MaxWidth(140f));
            if (widgets != null && widgets.Count > 1)
            {
                var names = new string[widgets.Count];
                for (var i = 0; i < widgets.Count; i++)
                {
                    names[i] = widgets[i].DisplayName;
                }
                var nextWidgetIndex = EditorGUILayout.Popup(
                    widgetIndex,
                    names,
                    EditorStyles.toolbarPopup,
                    GUILayout.MinWidth(100f),
                    GUILayout.MaxWidth(180f));
                if (nextWidgetIndex != widgetIndex)
                {
                    widget = widgets[nextWidgetIndex];
                    selectedWidgetId = widget.StableId;
                    scroll = Vector2.zero;
                    _activeWorkspacePresetId = string.Empty;
                }
            }
            else if (!string.Equals(widget.DisplayName, descriptor.DisplayName, StringComparison.Ordinal))
            {
                GUILayout.Label(widget.DisplayName, EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(descriptor.Category, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            var slotContext = ctx.WithAvailableContentWidth(Mathf.Max(260f, availableWidth - 4f));
            if (widget.OwnsScrollView)
            {
                widget.Draw(in slotContext);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            widget.Draw(in slotContext);
            EditorGUILayout.EndScrollView();
        }

        private IBattleDebugWidget ResolveWidget(
            IBattleDebugPanel panel,
            string stableId,
            out IReadOnlyList<IBattleDebugWidget> widgets,
            out int selectedIndex)
        {
            widgets = (panel as IBattleDebugWidgetProvider)?.Widgets;
            if (widgets == null || widgets.Count == 0)
            {
                selectedIndex = 0;
                return GetWidget(panel);
            }

            for (var i = 0; i < widgets.Count; i++)
            {
                if (widgets[i] != null &&
                    string.Equals(widgets[i].StableId, stableId, StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    return widgets[i];
                }
            }

            selectedIndex = 0;
            return widgets[0];
        }

        private bool HasDistinctDiagnosticSlots(
            IBattleDebugPanel primaryPanel,
            string primaryWidgetId,
            IBattleDebugPanel secondaryPanel,
            string secondaryWidgetId)
        {
            var primary = ResolveWidget(primaryPanel, primaryWidgetId, out _, out _);
            var secondary = ResolveWidget(secondaryPanel, secondaryWidgetId, out _, out _);
            return !ReferenceEquals(primaryPanel, secondaryPanel) ||
                   !string.Equals(primary.StableId, secondary.StableId, StringComparison.Ordinal);
        }

        private BattleDebugPanelWidgetAdapter GetWidget(IBattleDebugPanel panel)
        {
            if (!_widgetAdapters.TryGetValue(panel, out var widget))
            {
                widget = new BattleDebugPanelWidgetAdapter(panel);
                _widgetAdapters.Add(panel, widget);
            }

            return widget;
        }

        private void DrawDiagnosticWorkspaceSplitter(float contentWidth)
        {
            var splitterRect = GUILayoutUtility.GetRect(
                SplitterWidth,
                SplitterWidth,
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.18f));

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                splitterRect.Contains(currentEvent.mousePosition))
            {
                _resizingDiagnosticSplit = true;
                currentEvent.Use();
            }
            else if (_resizingDiagnosticSplit && currentEvent.type == EventType.MouseDrag)
            {
                _diagnosticSplitRatio = Mathf.Clamp(
                    _diagnosticSplitRatio + currentEvent.delta.x / Mathf.Max(1f, contentWidth),
                    0.3f,
                    0.7f);
                Repaint();
                currentEvent.Use();
            }
            else if (_resizingDiagnosticSplit &&
                     (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp))
            {
                _resizingDiagnosticSplit = false;
                EditorPrefs.SetFloat(PreferencesPrefix + "DiagnosticSplitRatio", _diagnosticSplitRatio);
                currentEvent.Use();
            }
        }

        private int DrawDiagnosticsShortcut<TPanel>(
            int selectedIndex,
            string label,
            string tooltip)
        {
            var targetIndex = FindVisiblePanelIndex<TPanel>();
            if (targetIndex < 0) return selectedIndex;

            var active = selectedIndex == targetIndex;
            if (GUILayout.Button(
                    new GUIContent(label, tooltip),
                    active ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                    GUILayout.Width(label == "Objects" ? 58f : 48f),
                    GUILayout.Height(18f)))
            {
                return targetIndex;
            }

            return selectedIndex;
        }

        private int FindVisiblePanelIndex<TPanel>()
        {
            for (var i = 0; i < _visiblePanels.Count; i++)
            {
                if (_visiblePanels[i] is TPanel) return i;
            }

            return -1;
        }

        private int FindVisiblePanelIndex(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return -1;

            for (var i = 0; i < _visiblePanels.Count; i++)
            {
                if (string.Equals(
                        BattleDebugModuleCatalog.GetStableId(_visiblePanels[i]),
                        stableId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private string GetVisibleModuleId(int index)
        {
            if (index < 0 || index >= _visiblePanels.Count) return string.Empty;
            return BattleDebugModuleCatalog.GetStableId(_visiblePanels[index]);
        }

        private void RestoreDiagnosticModuleSelections()
        {
            var primaryById = FindVisiblePanelIndex(_primaryDiagnosticModuleId);
            if (primaryById >= 0)
            {
                _selectedDiagnosticsPanelIndex = primaryById;
            }
            else
            {
                if (!string.IsNullOrEmpty(_activeWorkspacePresetId))
                {
                    _activeWorkspacePresetId = string.Empty;
                }
                _selectedDiagnosticsPanelIndex = Mathf.Clamp(
                    _selectedDiagnosticsPanelIndex,
                    0,
                    _visiblePanels.Count - 1);
                _primaryDiagnosticModuleId = GetVisibleModuleId(_selectedDiagnosticsPanelIndex);
            }

            var secondaryById = FindVisiblePanelIndex(_secondaryDiagnosticModuleId);
            if (secondaryById >= 0)
            {
                _selectedSecondaryDiagnosticsPanelIndex = secondaryById;
            }
            else
            {
                _selectedSecondaryDiagnosticsPanelIndex = Mathf.Clamp(
                    _selectedSecondaryDiagnosticsPanelIndex,
                    0,
                    _visiblePanels.Count - 1);
                _secondaryDiagnosticModuleId = GetVisibleModuleId(_selectedSecondaryDiagnosticsPanelIndex);
            }

            EnsureDistinctSecondarySelection();
        }

        private void EnsureDistinctSecondarySelection()
        {
            if (_visiblePanels.Count == 0)
            {
                _showSecondaryDiagnostics = false;
                return;
            }

            _selectedDiagnosticsPanelIndex = Mathf.Clamp(
                _selectedDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);
            _selectedSecondaryDiagnosticsPanelIndex = Mathf.Clamp(
                _selectedSecondaryDiagnosticsPanelIndex,
                0,
                _visiblePanels.Count - 1);

            var primaryPanel = _visiblePanels[_selectedDiagnosticsPanelIndex];
            var secondaryPanel = _visiblePanels[_selectedSecondaryDiagnosticsPanelIndex];
            if (HasDistinctDiagnosticSlots(
                    primaryPanel,
                    _primaryDiagnosticWidgetId,
                    secondaryPanel,
                    _secondaryDiagnosticWidgetId))
            {
                _secondaryDiagnosticModuleId = GetVisibleModuleId(_selectedSecondaryDiagnosticsPanelIndex);
                return;
            }

            var primaryWidget = ResolveWidget(
                primaryPanel,
                _primaryDiagnosticWidgetId,
                out var primaryWidgets,
                out var primaryWidgetIndex);
            _primaryDiagnosticWidgetId = primaryWidget.StableId;
            if (primaryWidgets != null && primaryWidgets.Count > 1)
            {
                var nextWidgetIndex = (primaryWidgetIndex + 1) % primaryWidgets.Count;
                _selectedSecondaryDiagnosticsPanelIndex = _selectedDiagnosticsPanelIndex;
                _secondaryDiagnosticModuleId = _primaryDiagnosticModuleId;
                _secondaryDiagnosticWidgetId = primaryWidgets[nextWidgetIndex].StableId;
                return;
            }

            if (_visiblePanels.Count > 1)
            {
                _selectedSecondaryDiagnosticsPanelIndex =
                    (_selectedDiagnosticsPanelIndex + 1) % _visiblePanels.Count;
                _secondaryDiagnosticModuleId = GetVisibleModuleId(_selectedSecondaryDiagnosticsPanelIndex);
                _secondaryDiagnosticWidgetId = string.Empty;
                return;
            }

            _showSecondaryDiagnostics = false;
        }

        private void ApplyWorkspacePreset(BattleDebugWorkspacePreset preset)
        {
            _activeWorkspacePresetId = preset.StableId;
            _primaryDiagnosticModuleId = preset.PrimaryModuleId;
            _primaryDiagnosticWidgetId = preset.PrimaryWidgetId;
            _secondaryDiagnosticModuleId = preset.SecondaryModuleId;
            _secondaryDiagnosticWidgetId = preset.SecondaryWidgetId;
            _showSecondaryDiagnostics = preset.ShowsSecondary;

            var primary = FindVisiblePanelIndex(_primaryDiagnosticModuleId);
            if (primary >= 0)
            {
                _selectedDiagnosticsPanelIndex = primary;
            }

            var secondary = FindVisiblePanelIndex(_secondaryDiagnosticModuleId);
            if (secondary >= 0)
            {
                _selectedSecondaryDiagnosticsPanelIndex = secondary;
            }

            EnsureDistinctSecondarySelection();
            _detailScroll = Vector2.zero;
            _secondaryDetailScroll = Vector2.zero;
        }

        private void SelectPrimaryDiagnosticPanel(int index, bool markCustom)
        {
            if (_visiblePanels.Count == 0) return;

            index = Mathf.Clamp(index, 0, _visiblePanels.Count - 1);
            _selectedDiagnosticsPanelIndex = index;
            _primaryDiagnosticModuleId = GetVisibleModuleId(index);
            _primaryDiagnosticWidgetId = string.Empty;
            if (markCustom)
            {
                _activeWorkspacePresetId = string.Empty;
            }

            _detailScroll = Vector2.zero;
            EnsureDistinctSecondarySelection();
        }

        private void CollectVisiblePanels(in BattleDebugContext ctx)
        {
            _visiblePanels.Clear();
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel == null) continue;
                var widget = GetWidget(panel);
                if (!widget.IsAvailable(in ctx)) continue;
                var workspace = panel is IBattleDebugPanelLayout layout
                    ? layout.Workspace
                    : BattleDebugWorkspace.Actor;
                if (workspace == _workspace)
                {
                    _visiblePanels.Add(panel);
                }
            }
        }

        private int GetSelectedPanelIndex()
        {
            return _workspace == BattleDebugWorkspace.Actor
                ? _selectedActorPanelIndex
                : _selectedDiagnosticsPanelIndex;
        }

        private void SetSelectedPanelIndex(int index)
        {
            if (_workspace == BattleDebugWorkspace.Actor)
            {
                _selectedActorPanelIndex = index;
            }
            else
            {
                _selectedDiagnosticsPanelIndex = index;
            }
        }

        private void DrawRefreshIntervalControl()
        {
            EditorGUI.BeginDisabledGroup(!_autoRefresh);
            GUILayout.Label(new GUIContent("频率", "仅控制编辑器窗口从逻辑层轮询和重绘的频率，不改变逻辑层诊断采样频率"), EditorStyles.miniLabel, GUILayout.Width(30));
            var selectedIndex = FindRefreshIntervalOption(_refreshIntervalSeconds);
            var nextIndex = EditorGUILayout.Popup(selectedIndex, RefreshIntervalLabels, EditorStyles.toolbarPopup, GUILayout.Width(58));
            EditorGUI.EndDisabledGroup();

            if (nextIndex == selectedIndex) return;
            _refreshIntervalSeconds = RefreshIntervalOptions[Mathf.Clamp(nextIndex, 0, RefreshIntervalOptions.Length - 1)];
            _nextRefreshAt = EditorApplication.timeSinceStartup;
        }

        private static int FindRefreshIntervalOption(float value)
        {
            var closestIndex = 0;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < RefreshIntervalOptions.Length; i++)
            {
                var distance = Mathf.Abs(RefreshIntervalOptions[i] - value);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closestIndex = i;
            }
            return closestIndex;
        }

        private void AutoRefresh()
        {
            if (!_autoRefresh) return;

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshAt) return;

            _nextRefreshAt = now + _refreshIntervalSeconds;
            RefreshEntities();
            Repaint();
        }

        private void RefreshEntities()
        {
            _entityRefreshBuffer.Clear();

            var filter = string.IsNullOrWhiteSpace(_filter) ? string.Empty : _filter.Trim();
            var selectedExists = false;
            var selectedVisibleIndex = -1;

            if (_diagnosticSource.IsOffline)
            {
                var actors = _diagnosticSource.Actors;
                _totalEntityCount = actors.Count;
                for (var i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    if (actor.ActorId == _selectedActorId) selectedExists = true;
                    if (actor.ActorId <= 0 || actor.ActorId > int.MaxValue) continue;
                    if (!MatchesOfflineActor(in actor, filter)) continue;
                    _entityRefreshBuffer.Add(
                        new BattleDebugEntityId((int)actor.ActorId));
                }
            }
            else
            {
                var facade = BattleDebugFacadeProvider.Current;
                if (facade == null || !facade.TryListEntities(out var ids) || ids == null)
                {
                    _totalEntityCount = 0;
                    _visibleEntities.Clear();
                    return;
                }

                _totalEntityCount = ids.Count;
                for (var i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (id.ActorId == _selectedActorId) selectedExists = true;
                    if (!global::AbilityKit.Game.Editor.BattleDebugEntityFilter.Matches(facade, id, filter)) continue;
                    _entityRefreshBuffer.Add(id);
                }
            }

            _entityRefreshBuffer.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
            if (!HasSameEntitySequence(_visibleEntities, _entityRefreshBuffer))
            {
                _visibleEntities.Clear();
                _visibleEntities.AddRange(_entityRefreshBuffer);
            }

            for (var i = 0; i < _visibleEntities.Count; i++)
            {
                if (_visibleEntities[i].ActorId != _selectedActorId) continue;
                selectedVisibleIndex = i;
                break;
            }

            if (_selectedActorId == 0)
            {
                _selectionStatus = null;
            }
            else if (!selectedExists)
            {
                _selectionStatus = _diagnosticSource.IsOffline
                    ? $"Actor #{_selectedActorId} 不在当前离线 Artifact 中。"
                    : $"Actor #{_selectedActorId} 已离开当前世界。";
            }
            else if (selectedVisibleIndex < 0)
            {
                _selectionStatus = $"Actor #{_selectedActorId} 已被当前过滤条件隐藏，详情选择保持不变。";
            }
            else
            {
                _selectionStatus = null;
            }
        }

        private static bool MatchesOfflineActor(in BattleDiagnosticActorSummary actor, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return actor.ActorId.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actor.ConfigId.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actor.Kind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrEmpty(actor.DisplayName) &&
                    actor.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool HasSameEntitySequence(
            IReadOnlyList<BattleDebugEntityId> current,
            IReadOnlyList<BattleDebugEntityId> next)
        {
            if (current.Count != next.Count) return false;
            for (var i = 0; i < current.Count; i++)
            {
                if (current[i].ActorId != next[i].ActorId) return false;
            }

            return true;
        }

        private void SelectRelativeEntity(int direction)
        {
            if (_visibleEntities.Count == 0 || direction == 0) return;

            var currentIndex = -1;
            for (var i = 0; i < _visibleEntities.Count; i++)
            {
                if (_visibleEntities[i].ActorId == _selectedActorId)
                {
                    currentIndex = i;
                    break;
                }
            }

            var nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : _visibleEntities.Count - 1)
                : (currentIndex + (direction > 0 ? 1 : -1) + _visibleEntities.Count) % _visibleEntities.Count;
            SelectActor(_visibleEntities[nextIndex].ActorId);
        }

        private void ClearActorSelection()
        {
            _selectedActorId = 0;
            _jumpId = string.Empty;
            _selectionStatus = null;
            Repaint();
        }

        private void SelectActor(long actorId)
        {
            SelectActor(actorId, recordNavigation: true);
        }

        private void SelectActor(long actorId, bool recordNavigation)
        {
            if (actorId <= 0 || actorId > int.MaxValue)
            {
                _selectionStatus = $"Actor ID {actorId} 超出有效范围。";
                Repaint();
                return;
            }

            if (recordNavigation && _diagnosticWorkspaceState.Scope.IsValid)
            {
                _diagnosticWorkspaceState.Select(new BattleDiagnosticSelection(
                    _diagnosticWorkspaceState.Scope,
                    BattleDiagnosticSelectionKind.Actor,
                    actorId,
                    _diagnosticWorkspaceState.FrameCursor.Frame));
            }

            _selectedActorId = (int)actorId;
            RefreshEntities();
            for (var i = 0; i < _visibleEntities.Count; i++)
            {
                if (_visibleEntities[i].ActorId != _selectedActorId) continue;
                _entityScroll.y = Mathf.Max(0f, i * 18f);
                break;
            }
            Repaint();
        }

        private void OpenReplay()
        {
            var replay = BattleReplayControlProvider.Current;
            if (!EditorApplication.isPlaying || replay == null)
            {
                _fileStatus = "加载录像需要处于播放模式且已有活动 Battle Session，以复用完整世界启动配置。";
                _fileStatusType = MessageType.Warning;
                return;
            }

            var initialDirectory = string.IsNullOrEmpty(replay.ReplayPath)
                ? Application.dataPath
                : Path.GetDirectoryName(replay.ReplayPath);
            var path = EditorUtility.OpenFilePanel("加载 Battle FrameRecord", initialDirectory, string.Empty);
            if (string.IsNullOrEmpty(path)) return;

            if (!replay.TryLoad(path, _renderReplayPresentation, out var error))
            {
                _fileStatus = $"录像加载失败：{error}";
                _fileStatusType = MessageType.Error;
                return;
            }

            _diagnosticSource.ReturnToLive();
            ClearActorSelection();
            var replayMode = replay.RenderPresentation ? "表现渲染" : "纯逻辑";
            _fileStatus = $"已加载录像：{Path.GetFileName(path)}（{replayMode}）。回放已暂停在第 0 帧。";
            _fileStatusType = MessageType.Info;
            RefreshEntities();
            Repaint();
        }

        private void OpenArtifact()
        {
            var initialDirectory = string.IsNullOrEmpty(_diagnosticSource.FilePath)
                ? Application.dataPath
                : Path.GetDirectoryName(_diagnosticSource.FilePath);
            var path = EditorUtility.OpenFilePanel("打开 Battle Diagnostics Artifact", initialDirectory, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                _diagnosticSource.Open(json, path);
                _fileStatus = $"已打开离线 Artifact：{_diagnosticSource.DisplayName}";
                _fileStatusType = MessageType.Info;
                RefreshEntities();
                if (_selectedActorId == 0 && _visibleEntities.Count > 0)
                {
                    _selectedActorId = _visibleEntities[0].ActorId;
                }
                Repaint();
            }
            catch (MobaBattleDiagnosticArtifactException ex)
            {
                _fileStatus = $"打开失败 [{ex.ErrorCode}]：{ex.Message}";
                _fileStatusType = MessageType.Error;
            }
            catch (Exception ex)
            {
                _fileStatus = $"打开失败 [File.Read]：{ex.Message}";
                _fileStatusType = MessageType.Error;
            }
        }

        private bool CanExportLiveSnapshot()
        {
            return !_diagnosticSource.IsOffline && TryResolveSnapshotCapture(out _);
        }

        private void ExportLiveArtifact()
        {
            if (!TryResolveSnapshotCapture(out var capture))
            {
                _fileStatus = "导出失败 [BattleDiagnostics.LiveSession]：当前实时会话没有可用的快照捕获服务。";
                _fileStatusType = MessageType.Warning;
                return;
            }

            var defaultName = $"battle-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var path = EditorUtility.SaveFilePanel("导出 Battle Diagnostics Artifact", string.Empty, defaultName, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var snapshot = capture.CaptureSnapshot();
                var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                _fileStatus = $"已导出实时诊断 Artifact：{Path.GetFileName(path)}";
                _fileStatusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                _fileStatus = $"导出失败 [File.Write]：{ex.Message}";
                _fileStatusType = MessageType.Error;
            }
        }

        private static bool TryResolveSnapshotCapture(out IMobaBattleDiagnosticSnapshotCapture capture)
        {
            capture = null;
            if (!EditorApplication.isPlaying) return false;
            var facade = BattleDebugFacadeProvider.Current;
            if (facade == null || !facade.TryGetSession(out var logicSession)) return false;
            if (!logicSession.TryGetWorld(out var world) || world?.Services == null) return false;
            return world.Services.TryResolve(out capture) && capture != null;
        }

        private void ReturnToLive()
        {
            _diagnosticSource.ReturnToLive();
            _fileStatus = EditorApplication.isPlaying
                ? "已关闭离线 Artifact，返回实时会话。"
                : "已关闭离线 Artifact；进入播放模式后可连接实时会话。";
            _fileStatusType = MessageType.Info;
            RefreshEntities();
            Repaint();
        }

        private void OpenEvents(long actorId)
        {
            OpenEventsTarget(target => target.OpenForActor(actorId));
        }

        private void OpenEvent(BattleDiagnosticEvent diagnosticEvent)
        {
            OpenEventsTarget(target => target.OpenEvent(
                in diagnosticEvent,
                _diagnosticWorkspaceState));
        }

        private void OpenRecentFailures()
        {
            OpenEventsTarget(target => target.OpenRecentFailures());
        }

        private void OpenEventsTarget(Action<IBattleDebugEventsTarget> open)
        {
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            _workspace = BattleDebugWorkspace.Diagnostics;
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (!(panel is IBattleDebugPanelLayout layout) ||
                    layout.Workspace != BattleDebugWorkspace.Diagnostics)
                {
                    continue;
                }

                if (panel is IBattleDebugEventsTarget target)
                {
                    open(target);
                    _selectedDiagnosticsPanelIndex = CountDiagnosticsPanelsBefore(panels, i);
                    _primaryDiagnosticModuleId = BattleDebugModuleCatalog.GetStableId(panel);
                    _primaryDiagnosticWidgetId = BattleDebugWidgetIds.EventsList;
                    _activeWorkspacePresetId = string.Empty;
                    _detailScroll = Vector2.zero;
                    Repaint();
                    return;
                }
            }
        }

        private void OpenConfig(BattleDebugConfigReference reference)
        {
            var sourceSelection = _diagnosticWorkspaceState.Selection;
            _selectionInspector.SelectConfig(in reference, in sourceSelection);
            _showSelectionInspector = true;

            if (!BattleDebugConfigSourceIndex.TryLocate(in reference, out var location, out var error))
            {
                _fileStatus = $"配置定位失败：{error}";
                _fileStatusType = MessageType.Warning;
                ShowNotification(new GUIContent(_fileStatus));
                Repaint();
                return;
            }

            if (reference.Kind == BattleDebugConfigKind.SkillFlow &&
                TryOpenSkillFlowInspector(in reference, out var inspectorStatus))
            {
                _fileStatus = inspectorStatus;
                _fileStatusType = MessageType.Info;
                ShowNotification(new GUIContent(_fileStatus));
                Repaint();
                return;
            }

            Selection.activeObject = location.Asset;
            EditorGUIUtility.PingObject(location.Asset);
            var opened = AssetDatabase.OpenAsset(location.Asset, location.LineNumber);
            _fileStatus = opened
                ? $"已定位 {reference}：{location.AssetPath}:{location.LineNumber}"
                : $"已选中 {reference} 的配置源，但外部编辑器未能打开：{location.AssetPath}:{location.LineNumber}";
            _fileStatusType = opened ? MessageType.Info : MessageType.Warning;
            ShowNotification(new GUIContent(_fileStatus));
            Repaint();
        }

        private static bool TryOpenSkillFlowInspector(
            in BattleDebugConfigReference reference,
            out string status)
        {
            var flowId = reference.Id;
            var guids = AssetDatabase.FindAssets("t:SkillFlowSO");
            var resolutionError = string.Empty;
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<
                    AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.SkillFlowSO>(assetPath);
                if (asset == null) continue;

                if (!AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
                    .SkillFlowInspectorSelectionState.TrySelect(
                        asset,
                        flowId,
                        reference.PhaseId,
                        out _,
                        out var error))
                {
                    if (asset.dataList != null &&
                        Array.Exists(asset.dataList, flow => flow != null && flow.Id == flowId))
                    {
                        resolutionError = error;
                    }
                    continue;
                }

                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                status = string.IsNullOrEmpty(reference.PhaseId)
                    ? $"Selected SkillFlow #{flowId} in {assetPath}."
                    : $"Selected SkillFlow #{flowId} / {reference.PhaseId} in {assetPath}.";
                return true;
            }

            status = string.IsNullOrEmpty(resolutionError)
                ? $"No SkillFlow asset contains flow #{flowId}."
                : resolutionError;
            return false;
        }

        private static int CountDiagnosticsPanelsBefore(
            System.Collections.Generic.IReadOnlyList<IBattleDebugPanel> panels,
            int exclusiveIndex)
        {
            var count = 0;
            for (var i = 0; i < exclusiveIndex; i++)
            {
                if (panels[i] is IBattleDebugPanelLayout layout &&
                    layout.Workspace == BattleDebugWorkspace.Diagnostics)
                {
                    count++;
                }
            }

            return count;
        }

        private void OpenTrace(long rootContextId, long contextId)
        {
            OpenTrace(rootContextId, contextId, recordNavigation: true);
        }

        private void OpenTrace(long rootContextId, long contextId, bool recordNavigation)
        {
            if (rootContextId <= 0) return;

            if (recordNavigation && _diagnosticWorkspaceState.Scope.IsValid)
            {
                var focusedContextId = contextId > 0 ? contextId : rootContextId;
                var kind = focusedContextId == rootContextId
                    ? BattleDiagnosticSelectionKind.TraceRoot
                    : BattleDiagnosticSelectionKind.TraceNode;
                _diagnosticWorkspaceState.Select(new BattleDiagnosticSelection(
                    _diagnosticWorkspaceState.Scope,
                    kind,
                    focusedContextId,
                    _diagnosticWorkspaceState.FrameCursor.Frame,
                    rootContextId));
            }

            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            var selectedId = _selectedActorId != 0
                ? new BattleDebugEntityId(_selectedActorId)
                : default;
            IUnitFacade selectedUnit = null;
            var facade = _diagnosticSource.IsOffline ? null : BattleDebugFacadeProvider.Current;
            if (facade != null && selectedId.IsValid)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var diagnosticResolution = _diagnosticSource.IsOffline
                ? new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.Ready,
                    _diagnosticSource.Session,
                    null,
                    healthSnapshot: _diagnosticSource.HealthSnapshot)
                : BattleDebugDiagnosticSessionResolver.Resolve(facade, EditorApplication.isPlaying);
            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                Repaint,
                selectActor: SelectActor,
                openTrace: OpenTrace,
                openEvents: OpenEvents,
                openEvent: OpenEvent,
                openRecentFailures: OpenRecentFailures,
                openConfig: OpenConfig,
                seekReplayFrame: CanSeekReplayFrame() ? SeekReplayFrame : null,
                diagnosticSession: diagnosticResolution.Session,
                skillRuntimeService: diagnosticResolution.SkillRuntimeService,
                diagnosticResolution: diagnosticResolution,
                isOffline: _diagnosticSource.IsOffline,
                workspaceState: _diagnosticWorkspaceState);
            var diagnosticsIndex = 0;
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (!(panel is IBattleDebugPanelLayout layout) ||
                    layout.Workspace != BattleDebugWorkspace.Diagnostics ||
                    !panel.IsVisible(in ctx))
                {
                    continue;
                }

                if (panel is IBattleDebugTraceTarget target)
                {
                    target.OpenTrace(rootContextId, contextId);
                    _workspace = BattleDebugWorkspace.Diagnostics;
                    _selectedDiagnosticsPanelIndex = diagnosticsIndex;
                    _primaryDiagnosticModuleId = BattleDebugModuleCatalog.GetStableId(panel);
                    _primaryDiagnosticWidgetId = BattleDebugWidgetIds.TraceTree;
                    _activeWorkspacePresetId = string.Empty;
                    _detailScroll = Vector2.zero;
                    Repaint();
                    return;
                }

                diagnosticsIndex++;
            }
        }

        private void SynchronizeDiagnosticWorkspace(
            in BattleDebugDiagnosticSessionResolution resolution,
            bool isOffline)
        {
            var session = resolution.Session;
            if (session == null || !session.SessionInfo.Scope.IsValid)
            {
                if (_diagnosticWorkspaceState.Scope.IsValid)
                {
                    _diagnosticWorkspaceState.DetachSession();
                }
                return;
            }

            var scope = session.SessionInfo.Scope;
            var latestFrame = resolution.HasHealthSnapshot
                ? resolution.HealthSnapshot.Value.LastSuccessfulStateFrame
                : isOffline
                    ? _diagnosticSource.LatestCompleteFrame
                    : BattleDiagnosticFrames.Invalid;
            if (_diagnosticWorkspaceState.Scope != scope)
            {
                _diagnosticWorkspaceState.AttachSession(scope, latestFrame);
                return;
            }

            _diagnosticWorkspaceState.AdvanceLive(latestFrame);
        }

        private void NavigateDiagnosticHistory(bool back)
        {
            var changed = back
                ? _diagnosticWorkspaceState.GoBack()
                : _diagnosticWorkspaceState.GoForward();
            if (!changed)
            {
                return;
            }

            ApplyDiagnosticSelection(_diagnosticWorkspaceState.Selection);
        }

        private void ApplyDiagnosticSelection(in BattleDiagnosticSelection selection)
        {
            switch (selection.Kind)
            {
                case BattleDiagnosticSelectionKind.Actor:
                    SelectActor(selection.Id, recordNavigation: false);
                    break;
                case BattleDiagnosticSelectionKind.Event:
                    SelectDiagnosticsPanel<BattleDebugDiagnosticEventsPanel>();
                    break;
                case BattleDiagnosticSelectionKind.TraceRoot:
                case BattleDiagnosticSelectionKind.TraceNode:
                    var rootContextId = selection.RelatedId > 0
                        ? selection.RelatedId
                        : selection.Id;
                    OpenTrace(rootContextId, selection.Id, recordNavigation: false);
                    break;
            }

            Repaint();
        }

        private void SelectDiagnosticsPanel<TPanel>() where TPanel : class, IBattleDebugPanel
        {
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null)
            {
                return;
            }

            for (var i = 0; i < panels.Count; i++)
            {
                if (!(panels[i] is TPanel))
                {
                    continue;
                }

                _workspace = BattleDebugWorkspace.Diagnostics;
                _selectedDiagnosticsPanelIndex = CountDiagnosticsPanelsBefore(panels, i);
                _primaryDiagnosticModuleId = BattleDebugModuleCatalog.GetStableId(panels[i]);
                _primaryDiagnosticWidgetId = panels[i] is BattleDebugDiagnosticEventsPanel
                    ? BattleDebugWidgetIds.EventsList
                    : panels[i] is BattleDebugDiagnosticTracePanel
                        ? BattleDebugWidgetIds.TraceTree
                        : string.Empty;
                _activeWorkspacePresetId = string.Empty;
                _detailScroll = Vector2.zero;
                return;
            }
        }
    }
}
