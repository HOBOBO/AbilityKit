// ============================================================================
// HfsmRuntimeMonitorWindow - 运行时状态机监控窗口
// 提供树形视图、图形视图和状态追踪功能
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityHFSM.Visualization;
using LiveRegistry = UnityHFSM.Visualization.LiveRegistry;

namespace UnityHFSM.Editor.RuntimeMonitor
{
    /// <summary>
    /// 运行时状态机监控窗口
    /// </summary>
    public class HfsmRuntimeMonitorWindow : EditorWindow
    {
        [MenuItem("Window/AbilityKit/HFSM Runtime Monitor")]
        public static void OpenWindow()
        {
            var window = GetWindow<HfsmRuntimeMonitorWindow>();
            window.titleContent = new GUIContent(
                "HFSM Runtime Monitor",
                EditorGUIUtility.IconContent("AnimatorController Icon").image
            );
            window.minSize = new Vector2(800, 500);
        }

        // 数据
        private int _selectedFsmIndex = -1;
        private FsmSnapshot _currentSnapshot;
        private Vector2 _scrollPosition;
        private bool _showParameters = true;
        private bool _showHistory = true;
        private bool _showTreeView = true;
        private bool _showGraphView = true;

        // 布局引擎
        private AutoLayoutEngine _layoutEngine;

        // 视图元素
        private VisualElement _root;
        private VisualElement _treeViewContainer;
        private VisualElement _graphViewContainer;
        private IMGUIContainer _graphCanvas;
        private VisualElement _parameterPanel;
        private VisualElement _historyPanel;

        // 样式
        private const float kNodeWidth = 140f;
        private const float kNodeHeight = 50f;
        private const float kNodeSpacingX = 40f;
        private const float kNodeSpacingY = 30f;
        private const float kNodeMarginLeft = 50f;
        private const float kNodeMarginTop = 30f;

        private void OnEnable()
        {
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
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            // 在播放模式下定期更新快照
            if (EditorApplication.timeSinceStartup % 0.5 < 0.1)
            {
                LiveRegistry.UpdateAllSnapshots();
            }
        }

        private void OnRegistryChanged()
        {
            RefreshFsmList();
        }

        private void OnSnapshotUpdated(object fsm)
        {
            if (_selectedFsmIndex >= 0)
            {
                var entry = LiveRegistry.GetEntry(_selectedFsmIndex);
                if (entry?.Target == fsm)
                {
                    _currentSnapshot = entry.Snapshot;
                    UpdateLayout();
                    Repaint();
                }
            }
        }

        private void CreateUI()
        {
            _root = rootVisualElement;
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

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // FSM 选择器
            var fsmLabel = new Label("FSM:");
            fsmLabel.style.marginLeft = 5;
            fsmLabel.style.marginRight = 5;
            fsmLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            fsmLabel.style.width = 40;
            toolbar.Add(fsmLabel);

            var fsmDropdown = new ToolbarMenu { text = "Select FSM..." };
            fsmDropdown.style.flexGrow = 1;
            toolbar.Add(fsmDropdown);

            // 刷新按钮
            var refreshButton = new ToolbarButton(() => RefreshFsmList()) { text = "Refresh" };
            toolbar.Add(refreshButton);

            // 视图切换
            var viewToggle = new ToolbarToggle { text = "Show Graph" };
            viewToggle.value = _showGraphView;
            viewToggle.RegisterValueChangedCallback(evt =>
            {
                _showGraphView = evt.newValue;
                UpdateViewVisibility();
            });
            toolbar.Add(viewToggle);

            var treeToggle = new ToolbarToggle { text = "Show Tree" };
            treeToggle.value = _showTreeView;
            treeToggle.RegisterValueChangedCallback(evt =>
            {
                _showTreeView = evt.newValue;
                UpdateViewVisibility();
            });
            toolbar.Add(treeToggle);

            // 播放状态指示
            var playIndicator = new VisualElement();
            playIndicator.style.width = 12;
            playIndicator.style.height = 12;
            playIndicator.style.borderTopLeftRadius = 6;
            playIndicator.style.borderTopRightRadius = 6;
            playIndicator.style.borderBottomLeftRadius = 6;
            playIndicator.style.borderBottomRightRadius = 6;
            playIndicator.style.marginLeft = 10;
            playIndicator.style.backgroundColor = EditorApplication.isPlaying
                ? new Color(0.2f, 0.8f, 0.2f)  // 绿色
                : new Color(0.5f, 0.5f, 0.5f); // 灰色
            playIndicator.tooltip = EditorApplication.isPlaying ? "Playing" : "Not Playing";
            toolbar.Add(playIndicator);

            _root.Add(toolbar);
        }

