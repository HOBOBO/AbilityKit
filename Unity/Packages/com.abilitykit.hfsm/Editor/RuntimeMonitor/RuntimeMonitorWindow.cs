// ============================================================================
// RuntimeMonitorWindow - 运行时状态机监控窗口
// 提供树形视图、图形视图和状态追踪功能
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using AbilityKit.Editor.Platform.State;
using AbilityKit.Editor.Platform.UI;
using AbilityKit.HFSM.Visualization;
using LiveRegistry = AbilityKit.HFSM.Visualization.LiveRegistry;

namespace AbilityKit.HFSM.Editor.RuntimeMonitor
{
    /// <summary>
    /// 运行时状态机监控窗口
    /// </summary>
    public class RuntimeMonitorWindow : EditorWindow
    {
        private const string ShowParametersStateKey = "show-parameters";
        private const string ShowHistoryStateKey = "show-history";
        private const string ShowTreeStateKey = "show-tree";
        private const string ShowGraphStateKey = "show-graph";
        private const string SelectedFsmStateKey = "selected-fsm";

        [MenuItem("Window/AbilityKit/HFSM 运行时监视器")]
        public static void OpenWindow()
        {
            var window = GetWindow<RuntimeMonitorWindow>();
            window.titleContent = new GUIContent(
                "HFSM 运行时监视器",
                EditorGUIUtility.IconContent("AnimatorController Icon").image
            );
            window.minSize = new Vector2(800, 500);
        }

        public static void OpenWindow(string preferredFsmName)
        {
            OpenWindow();
            var window = GetWindow<RuntimeMonitorWindow>();
            window._preferredFsmName = preferredFsmName ?? string.Empty;
            window.RefreshFsmList();
            window.Show();
            window.Focus();
        }

        // 数据
        private int _selectedFsmIndex = -1;
        private readonly IEditorUserStateStore _userState =
            new EditorPrefsUserStateStore("hfsm", "runtime-monitor");
        private readonly List<object> _entryTargets = new List<object>();
        private WeakReference _selectedFsm;
        private string _preferredFsmName = string.Empty;
        private FsmSnapshot _currentSnapshot;
        private Vector2 _scrollPosition;
        private Vector2 _parameterScrollPosition;
        private Vector2 _historyScrollPosition;
        private bool _showParameters = true;
        private bool _showHistory = true;
        private bool _showTreeView = true;
        private bool _showGraphView = true;

        // 布局引擎
        private AutoLayoutEngine _layoutEngine;

        // 视图元素
        private VisualElement _root;
        private EditorSplitter _mainSplitter;
        private PopupField<string> _fsmDropdown;
        private Label _runtimeInfoLabel;
        private VisualElement _playIndicator;
        private VisualElement _treeViewContainer;
        private VisualElement _graphViewContainer;
        private IMGUIContainer _treeCanvas;
        private IMGUIContainer _graphCanvas;
        private VisualElement _parameterPanel;
        private IMGUIContainer _parameterCanvas;
        private VisualElement _historyPanel;
        private IMGUIContainer _historyCanvas;
        private double _nextSnapshotRefreshTime;

        // 样式
        private const float kNodeWidth = 140f;
        private const float kNodeHeight = 50f;
        private const float kNodeSpacingX = 40f;
        private const float kNodeSpacingY = 30f;
        private const float kNodeMarginLeft = 50f;
        private const float kNodeMarginTop = 30f;

        private void OnEnable()
        {
            _showParameters = _userState.GetBool(ShowParametersStateKey, true);
            _showHistory = _userState.GetBool(ShowHistoryStateKey, true);
            _showTreeView = _userState.GetBool(ShowTreeStateKey, true);
            _showGraphView = _userState.GetBool(ShowGraphStateKey, true);
            _preferredFsmName = _userState.GetString(SelectedFsmStateKey, string.Empty);

            // 订阅事件
            LiveRegistry.Changed += OnRegistryChanged;
            LiveRegistry.SnapshotUpdated += OnSnapshotUpdated;
            EditorApplication.update += OnEditorUpdate;

            _layoutEngine = new AutoLayoutEngine(
                nodeWidth: kNodeWidth,
                nodeHeight: kNodeHeight,
                spacingX: kNodeSpacingX,
                spacingY: kNodeSpacingY,
                marginLeft: kNodeMarginLeft,
                marginTop: kNodeMarginTop
            );

            CreateUI();
        }

