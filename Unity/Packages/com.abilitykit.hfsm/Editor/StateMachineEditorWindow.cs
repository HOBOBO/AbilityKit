using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.State;
using AbilityKit.Editor.Platform.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Editor.Diagnostics;
using AbilityKit.HFSM.Editor.RuntimeMonitor;
using AbilityKit.HFSM.Graph.Descriptor;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// Main editor window for HFSM graph editing.
    /// Similar to Unity's Animator window but for hierarchical state machines.
    /// </summary>
    public class StateMachineEditorWindow : EditorWindow
    {
        private const string LastGraphGuidStateKey = "last-graph-guid";
        private const string LastZoomStateKey = "last-zoom";
        private const string LastPanXStateKey = "last-pan-x";
        private const string LastPanYStateKey = "last-pan-y";
        private const string DiagnosticsVisibleStateKey = "diagnostics-visible";

        private readonly IEditorUserStateStore _userState =
            new EditorPrefsUserStateStore("hfsm", "graph-editor");
        private readonly EditorCommandRegistry _commands = new EditorCommandRegistry();
        private readonly List<IDisposable> _commandRegistrations = new List<IDisposable>();
        private EditorContext _context;
        private VisualElement _root;

        // IMGUI containers for graph layers
        private IMGUIContainer _graphContainer;
        private IMGUIContainer _toolbarContainer;
        private IMGUIContainer _breadcrumbContainer;

        // Layers
        private GraphBackgroundLayer _backgroundLayer;
        private GraphTransitionLayer _transitionLayer;
        private GraphStateLayer _stateLayer;
        private GraphNavigationLayer _navigationLayer;

        // Toolbar
        private ToolbarButton _backButton;
        private Label _breadcrumbLabel;
        private ToolbarButton _diagnosticsButton;
        private VisualElement _diagnosticsPane;

        [MenuItem("Window/AbilityKit/HFSM 状态机编辑器")]
        public static void OpenWindow()
        {
            var window = GetWindow<StateMachineEditorWindow>();
            window.titleContent = new GUIContent("HFSM 编辑器", EditorGUIUtility.IconContent("AnimatorStateMachine Icon").image);
            window.minSize = new Vector2(800, 600);
        }

        public static void Open(Graph.GraphAsset graph)
        {
            OpenWindow();
            var window = GetWindow<StateMachineEditorWindow>();
            if (graph != null)
                window.LoadGraph(graph);
            window.Show();
            window.Focus();
        }

        public static void CreateGraphAssetAndOpen()
        {
            OpenWindow();
            GetWindow<StateMachineEditorWindow>().CreateNewGraph();
        }

        [MenuItem("Assets/编辑 HFSM 状态机图", true)]
        private static bool ValidateOpenGraphAsset()
        {
            return Selection.activeObject is Graph.GraphAsset;
        }

        [MenuItem("Assets/编辑 HFSM 状态机图")]
        private static void OpenGraphAsset()
        {
            var window = GetWindow<StateMachineEditorWindow>();
            window.titleContent = new GUIContent("HFSM 编辑器", EditorGUIUtility.IconContent("AnimatorStateMachine Icon").image);
            window.minSize = new Vector2(800, 600);

            if (Selection.activeObject is Graph.GraphAsset graph)
            {
                window.LoadGraph(graph);
            }
        }

        private void OnEnable()
        {
            RegisterCommands();
            _context = new EditorContext();
            _context.OnContextChanged += OnContextChanged;
            _context.OnStateMachineChanged += OnStateMachineChanged;

            CreateUI();
            CreateLayers();

            // Initialize view state
            bool hasSavedView = _userState.HasKey(LastZoomStateKey);
            if (hasSavedView)
            {
                float zoom = _userState.GetFloat(LastZoomStateKey);
                float panX = _userState.GetFloat(LastPanXStateKey);
                float panY = _userState.GetFloat(LastPanYStateKey);
                _context.ZoomFactor = zoom;
                _context.PanOffset = new Vector2(panX, panY);
            }
            else
            {
                // No saved view - will auto-frame on first render
                _context.ZoomFactor = 1f;
                _context.PanOffset = Vector2.zero;
                _pendingFrameAll = true;
            }

            // Try to restore last opened graph
            RestoreLastGraph();
        }


        private void RestoreLastGraph()
        {
            if (!_userState.HasKey(LastGraphGuidStateKey))
            {
                // No saved graph - will need auto-frame when a graph is loaded
                return;
            }

            string guid = _userState.GetString(LastGraphGuidStateKey);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            var graph = AssetDatabase.LoadAssetAtPath<Graph.GraphAsset>(path);
            if (graph != null)
            {
                LoadGraph(graph);
            }
        }

        private void OnDisable()
        {
            // Save view state
            if (_context != null)
            {
                _userState.SetFloat(LastZoomStateKey, _context.ZoomFactor);
                _userState.SetFloat(LastPanXStateKey, _context.PanOffset.x);
                _userState.SetFloat(LastPanYStateKey, _context.PanOffset.y);
            }

            // Save last opened graph
            if (_context != null && _context.GraphAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(_context.GraphAsset);
                if (!string.IsNullOrEmpty(path))
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    _userState.SetString(LastGraphGuidStateKey, guid);
                }
            }

            if (_context != null)
            {
                _context.OnContextChanged -= OnContextChanged;
                _context.OnStateMachineChanged -= OnStateMachineChanged;
            }
            _diagnosticsPanel?.Dispose();
            foreach (var registration in _commandRegistrations)
                registration.Dispose();
            _commandRegistrations.Clear();
        }

        private void RegisterCommands()
        {
            if (_commandRegistrations.Count > 0) return;
            RegisterCommand("hfsm.graph.new", "hfsm.command.new", _ => CreateNewGraph());
            RegisterCommand("hfsm.graph.load", "hfsm.command.load", _ => LoadGraphFromPicker());
            RegisterCommand("hfsm.graph.save", "hfsm.command.save", _ => SaveCurrentGraph());
            RegisterCommand("hfsm.graph.back", "hfsm.command.back", _ => _context?.NavigateBack());
            RegisterCommand("hfsm.graph.validate-menu", "hfsm.command.validate", _ => ShowValidateMenu());
            RegisterCommand("hfsm.graph.validate-next", "hfsm.command.validate-next", _ => ValidateNextGraph());
            RegisterCommand("hfsm.graph.validate-legacy", "hfsm.command.validate-legacy", _ => ValidateLegacyGraph());
            RegisterCommand("hfsm.graph.frame-all", "hfsm.command.frame-all", _ => FrameAll());
            RegisterCommand("hfsm.graph.toggle-diagnostics", "hfsm.command.diagnostics", _ => ToggleDiagnostics());
            RegisterCommand("hfsm.graph.export-menu", "hfsm.command.export", _ => ShowExportMenu());
            RegisterCommand("hfsm.graph.export-next", "hfsm.command.export-next", _ => ExportNextDefinition());
            RegisterCommand("hfsm.graph.debug", "hfsm.command.debug", _ => OpenRuntimeDebugger());
        }

        private void RegisterCommand(string id, string labelKey, Action<EditorCommandContext> execute)
        {
            _commandRegistrations.Add(_commands.Register(new EditorCommand(id, labelKey, execute)));
        }

        private void ExecuteCommand(string id)
        {
            _commands.Execute(id, new EditorCommandContext(this, _context?.GraphAsset));
        }

        private void CreateUI()
        {
            _root = rootVisualElement;

            // Load UXML template
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.abilitykit.hfsm/Editor/Resources/EditorLayout.uxml");

            if (visualTree != null)
            {
                _root.Add(visualTree.Instantiate());
            }

            // Load stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.abilitykit.hfsm/Editor/Resources/EditorStyles.uss");

            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }

            // Get UI elements
            var rootElement = _root.Q<VisualElement>("Root");
            if (rootElement != null)
            {
                rootElement.style.width = position.width;
                rootElement.style.height = position.height;
            }

            // Wire up toolbar buttons
            CreateToolbar();

            // Create main content area with IMGUIContainers
            CreateMainContent();
        }

        private void OnGUI()
        {
            // Ensure root element matches window size
            var rootElement = _root.Q<VisualElement>("Root");
            if (rootElement != null)
            {
                if (rootElement.style.width != position.width)
                    rootElement.style.width = position.width;
                if (rootElement.style.height != position.height)
                    rootElement.style.height = position.height;
            }
        }

        private void CreateToolbar()
        {
            // Get UI elements from UXML
            VisualElement breadcrumbArea = _root.Q<VisualElement>("breadcrumb-area");
            _backButton = _root.Q<ToolbarButton>("BackButton");
            _breadcrumbLabel = _root.Q<Label>("BreadcrumbLabel");

            // Wire up back button
            if (_backButton != null)
            {
                _backButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.back"));
                _backButton.SetEnabled(false);
            }

            // Wire up action buttons from UXML through stable platform commands.
            ToolbarButton newButton = _root.Q<ToolbarButton>("NewButton");
            if (newButton != null)
                newButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.new"));

            ToolbarButton loadButton = _root.Q<ToolbarButton>("LoadButton");
            if (loadButton != null)
                loadButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.load"));

            ToolbarButton saveButton = _root.Q<ToolbarButton>("SaveButton");
            if (saveButton != null)
                saveButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.save"));

            ToolbarButton validateButton = _root.Q<ToolbarButton>("ValidateButton");
            if (validateButton != null)
                validateButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.validate-menu"));

            ToolbarButton frameButton = _root.Q<ToolbarButton>("FrameButton");
            if (frameButton != null)
                frameButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.frame-all"));

            _diagnosticsButton = _root.Q<ToolbarButton>("DiagnosticsButton");
            if (_diagnosticsButton != null)
                _diagnosticsButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.toggle-diagnostics"));

            _diagnosticsPane = _root.Q<VisualElement>("DiagnosticsPane");
            SetDiagnosticsVisible(_userState.GetBool(DiagnosticsVisibleStateKey, true));

            ToolbarButton debugButton = _root.Q<ToolbarButton>("DebugButton");
            if (debugButton != null)
                debugButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.debug"));

            ToolbarButton exportButton = _root.Q<ToolbarButton>("ExportButton");
            if (exportButton != null)
                exportButton.clickable = new Clickable(() => ExecuteCommand("hfsm.graph.export-menu"));
        }

        private void ShowExportMenu()
        {
            var menu = new GenericMenu();
            var actions = ExportActionRegistry.CreateActions(
                () => ExecuteCommand("hfsm.graph.export-next"),
                ExportLegacyArchive);
            foreach (var action in actions)
                menu.AddItem(
                    new GUIContent(action.Label, action.Description),
                    false,
                    () => action.Execute());
            menu.ShowAsContext();
        }

        private void ShowValidateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Next Runtime 校验"),
                false,
                () => ExecuteCommand("hfsm.graph.validate-next"));
            menu.AddItem(
                new GUIContent("旧版状态机图校验"),
                false,
                () => ExecuteCommand("hfsm.graph.validate-legacy"));
            menu.ShowAsContext();
        }

        private void ExportNextDefinition()
        {
            if (_context.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("导出", "尚未加载可导出的状态机图。", "确定");
                return;
            }

            var diagnostics = RunNextDiagnostics();
            var result = diagnostics?.ExportResult;

            if (result == null || !result.IsSuccess)
            {
                SetDiagnosticsVisible(true);
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject(
                "导出 HFSM Next Runtime 定义",
                _context.GraphAsset.GraphName + ".hfsm",
                "json",
                "请选择已校验运行时定义的保存位置");
            if (string.IsNullOrEmpty(path)) return;

            var report = EditorExportExecutor.Execute(new[]
            {
                new EditorExportJob(
                    "hfsm.export.next-definition",
                    path,
                    "json",
                    () => ExportText("hfsm.export.next-definition", "json", path, result.Json, "定义哈希：" + diagnostics.DefinitionHash))
            });
            EditorExportReportWindow.Show("HFSM 导出", report);
        }

        private void ExportLegacyArchive(string exporterName)
        {
            if (_context.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("导出", "尚未加载可导出的状态机图。", "确定");
                return;
            }

            var exporter = ExtensionRegistry.GetExporter(exporterName);
            if (exporter == null)
            {
                EditorUtility.DisplayDialog("导出", $"未找到导出器：{exporterName}。", "确定");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "导出 HFSM 状态机图",
                _context.GraphAsset.GraphName + "_export",
                exporter.FileExtension,
                "请选择导出文件的保存位置");

            if (string.IsNullOrEmpty(path))
                return;

            var jobId = "hfsm.export.legacy." + exporterName.ToLowerInvariant();
            var report = EditorExportExecutor.Execute(new[]
            {
                new EditorExportJob(jobId, path, exporter.FileExtension, () =>
                {
                    try
                    {
                        // Create graph descriptor from the asset
                        var graphDescriptor = Graph.Descriptor.Impl.GraphDescriptorFactory.Create(_context.GraphAsset);
                        var options = ExportOptions.ForRuntime;
                        var result = exporter.Export(graphDescriptor, options);
                        if (!result.success)
                            return EditorExportReportEntry.Failed(jobId, path, result.errorMessage);
                        return ExportText(jobId, exporter.FileExtension, path, result.data);
                    }
                    catch (Exception e)
                    {
                        return EditorExportReportEntry.Failed(jobId, path, e.Message);
                    }
                })
            });
            EditorExportReportWindow.Show("HFSM 导出", report);
        }

        private static EditorExportReportEntry ExportText(string jobId, string format, string path, string content, params string[] messages)
        {
            var status = EditorAtomicFileWriter.WriteAllText(path, content);
            AssetDatabase.Refresh();
            return new EditorExportReportEntry(
                jobId,
                path,
                status == EditorAtomicWriteStatus.Unchanged ? EditorExportStatus.Unchanged : EditorExportStatus.Exported,
                new[] { new EditorExportArtifact(path, format) },
                messages);
        }

        // Panels
        private InspectorPanel _inspectorPanel;
        private ParameterPanel _parameterPanel;
        private DiagnosticsPanel _diagnosticsPanel;

        // Auto-frame flag to defer framing until view has valid size
        private bool _pendingFrameAll = false;

        private void CreateMainContent()
        {
            _root = rootVisualElement;

            // Try to find graph container from UXML with proper path
            _graphContainer = FindIMGUIContainer(_root, "graph-view");

            if (_graphContainer == null)
            {
                Debug.LogError("HFSM 编辑器：无法在 UXML 中找到 graph-view IMGUIContainer。");
                return;
            }

            _graphContainer.onGUIHandler += OnGraphGUI;

            // Create parameter panel
            _parameterPanel = new ParameterPanel();
            _parameterPanel.Initialize(_context);

            IMGUIContainer parameterContainer = FindIMGUIContainer(_root, "ParameterPanel");
            if (parameterContainer != null)
            {
                parameterContainer.onGUIHandler += () => _parameterPanel.OnGUI();
            }

            // Create inspector panel
            _inspectorPanel = CreateInstance<InspectorPanel>();
            _inspectorPanel.Initialize(_context);

            IMGUIContainer inspectorContainer = FindIMGUIContainer(_root, "InspectorPanel");
            if (inspectorContainer != null)
            {
                inspectorContainer.onGUIHandler += () => _inspectorPanel.OnGUI();
            }

            _diagnosticsPanel = new DiagnosticsPanel();
            _diagnosticsPanel.Initialize(_context);
            IMGUIContainer diagnosticsContainer = FindIMGUIContainer(_root, "DiagnosticsPanel");
            if (diagnosticsContainer != null)
                diagnosticsContainer.onGUIHandler += () => _diagnosticsPanel.OnGUI();
        }

        private IMGUIContainer FindIMGUIContainer(VisualElement root, string name)
        {
            // Try direct query first
            var container = root.Q<IMGUIContainer>(name);
            if (container != null)
                return container;

            // Search in all children
            foreach (var child in root.Children())
            {
                container = FindIMGUIContainer(child, name);
                if (container != null)
                    return container;
            }

            return null;
        }

        private void CreateLayers()
        {
            _backgroundLayer = new GraphBackgroundLayer(this);
            _transitionLayer = new GraphTransitionLayer(this);
            _stateLayer = new GraphStateLayer(this);
            _navigationLayer = new GraphNavigationLayer(this);

            // Initialize all layers with the shared context
            _backgroundLayer.Initialize(_context);
            _transitionLayer.Initialize(_context);
            _stateLayer.Initialize(_context);
            _navigationLayer.Initialize(_context);
        }

        private void OnGraphGUI()
        {
            if (_context == null || _graphContainer == null)
                return;

            // Update layer bounds
            Rect viewRect = _graphContainer.contentRect;

            // Auto-frame if pending (for initial load)
            if (_pendingFrameAll)
            {
                TryAutoFrameOnValidView(viewRect);
            }

            if (viewRect.width <= 0 || viewRect.height <= 0)
                return;

            // Update context view bounds for centering
            _context.UpdateViewBounds(viewRect);

            // Begin GUI group to clip content to view bounds
            GUI.BeginGroup(viewRect);

            // Render layers in order
            _backgroundLayer.ViewBounds = viewRect;
            _backgroundLayer.OnGUI(viewRect);

            _transitionLayer.ViewBounds = viewRect;
            _transitionLayer.OnGUI(viewRect);

            _stateLayer.ViewBounds = viewRect;
            _stateLayer.OnGUI(viewRect);

            // Process events in order: navigation first, then state, then transition
            _navigationLayer.ProcessEvent();
            _stateLayer.ProcessEvent();
            _transitionLayer.ProcessEvent();

            if (_context.GraphAsset == null)
            {
                DrawNoGraphMessage(viewRect);
            }

            // Draw transition preview info
            if (_context.IsPreviewTransition)
            {
                DrawTransitionPreviewInfo(viewRect);
            }

            // End GUI group
            GUI.EndGroup();

            // Update inspector
            if (_inspectorPanel != null)
            {
                _inspectorPanel.Repaint();
            }
        }

        private void DrawNoGraphMessage(Rect rect)
        {
            Rect messageRect = new Rect(rect.width / 2 - 150, rect.height / 2 - 30, 300, 60);
            GUI.Box(messageRect, "", EditorStyles.helpBox);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = Color.gray }
            };

            if (_context.GraphAsset != null)
            {
                // Debug info
                GUI.Label(new Rect(10, 10, 400, 100),
                    $"状态机图：{_context.GraphAsset.name}\n" +
                    $"节点数：{_context.CurrentChildNodes.Count}\n" +
                    $"平移：{_context.PanOffset}\n" +
                    $"缩放：{_context.ZoomFactor}\n" +
                    $"视图边界：{_graphContainer.contentRect}",
                    labelStyle);
            }
            else
            {
                GUI.Label(messageRect, "未加载状态机图。\n请使用“新建”或“加载”来创建或打开状态机图。", labelStyle);
            }
        }

        private void DrawTransitionPreviewInfo(Rect rect)
        {
            Rect infoRect = new Rect(10, rect.height - 30, 300, 25);
            GUI.Label(infoRect, "正在创建转换……单击目标状态，或按 ESC 取消。",
                EditorStyles.miniLabel);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is Graph.GraphAsset graph)
            {
                LoadGraph(graph);
            }
            Repaint();
        }

        private void OnContextChanged()
        {
            UpdateBreadcrumb();
            Repaint();
        }

        private void OnStateMachineChanged()
        {
            UpdateBreadcrumb();
            _backButton.SetEnabled(_context.StateMachinePath.Count > 1);
            Repaint();
        }

        private void UpdateBreadcrumb()
        {
            if (_context.GraphAsset == null)
            {
                _breadcrumbLabel.text = "未加载状态机图";
                return;
            }

            _breadcrumbLabel.text = $"{_context.GraphAsset.GraphName} > {_context.GetPathString()}";
        }

        #region Actions

        private void CreateNewGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "新建 HFSM 状态机图",
                "新建 HFSM 状态机图",
                "asset",
                "请输入新 HFSM 状态机图的名称");

            if (string.IsNullOrEmpty(path))
                return;

            var graph = ScriptableObject.CreateInstance<Graph.GraphAsset>();
            graph.GraphName = System.IO.Path.GetFileNameWithoutExtension(path);

            // Create root state machine
            var rootSM = graph.CreateStateMachine("根状态机", new Vector2(200, 100));
            graph.SetRootStateMachine(rootSM);

            // Create initial state
            var initialState = graph.CreateState("初始状态", new Vector2(200, 250));
            rootSM.AddChildNode(initialState.Id);
            initialState.isDefault = true;
            rootSM.DefaultStateId = initialState.Id;

            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();

            LoadGraph(graph);
        }

        private void LoadGraphFromPicker()
        {
            string path = EditorUtility.OpenFilePanel(
                "加载 HFSM 状态机图",
                "Assets",
                "asset");

            if (string.IsNullOrEmpty(path))
                return;

            path = "Assets" + path.Replace(Application.dataPath, "").Replace("\\", "/");
            var graph = AssetDatabase.LoadAssetAtPath<Graph.GraphAsset>(path);

            if (graph != null)
            {
                LoadGraph(graph);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "所选文件不是有效的 HFSM 状态机图资源。", "确定");
            }
        }

        private void SaveCurrentGraph()
        {
            if (_context?.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("保存 HFSM 状态机图", "尚未加载状态机图。", "确定");
                return;
            }

            EditorUtility.SetDirty(_context.GraphAsset);
            AssetDatabase.SaveAssetIfDirty(_context.GraphAsset);
        }

        private void OpenRuntimeDebugger()
        {
            RuntimeMonitorWindow.OpenWindow(_context?.GraphAsset?.GraphName);
        }

        public void LoadGraph(Graph.GraphAsset graph)
        {
            if (graph == null)
                return;

            _context.GraphAsset = graph;
            _context.Reset();
            UpdateBreadcrumb();

            // Defer framing until view has valid size
            _pendingFrameAll = true;

            Repaint();
        }

        private void TryAutoFrameOnValidView(Rect viewRect)
        {
            if (!_pendingFrameAll)
                return;

            // Determine the view rect to use
            float viewWidth = viewRect.width > 0 ? viewRect.width : 800f;
            float viewHeight = viewRect.height > 0 ? viewRect.height : 600f;
            Rect effectiveRect = new Rect(0, 0, viewWidth, viewHeight);

            _pendingFrameAll = false;

            // Update context view bounds for calculations
            _context.UpdateViewBounds(effectiveRect);

            // Check if we have nodes
            if (_context.CurrentChildNodes.Count > 0)
            {
                // Frame all nodes
                Rect bounds = _context.GetNodesBounds();
                float zoomX = viewWidth / (bounds.width + 100);
                float zoomY = viewHeight / (bounds.height + 100);
                float zoom = Mathf.Min(zoomX, zoomY, 1f);

                _context.ZoomFactor = zoom;
                _context.PanOffset = new Vector2(
                    viewWidth / 2 - (bounds.x + bounds.width / 2) * zoom,
                    viewHeight / 2 - (bounds.y + bounds.height / 2) * zoom
                );
            }
            else
            {
                // No nodes - center on origin (where new nodes will be placed)
                _context.ZoomFactor = 1f;
                _context.PanOffset = new Vector2(viewWidth / 2, viewHeight / 2);
            }

            Repaint();
        }

        private void ValidateNextGraph()
        {
            if (_context.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("校验状态机图", "尚未加载状态机图。", "确定");
                return;
            }

            SetDiagnosticsVisible(true);
            RunNextDiagnostics();
            Repaint();
        }

        private void ValidateLegacyGraph()
        {
            if (_context.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("校验状态机图", "尚未加载状态机图。", "确定");
                return;
            }

            var valid = _context.GraphAsset.Validate();
            EditorUtility.DisplayDialog(
                "旧版状态机图校验",
                valid ? "状态机图校验通过。" : "状态机图存在错误，请在控制台中查看详情。",
                "确定");
        }

        private DiagnosticSnapshot RunNextDiagnostics()
        {
            if (_diagnosticsPanel == null || _context.GraphAsset == null) return null;
            return _diagnosticsPanel.Refresh();
        }

        private void ToggleDiagnostics()
        {
            var visible = _diagnosticsPane == null ||
                          _diagnosticsPane.style.display == DisplayStyle.None;
            SetDiagnosticsVisible(visible);
        }

        private void SetDiagnosticsVisible(bool visible)
        {
            if (_diagnosticsPane != null)
                _diagnosticsPane.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _userState.SetBool(DiagnosticsVisibleStateKey, visible);
        }

        private void FrameAll()
        {
            if (_context.GraphAsset == null)
                return;

            Rect bounds = _context.GetNodesBounds();
            Rect viewRect = _graphContainer.contentRect;

            float zoomX = viewRect.width / (bounds.width + 100);
            float zoomY = viewRect.height / (bounds.height + 100);
            float zoom = Mathf.Min(zoomX, zoomY, 1f);

            _context.ZoomFactor = zoom;
            _context.PanOffset = new Vector2(
                viewRect.width / 2 - (bounds.x + bounds.width / 2) * zoom,
                viewRect.height / 2 - (bounds.y + bounds.height / 2) * zoom
            );

            Repaint();
        }

        #endregion

        #region Drag and Drop

        private void DragPerform()
        {
            if (DragAndDrop.objectReferences.Length == 1 &&
                DragAndDrop.objectReferences[0] is Graph.GraphAsset graph)
            {
                LoadGraph(graph);
            }
        }

        #endregion
    }
}