        private void CreateMainContent()
        {
            var mainContent = new VisualElement();
            mainContent.style.flexGrow = 1;
            mainContent.style.flexDirection = FlexDirection.Row;

            // 左侧树形视图
            _treeViewContainer = new VisualElement();
            _treeViewContainer.style.width = 220;
            _treeViewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            _treeViewContainer.style.borderRightWidth = 1;
            _treeViewContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            mainContent.Add(_treeViewContainer);

            // 右侧图形视图
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            rightPanel.style.flexDirection = FlexDirection.Column;

            _graphViewContainer = new VisualElement();
            _graphViewContainer.style.flexGrow = 1;
            rightPanel.Add(_graphViewContainer);

            // 参数面板（可折叠）
            _parameterPanel = CreateParameterPanel();
            _parameterPanel.style.height = 120;
            rightPanel.Add(_parameterPanel);

            mainContent.Add(rightPanel);
            _root.Add(mainContent);
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

            var title = new Label("Parameters");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;

            var toggle = new Label("▼");
            toggle.style.width = 20;
            toggle.style.cursor = StyleKeyword.Auto;
            toggle.RegisterCallback<ClickEvent>(evt =>
            {
                _showParameters = !_showParameters;
                toggle.text = _showParameters ? "▼" : "▶";
                _parameterPanel.style.height = _showParameters ? 120 : 25;
            });

            header.Add(title);
            header.Add(toggle);
            panel.Add(header);

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

            var title = new Label("History");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;

            header.Add(title);
            panel.Add(header);

            return panel;
        }

        private void CreateBottomPanel()
        {
            // 历史记录面板（可折叠）
            _historyPanel = CreateHistoryPanel();
            _historyPanel.style.height = 100;
            _root.Add(_historyPanel);
        }

