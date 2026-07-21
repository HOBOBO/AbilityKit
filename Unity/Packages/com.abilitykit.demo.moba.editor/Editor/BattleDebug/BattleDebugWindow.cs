using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;
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
            _nextRefreshAt = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(PreferencesPrefix + "EntityPaneWidth", _entityPaneWidth);
            EditorPrefs.SetInt(PreferencesPrefix + "Workspace", (int)_workspace);
            EditorPrefs.SetInt(PreferencesPrefix + "ActorPanelIndex", _selectedActorPanelIndex);
            EditorPrefs.SetInt(PreferencesPrefix + "DiagnosticsPanelIndex", _selectedDiagnosticsPanelIndex);
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("进入播放模式后才能使用战斗调试窗口。", MessageType.Info);
                return;
            }

            var facade = BattleDebugFacadeProvider.Current;
            if (facade == null)
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("BattleDebugFacadeProvider.Current 为空。请通过 BattleLogicSessionHost.Start() 启动 BattleLogicSession。", MessageType.Warning);
                return;
            }

            if (!facade.TryGetSession(out _))
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("当前没有活动中的 BattleLogicSession，请先启动会话。", MessageType.Info);
                return;
            }

            var selectedId = _selectedActorId != 0
                ? new EcsEntityId(_selectedActorId)
                : default;
            IUnitFacade selectedUnit = null;
            if (selectedId.IsValid)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                requestRepaint: Repaint,
                selectActor: SelectActor,
                openTrace: OpenTrace);

            DrawToolbar(in ctx);

            EditorGUILayout.BeginHorizontal();
            DrawEntityList(facade);
            DrawEntityPaneSplitter();
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

            GUILayout.FlexibleSpace();

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

            var facade = BattleDebugFacadeProvider.Current;
            if (facade == null)
            {
                _totalEntityCount = 0;
                _visibleEntities.Clear();
                return;
            }
            if (!facade.TryListEntities(out var ids) || ids == null)
            {
                _totalEntityCount = 0;
                _visibleEntities.Clear();
                return;
            }

            _totalEntityCount = ids.Count;
            var filter = string.IsNullOrWhiteSpace(_filter) ? string.Empty : _filter.Trim();
            var selectedExists = false;
            var selectedVisibleIndex = -1;

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (id.ActorId == _selectedActorId) selectedExists = true;
                if (!global::AbilityKit.Game.Editor.BattleDebugEntityFilter.Matches(facade, id, filter)) continue;

                _entityRefreshBuffer.Add(id);
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
                _selectionStatus = $"Actor #{_selectedActorId} 已离开当前世界。";
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

        private void OpenTrace(long rootContextId, long contextId)
        {
            if (rootContextId <= 0) return;

            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null) return;

            var selectedId = _selectedActorId != 0
                ? new EcsEntityId(_selectedActorId)
                : default;
            IUnitFacade selectedUnit = null;
            var facade = BattleDebugFacadeProvider.Current;
            if (facade != null && selectedId.IsValid)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var ctx = new BattleDebugContext(facade, selectedId, selectedUnit, Repaint);
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
