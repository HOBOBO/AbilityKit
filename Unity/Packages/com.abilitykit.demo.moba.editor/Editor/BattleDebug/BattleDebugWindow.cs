using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Share.ECS;
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
        private const float MinEntityPaneWidth = 160f;
        private const float MaxEntityPaneWidth = 420f;
        private const float SplitterWidth = 5f;

        private string _filter;
        private string _jumpId;
        private string _selectionStatus;
        private Vector2 _entityScroll;
        private Vector2 _detailScroll;

        private readonly List<EcsEntityId> _visibleEntities = new List<EcsEntityId>(256);
        private readonly List<EcsEntityId> _entityRefreshBuffer = new List<EcsEntityId>(256);
        private readonly List<IBattleDebugPanel> _visiblePanels = new List<IBattleDebugPanel>(16);
        private int _selectedActorId;
        private int _totalEntityCount;
        private double _nextRefreshAt;
        private float _entityPaneWidth = 220f;
        private bool _resizingEntityPane;
        private bool _autoRefresh = true;
        private bool _renderReplayPresentation = true;
        private bool _showEntityPane = true;
        private bool _showStatusArea = true;
        private readonly BattleDebugDiagnosticSource _diagnosticSource = new BattleDebugDiagnosticSource();
        private string _fileStatus;
        private MessageType _fileStatusType = MessageType.None;

        private BattleDebugWorkspace _workspace;
        private int _selectedActorPanelIndex;
        private int _selectedDiagnosticsPanelIndex;

        [MenuItem("Tools/AbilityKit/Battle/战斗调试")]
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
            _renderReplayPresentation = EditorPrefs.GetBool(PreferencesPrefix + "RenderReplayPresentation", true);
            _showEntityPane = EditorPrefs.GetBool(PreferencesPrefix + "ShowEntityPane", true);
            _showStatusArea = EditorPrefs.GetBool(PreferencesPrefix + "ShowStatusArea", true);
            _nextRefreshAt = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(PreferencesPrefix + "EntityPaneWidth", _entityPaneWidth);
            EditorPrefs.SetInt(PreferencesPrefix + "Workspace", (int)_workspace);
            EditorPrefs.SetInt(PreferencesPrefix + "ActorPanelIndex", _selectedActorPanelIndex);
            EditorPrefs.SetInt(PreferencesPrefix + "DiagnosticsPanelIndex", _selectedDiagnosticsPanelIndex);
            EditorPrefs.SetBool(PreferencesPrefix + "RenderReplayPresentation", _renderReplayPresentation);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowEntityPane", _showEntityPane);
            EditorPrefs.SetBool(PreferencesPrefix + "ShowStatusArea", _showStatusArea);
        }

        private void OnDestroy()
        {
            _diagnosticSource.Dispose();
        }

        private void OnGUI()
        {
            var isOffline = _diagnosticSource.IsOffline;
            var facade = isOffline ? null : BattleDebugFacadeProvider.Current;
            var diagnosticResolution = isOffline
                ? new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.Ready,
                    _diagnosticSource.Session,
                    null)
                : BattleDebugDiagnosticSessionResolver.Resolve(facade, EditorApplication.isPlaying);
            var hasLiveSession = !isOffline &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.NotPlaying &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.FacadeUnavailable &&
                                 diagnosticResolution.Phase != BattleDebugDiagnosticSessionResolutionPhase.LogicSessionUnavailable;

            var selectedId = _selectedActorId != 0
                ? new EcsEntityId(_selectedActorId)
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
                openConfig: OpenConfig,
                seekReplayFrame: CanSeekReplayFrame() ? SeekReplayFrame : null,
                diagnosticSession: diagnosticResolution.Session,
                skillRuntimeService: diagnosticResolution.SkillRuntimeService,
                diagnosticResolution: diagnosticResolution,
                isOffline: isOffline);

            DrawToolbar(in ctx);
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

            EditorGUILayout.BeginHorizontal();
            if (_showEntityPane)
            {
                DrawEntityList(facade);
                DrawEntityPaneSplitter();
            }
            DrawEntityDetails(in ctx);
            EditorGUILayout.EndHorizontal();

            AutoRefresh();
        }

        private void DrawToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("过滤", GUILayout.Width(35));
            var newFilter = GUILayout.TextField(_filter ?? string.Empty, GUI.skin.textField, GUILayout.MinWidth(100));
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

            GUILayout.FlexibleSpace();

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

            _autoRefresh = GUILayout.Toggle(
                _autoRefresh,
                new GUIContent("自动刷新", "仅控制此窗口的周期轮询，不影响底层诊断采集"),
                EditorStyles.toolbarButton,
                GUILayout.Width(70));
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                RefreshEntities();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
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

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("面板", GUILayout.Width(30));
            var nextIndex = EditorGUILayout.Popup(
                selectedIndex,
                names,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(140));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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

        private void CollectVisiblePanels(in BattleDebugContext ctx)
        {
            _visiblePanels.Clear();
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel == null || !panel.IsVisible(in ctx)) continue;
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

        private void AutoRefresh()
        {
            if (!_autoRefresh) return;

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshAt) return;

            _nextRefreshAt = now + 0.25;
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
                    _entityRefreshBuffer.Add(new EcsEntityId((int)actor.ActorId));
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
            IReadOnlyList<EcsEntityId> current,
            IReadOnlyList<EcsEntityId> next)
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
            if (actorId <= 0 || actorId > int.MaxValue)
            {
                _selectionStatus = $"Actor ID {actorId} 超出有效范围。";
                Repaint();
                return;
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
            if (actorId <= 0) return;

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
                    target.OpenForActor(actorId);
                    _selectedDiagnosticsPanelIndex = CountDiagnosticsPanelsBefore(panels, i);
                    _detailScroll = Vector2.zero;
                    Repaint();
                    return;
                }
            }
        }

        private void OpenConfig(BattleDebugConfigReference reference)
        {
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
            if (rootContextId <= 0) return;

            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            var selectedId = _selectedActorId != 0
                ? new EcsEntityId(_selectedActorId)
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
                    null)
                : BattleDebugDiagnosticSessionResolver.Resolve(facade, EditorApplication.isPlaying);
            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                Repaint,
                selectActor: SelectActor,
                openTrace: OpenTrace,
                openEvents: OpenEvents,
                openConfig: OpenConfig,
                seekReplayFrame: CanSeekReplayFrame() ? SeekReplayFrame : null,
                diagnosticSession: diagnosticResolution.Session,
                skillRuntimeService: diagnosticResolution.SkillRuntimeService,
                diagnosticResolution: diagnosticResolution,
                isOffline: _diagnosticSource.IsOffline);
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
                    _detailScroll = Vector2.zero;
                    Repaint();
                    return;
                }

                diagnosticsIndex++;
            }
        }
    }
}