        private void OnDisable()
        {
            LiveRegistry.Changed -= OnRegistryChanged;
            LiveRegistry.SnapshotUpdated -= OnSnapshotUpdated;
            EditorApplication.update -= OnEditorUpdate;
            _mainSplitter?.Dispose();
            _mainSplitter = null;
        }

        private void OnEditorUpdate()
        {
            UpdatePlayIndicator();
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            // 在播放模式下定期更新快照
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextSnapshotRefreshTime) return;
            _nextSnapshotRefreshTime = now + 0.25d;
            LiveRegistry.UpdateAllSnapshots();
        }

        private void OnRegistryChanged()
        {
            RefreshFsmList();
        }

        private void OnSnapshotUpdated(object fsm)
        {
            if (_selectedFsm == null || !ReferenceEquals(_selectedFsm.Target, fsm)) return;
            var entry = FindEntry(fsm);
            if (entry == null) return;

            _currentSnapshot = entry.Snapshot;
            UpdateRuntimeInfoLabel();
            UpdateLayout();
            RepaintViews();
        }

        private void CreateUI()
        {
            _root = rootVisualElement;
            _root.Clear();
            _root.style.flexDirection = FlexDirection.Column;

            // 工具栏
            CreateToolbar();

            // 主内容区
            CreateMainContent();

            // 底部面板
            CreateBottomPanel();

            // 初始刷新
            RefreshFsmList();
        }

        private void UpdateRuntimeInfoLabel()
        {
            if (_runtimeInfoLabel == null) return;
            var snapshot = _currentSnapshot;
            _runtimeInfoLabel.text = snapshot == null || (snapshot.frame == 0 && snapshot.definitionHash == 0L)
                ? string.Empty
                : $"frame {snapshot.frame}  ·  定义 {snapshot.definitionHash:X8}";
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // FSM 选择器
            var fsmLabel = new Label("FSM：");
            fsmLabel.style.marginLeft = 5;
            fsmLabel.style.marginRight = 5;
            fsmLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            fsmLabel.style.width = 40;
            toolbar.Add(fsmLabel);

            _fsmDropdown = new PopupField<string>(
                new List<string> { "无运行时实例" },
                0);
            _fsmDropdown.style.flexGrow = 1;
            _fsmDropdown.RegisterValueChangedCallback(evt => SelectFsm(_fsmDropdown.index));
            toolbar.Add(_fsmDropdown);

            // 刷新按钮
            var refreshButton = new ToolbarButton(() => RefreshFsmList()) { text = "刷新" };
            toolbar.Add(refreshButton);

            // 视图切换
            var viewToggle = new ToolbarToggle { text = "显示图形" };
            viewToggle.value = _showGraphView;
            viewToggle.RegisterValueChangedCallback(evt =>
            {
                _showGraphView = evt.newValue;
                _userState.SetBool(ShowGraphStateKey, _showGraphView);
                UpdateViewVisibility();
            });
            toolbar.Add(viewToggle);

            var treeToggle = new ToolbarToggle { text = "显示状态树" };
            treeToggle.value = _showTreeView;
            treeToggle.RegisterValueChangedCallback(evt =>
            {
                _showTreeView = evt.newValue;
                _userState.SetBool(ShowTreeStateKey, _showTreeView);
                UpdateViewVisibility();
            });
            toolbar.Add(treeToggle);

            var parametersToggle = new ToolbarToggle { text = "参数", value = _showParameters };
            parametersToggle.RegisterValueChangedCallback(evt =>
            {
                _showParameters = evt.newValue;
                _userState.SetBool(ShowParametersStateKey, _showParameters);
                UpdateViewVisibility();
            });
            toolbar.Add(parametersToggle);

            var historyToggle = new ToolbarToggle { text = "转换历史", value = _showHistory };
            historyToggle.RegisterValueChangedCallback(evt =>
            {
                _showHistory = evt.newValue;
                _userState.SetBool(ShowHistoryStateKey, _showHistory);
                UpdateViewVisibility();
            });
            toolbar.Add(historyToggle);

            // 运行版本信息（帧号 / 定义哈希）
            _runtimeInfoLabel = new Label
            {
                style =
                {
                    marginLeft = 8,
                    marginRight = 6,
                    unityTextAlign = TextAnchor.MiddleLeft,
                },
            };
            toolbar.Add(_runtimeInfoLabel);
            UpdateRuntimeInfoLabel();

            // 播放状态指示
            _playIndicator = new VisualElement();
            _playIndicator.style.width = 12;
            _playIndicator.style.height = 12;
            _playIndicator.style.borderTopLeftRadius = 6;
            _playIndicator.style.borderTopRightRadius = 6;
            _playIndicator.style.borderBottomLeftRadius = 6;
            _playIndicator.style.borderBottomRightRadius = 6;
            _playIndicator.style.marginLeft = 10;
            _playIndicator.style.marginRight = 6;
            toolbar.Add(_playIndicator);
            UpdatePlayIndicator();

            _root.Add(toolbar);
        }

