using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Main editor window for HFSM graph editing.
    /// Similar to Unity's Animator window but for hierarchical state machines.
    /// </summary>
    public class HfsmEditorWindow : EditorWindow
    {
        private HfsmEditorContext _context;
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

        [MenuItem("Window/AbilityKit/HFSM Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<HfsmEditorWindow>();
            window.titleContent = new GUIContent("HFSM Editor", EditorGUIUtility.IconContent("AnimatorStateMachine Icon").image);
            window.minSize = new Vector2(800, 600);
        }

        [MenuItem("Assets/Edit HFSM Graph", true)]
        private static bool ValidateOpenGraphAsset()
        {
            return Selection.activeObject is Graph.HfsmGraphAsset;
        }

        [MenuItem("Assets/Edit HFSM Graph")]
        private static void OpenGraphAsset()
        {
            var window = GetWindow<HfsmEditorWindow>();
            window.titleContent = new GUIContent("HFSM Editor", EditorGUIUtility.IconContent("AnimatorStateMachine Icon").image);
            window.minSize = new Vector2(800, 600);

            if (Selection.activeObject is Graph.HfsmGraphAsset graph)
            {
                window.LoadGraph(graph);
            }
        }

        private const string EditorPrefLastGraphGuid = "HfsmEditor_LastGraphGuid";
        private const string EditorPrefLastZoom = "HfsmEditor_LastZoom";
        private const string EditorPrefLastPanX = "HfsmEditor_LastPanX";
        private const string EditorPrefLastPanY = "HfsmEditor_LastPanY";

        private void OnEnable()
        {
            _context = new HfsmEditorContext();
            _context.OnContextChanged += OnContextChanged;
            _context.OnStateMachineChanged += OnStateMachineChanged;

            CreateUI();
            CreateLayers();

            // Try to restore last opened graph
            if (EditorPrefs.HasKey(EditorPrefLastZoom))
            {
                float zoom = EditorPrefs.GetFloat(EditorPrefLastZoom);
                float panX = EditorPrefs.GetFloat(EditorPrefLastPanX);
                float panY = EditorPrefs.GetFloat(EditorPrefLastPanY);
                _context.ZoomFactor = zoom;
                _context.PanOffset = new Vector2(panX, panY);
            }

            // Try to restore last opened graph asset
            RestoreLastGraph();
        }

        private void RestoreLastGraph()
        {
            if (!EditorPrefs.HasKey(EditorPrefLastGraphGuid))
                return;

            string guid = EditorPrefs.GetString(EditorPrefLastGraphGuid);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            var graph = AssetDatabase.LoadAssetAtPath<Graph.HfsmGraphAsset>(path);
            if (graph != null)
            {
                LoadGraph(graph);
            }
        }

        private void OnDisable()
        {
            // Save view state
            EditorPrefs.SetFloat(EditorPrefLastZoom, _context.ZoomFactor);
            EditorPrefs.SetFloat(EditorPrefLastPanX, _context.PanOffset.x);
            EditorPrefs.SetFloat(EditorPrefLastPanY, _context.PanOffset.y);

            // Save last opened graph
            if (_context != null && _context.GraphAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(_context.GraphAsset);
                if (!string.IsNullOrEmpty(path))
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    EditorPrefs.SetString(EditorPrefLastGraphGuid, guid);
                }
            }

            if (_context != null)
            {
                _context.OnContextChanged -= OnContextChanged;
                _context.OnStateMachineChanged -= OnStateMachineChanged;
            }
        }

        private void CreateUI()
        {
            _root = rootVisualElement;

            // Load UXML template
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.abilitykit.hfsm/Editor/Resources/HfsmEditorLayout.uxml");

            if (visualTree != null)
            {
                _root.Add(visualTree.Instantiate());
            }

            // Load stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.abilitykit.hfsm/Editor/Resources/HfsmEditorStyles.uss");

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
                _backButton.clickable = new Clickable(() => _context.NavigateBack());
                _backButton.SetEnabled(false);
            }

            // Wire up action buttons from UXML
            ToolbarButton newButton = _root.Q<ToolbarButton>("NewButton");
            if (newButton != null)
                newButton.clickable = new Clickable(() => CreateNewGraph());

            ToolbarButton loadButton = _root.Q<ToolbarButton>("LoadButton");
            if (loadButton != null)
                loadButton.clickable = new Clickable(() => LoadGraphFromPicker());

            ToolbarButton validateButton = _root.Q<ToolbarButton>("ValidateButton");
            if (validateButton != null)
                validateButton.clickable = new Clickable(() => ValidateGraph());

            ToolbarButton frameButton = _root.Q<ToolbarButton>("FrameButton");
            if (frameButton != null)
                frameButton.clickable = new Clickable(() => FrameAll());
        }

        // Panels
        private HfsmInspectorPanel _inspectorPanel;
        private HfsmParameterPanel _parameterPanel;

        private void CreateMainContent()
        {
            _root = rootVisualElement;

            // Try to find graph container from UXML with proper path
            _graphContainer = FindIMGUIContainer(_root, "graph-view");

            if (_graphContainer == null)
            {
                Debug.LogError("HFSM Editor: Could not find graph-view IMGUIContainer in UXML");
                return;
            }

            _graphContainer.onGUIHandler += OnGraphGUI;

            // Create parameter panel
            _parameterPanel = new HfsmParameterPanel();
            _parameterPanel.Initialize(_context);

            IMGUIContainer parameterContainer = FindIMGUIContainer(_root, "ParameterPanel");
            if (parameterContainer != null)
            {
                parameterContainer.onGUIHandler += () => _parameterPanel.OnGUI();
            }

            // Create inspector panel
            _inspectorPanel = CreateInstance<HfsmInspectorPanel>();
            _inspectorPanel.Initialize(_context);

            IMGUIContainer inspectorContainer = FindIMGUIContainer(_root, "InspectorPanel");
            if (inspectorContainer != null)
            {
                inspectorContainer.onGUIHandler += () => _inspectorPanel.OnGUI();
            }
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
                    $"Graph: {_context.GraphAsset.name}\n" +
                    $"Nodes: {_context.CurrentChildNodes.Count}\n" +
                    $"Pan: {_context.PanOffset}\n" +
                    $"Zoom: {_context.ZoomFactor}\n" +
                    $"ViewBounds: {_graphContainer.contentRect}",
                    labelStyle);
            }
            else
            {
                GUI.Label(messageRect, "No graph loaded.\nUse 'New' or 'Load' to create/open a graph.", labelStyle);
            }
        }

        private void DrawTransitionPreviewInfo(Rect rect)
        {
            Rect infoRect = new Rect(10, rect.height - 30, 300, 25);
            GUI.Label(infoRect, "Creating transition... Click target state or press ESC to cancel.",
                EditorStyles.miniLabel);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is Graph.HfsmGraphAsset graph)
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
                _breadcrumbLabel.text = "No graph loaded";
                return;
            }

            _breadcrumbLabel.text = $"{_context.GraphAsset.GraphName} > {_context.GetPathString()}";
        }

        #region Actions

        private void CreateNewGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New HFSM Graph",
                "New HFSM Graph",
                "asset",
                "Enter a name for the new HFSM graph");

            if (string.IsNullOrEmpty(path))
                return;

            var graph = ScriptableObject.CreateInstance<Graph.HfsmGraphAsset>();
            graph.GraphName = System.IO.Path.GetFileNameWithoutExtension(path);

            // Create root state machine
            var rootSM = graph.CreateStateMachine("Root", new Vector2(200, 100));
            graph.SetRootStateMachine(rootSM);

            // Create initial state
            var initialState = graph.CreateState("Initial", new Vector2(200, 250));
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
                "Load HFSM Graph",
                "Assets",
                "asset");

            if (string.IsNullOrEmpty(path))
                return;

            path = "Assets" + path.Replace(Application.dataPath, "").Replace("\\", "/");
            var graph = AssetDatabase.LoadAssetAtPath<Graph.HfsmGraphAsset>(path);

            if (graph != null)
            {
                LoadGraph(graph);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a valid HFSM Graph Asset.", "OK");
            }
        }

        public void LoadGraph(Graph.HfsmGraphAsset graph)
        {
            if (graph == null)
                return;

            _context.GraphAsset = graph;
            _context.Reset();
            UpdateBreadcrumb();

            // Auto-frame all nodes when loading
            FrameAll();

            Repaint();
        }

        private void ValidateGraph()
        {
            if (_context.GraphAsset == null)
            {
                EditorUtility.DisplayDialog("Validate Graph", "No graph loaded.", "OK");
                return;
            }

            if (_context.GraphAsset.Validate())
            {
                EditorUtility.DisplayDialog("Validate Graph", "Graph is valid!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validate Graph", "Graph has errors. Check console for details.", "OK");
            }
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
                DragAndDrop.objectReferences[0] is Graph.HfsmGraphAsset graph)
            {
                LoadGraph(graph);
            }
        }

        #endregion
    }
}