        private void UpdateViewVisibility()
        {
            if (_treeViewContainer != null)
                _treeViewContainer.style.display = _showTreeView ? DisplayStyle.Flex : DisplayStyle.None;

            if (_graphViewContainer != null)
                _graphViewContainer.style.display = _showGraphView ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshFsmList()
        {
            var entries = LiveRegistry.GetEntries();

            if (entries.Count == 0)
            {
                return;
            }

            // 验证当前选择是否有效
            if (_selectedFsmIndex < 0 || _selectedFsmIndex >= entries.Count)
            {
                _selectedFsmIndex = 0;
            }

            // 更新快照
            var entry = entries[_selectedFsmIndex];
            _currentSnapshot = entry?.Snapshot;

            if (_currentSnapshot != null)
            {
                UpdateLayout();
            }

            Repaint();
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

        private void OnGUI()
        {
            // 检查是否在播放模式
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                DrawPlayModeWarning();
            }

            DrawGraphView();
            DrawTreeView();
            DrawParameterPanel();
            DrawHistoryPanel();
        }

        private void DrawPlayModeWarning()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to monitor running state machines.\n" +
                    "Call LiveRegistry.Register(name, fsm) from your code to register FSMs.",
                    MessageType.Info);
            }
        }

        private void DrawTreeView()
        {
            if (!_showTreeView || _currentSnapshot == null)
                return;

            GUILayout.BeginArea(new Rect(0, 25, 220, position.height - 25 - 100));
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("State Tree", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawStateTreeNode("", 0);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
                if (state.isActive)
                {
                    style.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                    style.fontStyle = FontStyle.Bold;
                }

                var label = state.isActive ? $"● {state.name}" : state.name;
                GUILayout.Label(label, style);

                // 持续时间
                if (state.isActive && state.activeDuration > 0)
                {
                    GUILayout.Label($"({state.activeDuration:F1}s)", EditorStyles.miniLabel);
                }

                GUILayout.EndHorizontal();

                // 递归绘制子节点
                DrawStateTreeNode(state.path, indent + 1);
            }
        }

        private void DrawGraphView()
        {
            if (!_showGraphView || _currentSnapshot == null || _currentSnapshot.states.Count == 0)
                return;

            // 图形画布
            var graphRect = _showTreeView
                ? new Rect(220, 25, position.width - 220, position.height - 25 - 100)
                : new Rect(0, 25, position.width, position.height - 25 - 100);

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

            // 背景颜色
            Color bgColor;
            if (state.isActive)
            {
                bgColor = new Color(0.2f, 0.6f, 0.2f, 0.9f); // 绿色（激活）
            }
            else if (state.isEntering)
            {
                bgColor = new Color(0.6f, 0.6f, 0.2f, 0.9f); // 黄色（进入）
            }
            else if (state.isExiting)
            {
                bgColor = new Color(0.6f, 0.3f, 0.2f, 0.9f); // 橙色（退出）
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
            var borderColor = state.isActive
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
                state.isStateMachine ? "State Machine" : "State", typeStyle);

            // 激活时长
            if (state.isActive && state.activeDuration > 0)
            {
                var durationStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.6f, 1f, 0.6f) }
                };
                GUI.Label(new Rect(5, 38, width - 10, 12),
                    $"{state.activeDuration:F2}s", durationStyle);
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

            // 绘制条件标签
            if (!string.IsNullOrEmpty(transition.conditionDescription))
            {
                var midPoint = BezierUtility.BezierPoint(start, control1, control2, end, 0.5f);
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                    alignment = TextAnchor.MiddleCenter
                };

                var labelContent = new GUIContent(transition.conditionDescription);
                var labelSize = labelStyle.CalcSize(labelContent);
                GUI.Label(new Rect(midPoint.x - labelSize.x / 2, midPoint.y - labelSize.y / 2 - 10,
                    labelSize.x, labelSize.y), labelContent, labelStyle);
            }
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

            GUILayout.BeginArea(new Rect(0, position.height - 100, position.width, 100));
            GUILayout.BeginScrollView(Vector2.zero);

            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

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
                EditorGUILayout.HelpBox("No parameters", MessageType.None);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawParameter(ParameterInfo param)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(param.name, GUILayout.Width(100));

            string valueStr;
            switch (param.type)
            {
                case ParameterType.Bool:
                    valueStr = param.boolValue ? "True" : "False";
                    break;
                case ParameterType.Int:
                    valueStr = param.intValue.ToString();
                    break;
                case ParameterType.Float:
                    valueStr = param.floatValue.ToString("F2");
                    break;
                case ParameterType.Trigger:
                    valueStr = "[Trigger]";
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

            GUILayout.BeginArea(new Rect(0, position.height - 100, position.width, 100));
            GUILayout.BeginScrollView(Vector2.right);

            EditorGUILayout.LabelField("Recent Transitions", EditorStyles.boldLabel);

            if (_currentSnapshot.history.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var count = Math.Min(_currentSnapshot.history.Count, 5);
                for (int i = _currentSnapshot.history.Count - count; i < _currentSnapshot.history.Count; i++)
                {
                    var record = _currentSnapshot.history[i];
                    EditorGUILayout.LabelField(
                        $"[{record.timeAgo:F1}s] {record.fromPath} → {record.toPath}",
                        EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No transitions recorded", MessageType.None);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

#endif