        private void CreateMainContent()
        {
            _mainSplitter?.Dispose();
            _mainSplitter = new EditorSplitter(
                new EditorSplitterState(
                    220f,
                    minimumPosition: 160f,
                    maximumPosition: 420f,
                    store: _userState,
                    stateKey: "tree-splitter"));

            // 左侧树形视图
            _treeViewContainer = new VisualElement();
            _treeViewContainer.style.flexGrow = 1f;
            _treeViewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            _treeViewContainer.style.borderRightWidth = 1;
            _treeViewContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            _treeCanvas = new IMGUIContainer(DrawTreeView);
            _treeCanvas.style.flexGrow = 1f;
            _treeViewContainer.Add(_treeCanvas);
            _mainSplitter.FirstPane.Add(_treeViewContainer);

            // 右侧图形视图
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            rightPanel.style.flexDirection = FlexDirection.Column;

            _graphViewContainer = new VisualElement();
            _graphViewContainer.style.flexGrow = 1;
            _graphCanvas = new IMGUIContainer(DrawGraphView);
            _graphCanvas.style.flexGrow = 1f;
            _graphViewContainer.Add(_graphCanvas);
            rightPanel.Add(_graphViewContainer);

            // 参数面板（可折叠）
            _parameterPanel = CreateParameterPanel();
            _parameterPanel.style.height = 120;
            rightPanel.Add(_parameterPanel);

            _mainSplitter.SecondPane.Add(rightPanel);
            _root.Add(_mainSplitter);
        }

        private VisualElement CreateParameterPanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.paddingTop = 5;
            header.style.paddingBottom = 5;
            header.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);

            var title = new Label("参数");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;

            var toggle = new Label(_showParameters ? "▼" : "▶");
            toggle.style.width = 20;
            toggle.style.cursor = StyleKeyword.Auto;
            toggle.RegisterCallback<ClickEvent>(evt =>
            {
                _showParameters = !_showParameters;
                toggle.text = _showParameters ? "▼" : "▶";
                _userState.SetBool(ShowParametersStateKey, _showParameters);
                UpdateViewVisibility();
            });

            header.Add(title);
            header.Add(toggle);
            panel.Add(header);
            _parameterCanvas = new IMGUIContainer(DrawParameterPanel);
            _parameterCanvas.style.flexGrow = 1f;
            panel.Add(_parameterCanvas);

            return panel;
        }

        private VisualElement CreateHistoryPanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.paddingTop = 5;
            header.style.paddingBottom = 5;
            header.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);

            var title = new Label("转换历史");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;

            header.Add(title);
            var toggle = new Label(_showHistory ? "▼" : "▶");
            toggle.style.width = 20;
            toggle.RegisterCallback<ClickEvent>(_ =>
            {
                _showHistory = !_showHistory;
                toggle.text = _showHistory ? "▼" : "▶";
                _userState.SetBool(ShowHistoryStateKey, _showHistory);
                UpdateViewVisibility();
            });
            header.Add(toggle);
            panel.Add(header);
            _historyCanvas = new IMGUIContainer(DrawHistoryPanel);
            _historyCanvas.style.flexGrow = 1f;
            panel.Add(_historyCanvas);

            return panel;
        }

        private void CreateBottomPanel()
        {
            // 历史记录面板（可折叠）
            _historyPanel = CreateHistoryPanel();
            _root.Add(_historyPanel);
            UpdateViewVisibility();
        }

        private void UpdateViewVisibility()
        {
            if (_treeViewContainer != null)
                _treeViewContainer.style.display = _showTreeView ? DisplayStyle.Flex : DisplayStyle.None;

            if (_graphViewContainer != null)
                _graphViewContainer.style.display = _showGraphView ? DisplayStyle.Flex : DisplayStyle.None;

            if (_parameterPanel != null)
            {
                _parameterPanel.style.display = DisplayStyle.Flex;
                _parameterPanel.style.height = _showParameters ? 120 : 25;
            }
            if (_parameterCanvas != null)
                _parameterCanvas.style.display = _showParameters ? DisplayStyle.Flex : DisplayStyle.None;

            if (_historyPanel != null)
            {
                _historyPanel.style.display = DisplayStyle.Flex;
                _historyPanel.style.height = _showHistory ? 100 : 25;
            }
            if (_historyCanvas != null)
                _historyCanvas.style.display = _showHistory ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshFsmList()
        {
            var entries = LiveRegistry.GetEntries();
            var previousTarget = _selectedFsm?.Target;
            var labels = new List<string>();
            _entryTargets.Clear();

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var target = entry.Target;
                if (target == null) continue;
                labels.Add($"{entry.Name} ({entry.FsmType?.Name ?? "未知类型"})");
                _entryTargets.Add(target);
            }

            if (_entryTargets.Count == 0)
            {
                _selectedFsmIndex = -1;
                _selectedFsm = null;
                _currentSnapshot = null;
                _fsmDropdown.choices = new List<string> { "无运行时实例" };
                _fsmDropdown.SetValueWithoutNotify(_fsmDropdown.choices[0]);
                _fsmDropdown.SetEnabled(false);
                RepaintViews();
                return;
            }

            _fsmDropdown.choices = labels;
            _fsmDropdown.SetEnabled(true);
            _selectedFsmIndex = FindSelectionIndex(entries, previousTarget, _preferredFsmName);
            _fsmDropdown.index = _selectedFsmIndex;
            SelectFsm(_selectedFsmIndex);
        }

        private int FindSelectionIndex(
            IReadOnlyList<LiveRegistry.Entry> entries,
            object previousTarget,
            string preferredName)
        {
            for (var index = 0; index < _entryTargets.Count; index++)
                if (ReferenceEquals(_entryTargets[index], previousTarget)) return index;

            if (!string.IsNullOrEmpty(preferredName))
            {
                for (var index = 0; index < entries.Count; index++)
                    if (string.Equals(entries[index].Name, preferredName, StringComparison.OrdinalIgnoreCase))
                        return index;
            }

            return 0;
        }

        private void SelectFsm(int index)
        {
            if (index < 0 || index >= _entryTargets.Count) return;
            _selectedFsmIndex = index;
            var target = _entryTargets[index];
            _selectedFsm = new WeakReference(target);
            var entry = FindEntry(target);
            _currentSnapshot = entry?.Snapshot;
            _preferredFsmName = entry?.Name ?? string.Empty;
            UpdateRuntimeInfoLabel();
            _userState.SetString(SelectedFsmStateKey, _preferredFsmName);
            if (EditorApplication.isPlaying)
                LiveRegistry.UpdateSnapshot(target);
            else
                UpdateLayout();
            RepaintViews();
        }

        private static LiveRegistry.Entry FindEntry(object target)
        {
            var entries = LiveRegistry.GetEntries();
            for (var index = 0; index < entries.Count; index++)
                if (ReferenceEquals(entries[index].Target, target)) return entries[index];
            return null;
        }

        private void RepaintViews()
        {
            _treeCanvas?.MarkDirtyRepaint();
            _graphCanvas?.MarkDirtyRepaint();
            _parameterCanvas?.MarkDirtyRepaint();
            _historyCanvas?.MarkDirtyRepaint();
            Repaint();
        }

        private void UpdatePlayIndicator()
        {
            if (_playIndicator == null) return;
            _playIndicator.style.backgroundColor = EditorApplication.isPlaying
                ? new Color(0.2f, 0.8f, 0.2f)
                : new Color(0.5f, 0.5f, 0.5f);
            _playIndicator.tooltip = EditorApplication.isPlaying ? "正在运行" : "未运行";
        }

        private void UpdateLayout()
        {
            if (_currentSnapshot == null || _currentSnapshot.states.Count == 0)
                return;

            // 计算布局
            float canvasWidth = _graphViewContainer.resolvedStyle.width;
            float canvasHeight = _graphViewContainer.resolvedStyle.height;

            if (canvasWidth <= 0) canvasWidth = 600;
            if (canvasHeight <= 0) canvasHeight = 400;

            _layoutEngine.CalculateLayout(_currentSnapshot, canvasWidth, canvasHeight);
        }

        private void DrawTreeView()
        {
            if (!_showTreeView)
                return;

            if (_currentSnapshot == null)
            {
                EditorGUILayout.HelpBox("尚未选择运行时 FSM。", MessageType.Info);
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("状态树", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawStateTreeNode("", 0);

            GUILayout.EndScrollView();
        }

        private void DrawStateTreeNode(string parentPath, int indent)
        {
            if (_currentSnapshot == null)
                return;

            foreach (var state in _currentSnapshot.states)
            {
                if (state.parentPath != parentPath)
                    continue;

                // 绘制缩进
                GUILayout.BeginHorizontal();
                GUILayout.Space(indent * 15);

                // 展开/折叠图标
                if (state.isStateMachine)
                {
                    var hasChildren = false;
                    foreach (var child in _currentSnapshot.states)
                    {
                        if (child.parentPath == state.path)
                        {
                            hasChildren = true;
                            break;
                        }
                    }

                    if (hasChildren)
                    {
                        GUILayout.Label("▶", GUILayout.Width(15));
                    }
                    else
                    {
                        GUILayout.Space(15);
                    }
                }
                else
                {
                    GUILayout.Space(15);
                }

                // 状态名称
                var style = new GUIStyle(EditorStyles.label);
                if (state.isExiting)
                {
                    style.normal.textColor = new Color(1f, 0.65f, 0.25f);
                    style.fontStyle = FontStyle.Bold;
                }
                else if (state.isEntering)
                {
                    style.normal.textColor = new Color(1f, 0.85f, 0.3f);
                    style.fontStyle = FontStyle.Bold;
                }
                else if (state.isActive)
                {
                    style.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                    style.fontStyle = FontStyle.Bold;
                }

                var label = state.isExiting
                    ? $"◐ {state.name}（等待退出）"
                    : state.isEntering
                        ? $"◑ {state.name}（等待进入）"
                        : state.isActive
                            ? $"● {state.name}"
                            : state.name;
                GUILayout.Label(label, style);

                // 持续时间
                if (state.isActive && state.activeDuration > 0)
                {
                    GUILayout.Label($"（{state.activeDuration:F1} 秒）", EditorStyles.miniLabel);
                }

                GUILayout.EndHorizontal();

                // 递归绘制子节点
                DrawStateTreeNode(state.path, indent + 1);
                if (!state.isStateMachine)
                    DrawBehaviorRuntimeRows(state.path, indent + 1);
            }
        }

        private void DrawBehaviorRuntimeRows(string statePath, int indent)
        {
            if (_currentSnapshot.behaviorNodes == null)
                return;

            foreach (var behavior in _currentSnapshot.behaviorNodes)
            {
                if (!string.Equals(behavior.statePath, statePath, StringComparison.Ordinal))
                    continue;

                GUILayout.BeginHorizontal();
                GUILayout.Space(indent * 15 + 15);
                var statusColor = GetBehaviorStatusColor(behavior.status);
                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = statusColor },
                    fontStyle = behavior.isActive ? FontStyle.Bold : FontStyle.Normal
                };
                GUILayout.Label(GetBehaviorStatusGlyph(behavior.status), statusStyle, GUILayout.Width(14));
                GUILayout.Label(GetBehaviorDisplayName(behavior), statusStyle);
                GUILayout.FlexibleSpace();
                if (behavior.elapsedTime > 0f)
                    GUILayout.Label($"{behavior.elapsedTime:F1} 秒", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
            }
        }

        private static Color GetBehaviorStatusColor(BehaviorNodeStatus status)
        {
            switch (status)
            {
                case BehaviorNodeStatus.Running:
                    return new Color(0.3f, 0.8f, 1f);
                case BehaviorNodeStatus.Success:
                    return new Color(0.3f, 0.85f, 0.35f);
                case BehaviorNodeStatus.Failure:
                    return new Color(1f, 0.35f, 0.35f);
                case BehaviorNodeStatus.Cancelled:
                    return new Color(1f, 0.65f, 0.25f);
                default:
                    return new Color(0.55f, 0.55f, 0.55f);
            }
        }

        private static string GetBehaviorStatusGlyph(BehaviorNodeStatus status)
        {
            switch (status)
            {
                case BehaviorNodeStatus.Running:
                    return ">";
                case BehaviorNodeStatus.Success:
                    return "V";
                case BehaviorNodeStatus.Failure:
                    return "X";
                case BehaviorNodeStatus.Cancelled:
                    return "-";
                default:
                    return ".";
            }
        }

        private static string GetBehaviorDisplayName(BehaviorNodeInfo behavior)
        {
            var name = string.IsNullOrEmpty(behavior.name) ? behavior.typeName : behavior.name;
            if (!IsDefaultBehaviorName(behavior.typeName, name)) return name;
            if (!AbilityKit.HFSM.BehaviorTypeRegistry.IsInitialized)
                AbilityKit.HFSM.BehaviorTypeRegistry.Initialize();
            return AbilityKit.HFSM.BehaviorTypeRegistry.GetDefinition(behavior.typeName)?.displayName ?? name;
        }

        private static bool IsDefaultBehaviorName(string typeName, string name)
        {
            if (name == typeName) return true;
            switch (typeName)
            {
                case "SetFloat": return name == "Set Float";
                case "SetBool": return name == "Set Bool";
                case "SetInt": return name == "Set Int";
                case "PlayAnimation": return name == "Play Animation";
                case "SetActive": return name == "Set Active";
                case "MoveTo": return name == "Move To";
                case "RandomSelector": return name == "Random Selector";
                case "RandomSequence": return name == "Random Sequence";
                case "TimeLimit": return name == "Time Limit";
                case "UntilSuccess": return name == "Until Success";
                case "UntilFailure": return name == "Until Failure";
                default: return false;
            }
        }

        private void DrawGraphView()
        {
            if (!_showGraphView)
                return;

            if (_currentSnapshot == null || _currentSnapshot.states.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    EditorApplication.isPlaying
                        ? "所选 FSM 暂无可用的运行时快照。"
                        : "请进入播放模式，并通过 LiveRegistry.Register(name, fsm) 注册 FSM。",
                    MessageType.Info);
                return;
            }

            // 图形画布
            var size = _graphCanvas?.contentRect.size ?? new Vector2(600f, 400f);
            var graphRect = new Rect(Vector2.zero, size);

            GUI.BeginGroup(graphRect);

            // 背景网格
            DrawGrid(graphRect);

            // 绘制连线
            DrawTransitions();

            // 绘制节点
            DrawNodes();

            GUI.EndGroup();
        }

        private void DrawGrid(Rect bounds)
        {
            var gridColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            float gridSize = 20f;

            // 垂直线
            for (float x = 0; x < bounds.width; x += gridSize)
            {
                Handles.color = gridColor;
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, bounds.height));
            }

            // 水平线
            for (float y = 0; y < bounds.height; y += gridSize)
            {
                Handles.color = gridColor;
                Handles.DrawLine(new Vector3(0, y), new Vector3(bounds.width, y));
            }
        }

        private void DrawNodes()
        {
            if (_currentSnapshot == null)
                return;

            foreach (var state in _currentSnapshot.states)
            {
                DrawStateNode(state);
            }
        }

        private void DrawStateNode(StateNodeInfo state)
        {
            var x = state.x;
            var y = state.y;
            var width = kNodeWidth;
            var height = kNodeHeight;

            // 节点矩形
            var rect = new Rect(x, y, width, height);

            // 背景颜色。Pending 状态优先于 active，避免延迟退出被绿色覆盖。
            Color bgColor;
            if (state.isExiting)
            {
                bgColor = new Color(0.6f, 0.3f, 0.2f, 0.9f); // 橙色（待退出）
            }
            else if (state.isEntering)
            {
                bgColor = new Color(0.6f, 0.6f, 0.2f, 0.9f); // 黄色（待进入）
            }
            else if (state.isActive)
            {
                bgColor = new Color(0.2f, 0.6f, 0.2f, 0.9f); // 绿色（激活）
            }
            else if (state.isStateMachine)
            {
                bgColor = new Color(0.3f, 0.4f, 0.5f, 0.9f); // 蓝色（状态机）
            }
            else
            {
                bgColor = new Color(0.35f, 0.35f, 0.35f, 0.9f); // 灰色（普通状态）
            }

            // 绘制阴影
            var shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.3f));

            // 绘制背景
            EditorGUI.DrawRect(rect, bgColor);

            // 绘制边框
            var borderColor = state.isExiting
                ? new Color(1f, 0.55f, 0.2f)
                : state.isEntering
                    ? new Color(1f, 0.85f, 0.25f)
                    : state.isActive
                        ? new Color(0.3f, 1f, 0.3f)
                        : new Color(0.5f, 0.5f, 0.5f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), borderColor);

            // 绘制内容
            GUI.BeginGroup(rect);

            // 状态名称
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(5, 5, width - 10, 20), state.name, labelStyle);

            // 类型标签
            var typeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            GUI.Label(new Rect(5, 25, width - 10, 15),
                state.isStateMachine ? "状态机" : "状态", typeStyle);

            // 激活时长
            if (state.isActive && state.activeDuration > 0)
            {
                var durationStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.6f, 1f, 0.6f) }
                };
                GUI.Label(new Rect(5, 38, width - 10, 12),
                    $"{state.activeDuration:F2} 秒", durationStyle);
            }

            GUI.EndGroup();
        }

        private void DrawTransitions()
        {
            if (_currentSnapshot == null)
                return;

            foreach (var transition in _currentSnapshot.transitions)
            {
                DrawTransition(transition);
            }
        }

        private void DrawTransition(TransitionInfo transition)
        {
            var fromState = _currentSnapshot.FindState(transition.fromPath);
            var toState = _currentSnapshot.FindState(transition.toPath);

            if (fromState == null || toState == null)
                return;

            // 计算起点和终点
            float startX = fromState.Value.x + kNodeWidth / 2;
            float startY = fromState.Value.y + kNodeHeight;
            float endX = toState.Value.x + kNodeWidth / 2;
            float endY = toState.Value.y;

            // 根据层级关系调整
            if (fromState.Value.nestingLevel > toState.Value.nestingLevel)
            {
                // 子状态到父状态
                startX = fromState.Value.x + kNodeWidth;
                startY = fromState.Value.y + kNodeHeight / 2;
                endX = toState.Value.x;
                endY = toState.Value.y + kNodeHeight / 2;
            }
            else if (fromState.Value.nestingLevel < toState.Value.nestingLevel)
            {
                // 父状态到子状态
                startX = fromState.Value.x + kNodeWidth / 2;
                startY = fromState.Value.y + kNodeHeight;
                endX = toState.Value.x + kNodeWidth / 2;
                endY = toState.Value.y;
            }

            // 连线颜色
            var lineColor = transition.canTransition
                ? new Color(0.3f, 0.7f, 0.3f, 0.8f)
                : new Color(0.5f, 0.5f, 0.5f, 0.5f);

            // 贝塞尔曲线控制点
            Vector3 start = new Vector3(startX, startY);
            Vector3 end = new Vector3(endX, endY);
            float controlOffset = Math.Abs(endY - startY) * 0.5f;

            Vector3 control1 = new Vector3(startX, startY + controlOffset);
            Vector3 control2 = new Vector3(endX, endY - controlOffset);

            // 绘制曲线
            Handles.color = lineColor;
            Handles.DrawBezier(start, end, control1, control2, lineColor, null, 2f);

            // 绘制箭头
            DrawArrow(endX, endY, endX > startX ? 0 : (endX < startX ? 180 : (endY > startY ? 90 : 270)), lineColor);

            // 绘制条件和真实运行语义标签。Legacy runtime 没有 priority/definition hash，
            // 因此这里只显示 provider 可以可靠读取的 Any 和 Force。
            var transitionLabel = BuildTransitionLabel(transition);
            if (!string.IsNullOrEmpty(transitionLabel))
            {
                var midPoint = BezierUtility.BezierPoint(start, control1, control2, end, 0.5f);
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                    alignment = TextAnchor.MiddleCenter
                };

                var labelContent = new GUIContent(transitionLabel);
                var labelSize = labelStyle.CalcSize(labelContent);
                GUI.Label(new Rect(midPoint.x - labelSize.x / 2, midPoint.y - labelSize.y / 2 - 10,
                    labelSize.x, labelSize.y), labelContent, labelStyle);
            }
        }

        private static string BuildTransitionLabel(TransitionInfo transition)
        {
            var prefix = transition.isFromAny ? "[任意状态] " : string.Empty;
            if (transition.forceInstantly)
                prefix += "[强制] ";
            var description = transition.conditionDescription ?? string.Empty;
            description = description.Replace(" [trigger: ", " [触发器：");
            return prefix + description;
        }

        private void DrawArrow(float x, float y, float angle, Color color)
        {
            Handles.color = color;

            float size = 8;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 tip = new Vector3(x, y);
            Vector3 left = new Vector3(x - size * Mathf.Cos(rad - 0.5f), y - size * Mathf.Sin(rad - 0.5f));
            Vector3 right = new Vector3(x - size * Mathf.Cos(rad + 0.5f), y - size * Mathf.Sin(rad + 0.5f));

            Handles.DrawLine(tip, left);
            Handles.DrawLine(tip, right);
        }

        private void DrawParameterPanel()
        {
            if (!_showParameters || _currentSnapshot == null)
                return;

            _parameterScrollPosition = GUILayout.BeginScrollView(_parameterScrollPosition);

            EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);

            if (_currentSnapshot.parameters.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var param in _currentSnapshot.parameters)
                {
                    DrawParameter(param);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("暂无参数", MessageType.None);
            }

            GUILayout.EndScrollView();
        }

        private void DrawParameter(ParameterInfo param)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(param.name, GUILayout.Width(100));

            string valueStr;
            switch (param.type)
            {
                case ParameterType.Bool:
                    valueStr = param.boolValue ? "真" : "假";
                    break;
                case ParameterType.Int:
                    valueStr = param.intValue.ToString();
                    break;
                case ParameterType.Float:
                    valueStr = param.floatValue.ToString("F2");
                    break;
                case ParameterType.Trigger:
                    valueStr = "[触发器]";
                    break;
                default:
                    valueStr = "?";
                    break;
            }

            EditorGUILayout.LabelField(valueStr, EditorStyles.textField);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHistoryPanel()
        {
            if (!_showHistory || _currentSnapshot == null)
                return;

            _historyScrollPosition = GUILayout.BeginScrollView(_historyScrollPosition);

            EditorGUILayout.LabelField("最近转换", EditorStyles.boldLabel);

            if (_currentSnapshot.history.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var count = Math.Min(_currentSnapshot.history.Count, 5);
                for (int i = _currentSnapshot.history.Count - count; i < _currentSnapshot.history.Count; i++)
                {
                    var record = _currentSnapshot.history[i];
                    EditorGUILayout.LabelField(
                        $"[{record.timeAgo:F1} 秒] {record.fromPath} → {record.toPath}",
                        EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("暂无转换记录", MessageType.None);
            }

            GUILayout.EndScrollView();
        }
    }
}

#endif
