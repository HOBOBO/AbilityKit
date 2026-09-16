using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 行为树图编辑器。节点目录、端口数量、属性面板全部由 <see cref="NodeRegistry"/>
    /// 描述符驱动生成——编辑器只认识描述符，新增包外节点零编辑器代码。
    /// 边方向：子 → 父（输出端口在上，输入端口在下，连线建立 ChildIds 关系）。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringGraphWindow")]
    public class AuthoringGraphWindow : EditorWindow, IAuthoringGraphHost, IAuthoringInspectorHost
    {
        internal const float MinimumWindowWidth = 900f;
        internal const float MinimumWindowHeight = 560f;
        internal const float MinimumInspectorWidth = 280f;
        internal const string PrimaryToolbarName = "bt-primary-toolbar";
        internal const string CommandToolbarName = "bt-command-toolbar";
        internal const string InspectorPaneName = "bt-inspector-pane";
        internal const string InspectorScrollName = "bt-inspector-scroll";
        internal const string PreviewFaultLabelName = "bt-preview-fault";
        internal const string PreviewStepButtonName = "bt-preview-step";
        internal const string PreviewResetButtonName = "bt-preview-reset";
        internal const float MinimumInspectorContentHeight = 140f;

        private AuthoringAsset? _asset;
        private AuthoringProjectAsset? _project;
        private readonly AuthoringWorkspaceController _workspace = new();
        private readonly AuthoringWorkspacePresenter _presenter;
        private AuthoringDocumentSession _documentSession => _workspace.Session;
        private readonly EditorCommandRegistry _commands = new();
        private readonly List<IDisposable> _commandRegistrations = new();
        private EditorDiagnosticCollection _diagnostics => _workspace.Diagnostics;
        private IEditorLocalization _localization = null!;
        private AuthoringSourceDocument _document => _workspace.Document;
        private AuthoringGraphView _graphView = null!;
        private AuthoringInspectorRenderer _inspectorRenderer = null!;
        private NodeDefinition? _selectedNode;
        private Label? _modeLabel;
        private Label? _dirtyLabel;
        private AuthoringValidationPanel? _validationPanel;
        private UnityEditor.UIElements.ToolbarSearchField? _nodeSearchField;
        private AuthoringOverviewPanel? _overviewPanel;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _observationPauseButton;
        private readonly ObservationController _observationController = new(sampleIntervalSeconds: 0.15d);
        private readonly ObservationContributorRegistry _observationContributors =
            ObservationContributorRegistry.Default;
        private ObservationSnapshot? _displayedObservationSnapshot;
        private ObservationSnapshot? _previousObservationSnapshot;
        private ObservationDiff? _displayedObservationDiff;
        private bool _isDirty => _documentSession.IsDirty;
        private ObservationSessionState _observationState = ObservationSessionState.NoSample;
        private int _observationFrame;

        /// <summary>观察模式：绑定一个运行中实例，把实时节点状态着色到画布（只读）。</summary>
        private TreeDebugView? _observedView;

        /// <summary>观察模式工具栏的实例切换下拉；与 <see cref="_instancePopupIds"/> 平行。</summary>
        private PopupField<string>? _instancePopup;
        private readonly List<long> _instancePopupIds = new();
        private string _instancePopupFingerprint = "";
        private ObservationEventTimelinePanel? _eventTimelinePanel;
        private TreePreviewSession? _previewSession;
        private Button? _previewStepButton;
        private Button? _previewResetButton;
        private bool _catalogPreview;
        private AuthoringGraphWindow? _previewOwner;
        private Label? _previewFaultLabel;
        private string _configTreeId = "";

        public AuthoringGraphWindow()
        {
            _presenter = new AuthoringWorkspacePresenter(_workspace);
        }

        internal bool IsObservation => _workspace != null && _documentSession.IsReadOnly;
        internal AuthoringDocumentSession DocumentSession => _documentSession;

        public static void Open(AuthoringAsset asset)
        {
            Open(asset, AuthoringMenuUtility.FindOwningProject(asset));
        }

        public static void Open(AuthoringAsset asset, AuthoringProjectAsset? project)
        {
            var window = Resources.FindObjectsOfTypeAll<AuthoringGraphWindow>()
                .FirstOrDefault(candidate => candidate._workspace != null && !candidate.IsObservation);
            if (window != null && ReferenceEquals(window._asset, asset))
            {
                window._project = project;
                window.Show();
                window.Focus();
                return;
            }
            if (window != null && !window.ConfirmAssetSwitch(asset)) return;
            window ??= CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent("行为树编辑器");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window.EnterEditMode(asset, project);
            window.Show();
            window.Focus();
        }

        /// <summary>以观察模式打开：从运行时实例的树定义渲染图，实时着色节点状态。</summary>
        public static void OpenObservation(TreeDebugView view)
        {
            if (view == null) return;
            var window = Resources.FindObjectsOfTypeAll<AuthoringGraphWindow>()
                .FirstOrDefault(candidate => candidate._workspace != null && ReferenceEquals(candidate._observedView, view))
                ?? CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent("行为树观察图");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window.EnterObservationMode(view);
            window.Show();
            window.Focus();
        }

        internal static void OpenCatalogPreview(AuthoringSourceDocument source)
        {
            var window = CreateWindow<AuthoringGraphWindow>();
            window._catalogPreview = true;
            window._observedView = null;
            window._workspace.State.SetDocumentScope("reference." + source.Tree.TreeId);
            var document = AuthoringJson.Load(AuthoringJson.Save(source));
            if (document.Layout.Count > 1 && document.Layout.All(item => item.X == 0f && item.Y == 0f))
                document.Layout.Clear();
            window._workspace.Open(document, isReadOnly: true);
            window._selectedNode = null;
            window.titleContent = new GUIContent("子树引用（只读）");
            window.BuildUi();
            window.RebuildGraph();
            window.Show();
            window.Focus();
        }

        public static void OpenObservation(
            ObservationSnapshot snapshot,
            TreeDefinition definition,
            ObservationSnapshot previousSnapshot = null,
            ObservationDiff diff = null)
        {
            if (snapshot == null) return;
            var window = CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent("行为树观察图");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window.EnterObservationMode(
                new ObservationSnapshotDebugView(snapshot, definition),
                snapshot,
                previousSnapshot,
                diff);
            window.Show();
            window.Focus();
        }

        internal static AuthoringGraphWindow OpenPreview(
            TreePreviewSession session,
            AuthoringGraphWindow owner,
            AuthoringSourceDocument sourceDocument)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return OpenOwnedObservation(session.Runtime, owner, sourceDocument, session);
        }

        private static AuthoringGraphWindow OpenOwnedObservation(
            TreeDebugView view,
            AuthoringGraphWindow owner,
            AuthoringSourceDocument sourceDocument = null,
            TreePreviewSession session = null)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            foreach (var existing in Resources.FindObjectsOfTypeAll<AuthoringGraphWindow>())
            {
                if (ReferenceEquals(existing._previewOwner, owner)) existing.Close();
            }

            var window = CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent(session != null ? "行为树预览" : "行为树观察图");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window._previewOwner = owner;
            window._previewSession = session;
            if (session != null) session.Faulted += window.OnPreviewFaulted;
            window.EnterObservationMode(view, sourceDocument: sourceDocument);
            window.Show();
            window.Focus();
            return window;
        }

        private void EnterEditMode(AuthoringAsset asset, AuthoringProjectAsset? project = null)
        {
            _catalogPreview = false;
            _observedView = null;
            _asset = asset;
            _project = project;
            _workspace.State.SetDocumentScope(DocumentScopeForAsset(asset));
            _workspace.Open(asset != null ? asset.LoadDocument() : new AuthoringSourceDocument());
            _selectedNode = null;
            _observationController.Reset();
            _displayedObservationSnapshot = null;
            _previousObservationSnapshot = null;
            _displayedObservationDiff = null;
            _observationState = ObservationSessionState.NoSample;
            _observationFrame = 0;
            hasUnsavedChanges = false;
            BuildUi();
            RebuildGraph();
        }

        private void EnterObservationMode(
            TreeDebugView view,
            ObservationSnapshot initialSnapshot = null,
            ObservationSnapshot previousSnapshot = null,
            ObservationDiff initialDiff = null,
            AuthoringSourceDocument sourceDocument = null)
        {
            _catalogPreview = false;
            _observedView = view;
            _configTreeId = sourceDocument?.Tree?.TreeId
                ?? (_asset != null ? _document.Tree.TreeId : "");
            _workspace.State.SetDocumentScope("observation." + (view.TreeId ?? "unknown"));
            _selectedNode = null;
            _observationController.Reset();
            _observationState = ObservationSessionState.NoSample;
            _observationFrame = view.LastFrame;
            _displayedObservationSnapshot = null;
            _previousObservationSnapshot = previousSnapshot;
            _displayedObservationDiff = initialDiff;
            if (TryBindObservationView(view))
            {
                UpdateDisplayedObservationSnapshot(
                    _observationController.Sample(),
                    _observationController.Timeline.SampleAt(_observationController.Timeline.Count - 2),
                    _observationController.Timeline.LatestDiff);
            }
            else if (initialSnapshot != null)
            {
                UpdateDisplayedObservationSnapshot(initialSnapshot, previousSnapshot, initialDiff);
            }
            hasUnsavedChanges = false;
            // 从运行时定义构造只读文档（布局为空，节点按层级自动排布）
            _workspace.Open(
                AuthoringDocumentCatalog.BuildObservationDocument(
                    view,
                    EditorNodeCatalog.Registry,
                    sourceDocument),
                isReadOnly: true);
            BuildUi();
            RebuildGraph();
        }

        private bool ConfirmAssetSwitch(AuthoringAsset nextAsset)
        {
            if (!_isDirty) return true;
            var currentName = _asset != null ? _asset.name : "当前行为树";
            var nextName = nextAsset != null ? nextAsset.name : "新行为树";
            var choice = EditorUtility.DisplayDialogComplex(
                "未保存的行为树",
                $"'{currentName}' 包含未保存修改。打开 '{nextName}' 前要如何处理？",
                "保存并打开",
                "取消",
                "放弃并打开");
            if (choice == 1) return false;
            if (choice == 0) Save();
            return true;
        }

        private void OnEnable()
        {
            // 幽灵窗口（构造函数未执行、readonly 字段为 null，FindObjectsOfTypeAll/布局恢复会造出）无可初始化状态。
            if (_commandRegistrations == null) return;
            minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            _localization = EditorLocalization.Localization;
            _localization.LanguageChanged += OnLanguageChanged;
            RegisterCommands();
            _graphView = new AuthoringGraphView(this);
            BuildUi();
            _graphView.RegisterCallback<KeyDownEvent>(OnGraphKeyDown);
            _graphView.RegisterCallback<MouseUpEvent>(_ => SchedulePersistViewport());
            _graphView.RegisterCallback<WheelEvent>(_ => SchedulePersistViewport());

            // 观察模式的实时状态着色；静态 authoring 不再按固定频率轮询选择或文档。
            rootVisualElement.schedule.Execute(ObservationTick).Every(150);
        }

        private void OnDisable()
        {
            StopPreview();
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
            if (_commandRegistrations == null) return;
            foreach (var registration in _commandRegistrations)
                registration.Dispose();
            _commandRegistrations.Clear();
        }

        private void OnLanguageChanged()
        {
            BuildUi();
            RebuildGraph();
        }

        private void RegisterCommands()
        {
            if (_commandRegistrations.Count > 0) return;
            var commands = EditorCommandFactory.Create(
                Close,
                ToggleObservationPause,
                CopyObservationSnapshot,
                Save,
                ExportRuntime,
                PerformUndo,
                PerformRedo,
                AddRoot,
                AddGroupFromSelection,
                AddCanvasNote,
                AutoLayout,
                FrameAll,
                ValidateOnGraph,
                () => IsObservation,
                () => _documentSession.CanUndo,
                () => _documentSession.CanRedo);
            foreach (var command in commands)
                _commandRegistrations.Add(_commands.Register(command));
        }

        private bool ExecuteCommand(string id)
        {
            var executed = _commands.Execute(id, new EditorCommandContext(this, _selectedNode));
            RefreshChrome();
            SchedulePersistViewport();
            return executed;
        }

        private static string DocumentScopeForAsset(AuthoringAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            var guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrWhiteSpace(guid)
                ? "asset." + guid
                : "asset.instance." + asset.GetInstanceID();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();

            var header = new UnityEditor.UIElements.Toolbar();
            header.name = PrimaryToolbarName;
            header.style.minHeight = 30f;
            header.style.paddingLeft = 4f;
            header.style.paddingRight = 4f;
            if (_catalogPreview)
            {
                header.Add(new Label("子树引用（只读）")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 8f },
                });
            }
            else
            {
                header.Add(ModeToggleButton("编辑", !IsObservation, () =>
                {
                    if (IsObservation) BackToEdit();
                }));
                header.Add(ModeToggleButton("调试", IsObservation, () =>
                {
                    if (!IsObservation) EnterDebugMode();
                }));
                _modeLabel = new Label(L(IsObservation
                    ? "abilitykit.behaviortree.mode.observation"
                    : "abilitykit.behaviortree.mode.edit"))
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        minWidth = 150f,
                        marginLeft = 10f,
                        marginRight = 8f,
                        unityTextAlign = TextAnchor.MiddleLeft,
                    },
                };
                header.Add(_modeLabel);
            }
            if (!IsObservation)
            {
                _dirtyLabel = new Label
                {
                    style =
                    {
                        minWidth = 58f,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 10f,
                    },
                };
                header.Add(_dirtyLabel);
            }
            header.Add(new VisualElement { style = { flexGrow = 1f } });

            _nodeSearchField = new UnityEditor.UIElements.ToolbarSearchField
            {
                tooltip = L("abilitykit.behaviortree.search.tooltip"),
            };
            _nodeSearchField.SetValueWithoutNotify(_workspace.State.NodeSearch);
            _nodeSearchField.style.width = 220f;
            _nodeSearchField.style.minWidth = 140f;
            _nodeSearchField.style.marginRight = 4f;
            _nodeSearchField.RegisterValueChangedCallback(evt =>
            {
                _workspace.State.NodeSearch = evt.newValue ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(evt.newValue))
                    _graphView.FocusFirstMatch(evt.newValue);
                RefreshOverview();
            });
            header.Add(_nodeSearchField);
            rootVisualElement.Add(header);

            var actions = new UnityEditor.UIElements.Toolbar();
            actions.name = CommandToolbarName;
            actions.style.minHeight = 28f;
            actions.style.paddingLeft = 4f;
            actions.style.paddingRight = 4f;
            if (_catalogPreview)
            {
                actions.Add(new Button(Close) { text = "返回" });
                actions.Add(CommandButton(EditorCommandIds.FrameAll, "frame-all"));
            }
            else if (IsObservation)
            {
                actions.Add(CommandButton(EditorCommandIds.Close, "close"));
                _observationPauseButton = CommandButton(EditorCommandIds.PauseObservation, "pause");
                actions.Add(_observationPauseButton);
                if (_previewSession != null)
                {
                    _previewStepButton = new Button(StepPreview)
                    {
                        name = PreviewStepButtonName,
                        text = "单步",
                        tooltip = "暂停后推进一个逻辑帧",
                    };
                    _previewResetButton = new Button(ResetPreview)
                    {
                        name = PreviewResetButtonName,
                        text = "重置",
                        tooltip = "恢复本次预览的初始运行状态",
                    };
                    _previewStepButton.style.height = 22f;
                    _previewResetButton.style.height = 22f;
                    actions.Add(_previewStepButton);
                    actions.Add(_previewResetButton);
                    RefreshPreviewControls();
                }
                actions.Add(CommandButton(EditorCommandIds.CopySnapshot, "copy-snapshot"));
                actions.Add(ToolbarSeparator());
                _instancePopup = new PopupField<string>
                {
                    tooltip = _previewSession != null
                        ? "当前预览实例"
                        : "切换到其它运行中的行为树实例",
                };
                _instancePopup.style.width = 230f;
                _instancePopup.RegisterValueChangedCallback(evt => OnInstancePopupChanged(evt.newValue));
                actions.Add(_instancePopup);
                actions.Add(ToolbarSeparator());
                if (_observedView?.SubtreeInstances?.Count > 0)
                {
                    actions.Add(SubtreePreviewMenu());
                    actions.Add(ToolbarSeparator());
                }
                actions.Add(CommandButton(EditorCommandIds.FrameAll, "frame-all"));
            }
            else
            {
                actions.Add(CommandButton(EditorCommandIds.Save, "save"));
                actions.Add(CommandButton(EditorCommandIds.Export, "export"));
                var previewButton = new Button(StartPreview) { text = "预览" };
                previewButton.style.height = 22f;
                previewButton.style.marginLeft = 1f;
                previewButton.style.marginRight = 1f;
                actions.Add(previewButton);
                actions.Add(ToolbarSeparator());
                _undoButton = CommandButton(EditorCommandIds.Undo, "undo");
                _redoButton = CommandButton(EditorCommandIds.Redo, "redo");
                actions.Add(_undoButton);
                actions.Add(_redoButton);
                actions.Add(ToolbarSeparator());
                actions.Add(CommandButton(EditorCommandIds.AddRoot, "add-root"));
                actions.Add(CommandButton(EditorCommandIds.Group, "group"));
                actions.Add(CommandButton(EditorCommandIds.Note, "note"));
                actions.Add(ToolbarSeparator());
                actions.Add(CommandButton(EditorCommandIds.AutoLayout, "auto-layout"));
                actions.Add(LayoutMenu());
                actions.Add(CommandButton(EditorCommandIds.FrameAll, "frame-all"));
                actions.Add(ToolbarSeparator());
                actions.Add(CommandButton(EditorCommandIds.Validate, "validate"));
            }
            actions.Add(new VisualElement { style = { flexGrow = 1f } });
            rootVisualElement.Add(actions);

            if (_previewSession != null)
            {
                _previewFaultLabel = new Label
                {
                    name = PreviewFaultLabelName,
                    style =
                    {
                        display = _previewSession.IsFaulted ? DisplayStyle.Flex : DisplayStyle.None,
                        minHeight = 30f,
                        paddingLeft = 10f,
                        paddingRight = 10f,
                        paddingTop = 6f,
                        paddingBottom = 6f,
                        whiteSpace = WhiteSpace.Normal,
                        backgroundColor = new Color(0.42f, 0.12f, 0.1f, 0.92f),
                        color = new Color(1f, 0.86f, 0.82f),
                    },
                };
                UpdatePreviewFaultLabel();
                rootVisualElement.Add(_previewFaultLabel);
            }

            // Keep the inspector at a readable width while allowing the graph canvas to use the remaining space.
            var split = new TwoPaneSplitView(1, _workspace.State.InspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            split.Add(_graphView);

            var rightPane = new VisualElement();
            rightPane.name = InspectorPaneName;
            rightPane.style.flexGrow = 1f;
            rightPane.style.minWidth = MinimumInspectorWidth;
            rightPane.style.overflow = Overflow.Hidden;
            rightPane.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.45f);
            rightPane.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width >= 240f)
                    _workspace.State.InspectorWidth = evt.newRect.width;
            });
            _overviewPanel = new AuthoringOverviewPanel(
                _presenter,
                _workspace.State,
                nodeId => _graphView.FocusNode(nodeId),
                AutoLayoutAll,
                AutoLayoutWithSelectedFixed);
            rightPane.Add(_overviewPanel.Root);
            var inspectorScroll = new ScrollView();
            inspectorScroll.name = InspectorScrollName;
            inspectorScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspectorScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            inspectorScroll.style.flexGrow = 1f;
            inspectorScroll.style.flexShrink = 1f;
            inspectorScroll.style.minHeight = MinimumInspectorContentHeight;
            inspectorScroll.style.paddingLeft = 10f;
            inspectorScroll.style.paddingRight = 10f;
            inspectorScroll.style.paddingTop = 8f;
            inspectorScroll.style.paddingBottom = 10f;
            _inspectorRenderer = new AuthoringInspectorRenderer(inspectorScroll, this);
            rightPane.Add(inspectorScroll);
            if (IsObservation && !_catalogPreview)
            {
                _eventTimelinePanel = new ObservationEventTimelinePanel(
                    _observationController,
                    _observationContributors);
                rightPane.Add(_eventTimelinePanel);
            }
            _validationPanel = new AuthoringValidationPanel(
                _workspace.State.GetPanelVisible("validation", false),
                _localization,
                ids => _graphView.MarkErrorNodes(ids),
                _graphView.ClearErrorNodes);
            rightPane.Add(_validationPanel);
            split.Add(rightPane);
            rootVisualElement.Add(split);
            RefreshChrome();
            RefreshInstancePopup();
        }

        private Button CommandButton(string commandId, string keySuffix)
        {
            var button = new Button(() => ExecuteCommand(commandId))
            {
                text = L("abilitykit.behaviortree.command." + keySuffix),
                tooltip = L("abilitykit.behaviortree.command." + keySuffix + ".tooltip")
            };
            button.style.height = 22f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            if (_commands.TryGet(commandId, out var command))
                button.SetEnabled(command.CanExecute(new EditorCommandContext(this, _selectedNode)));
            return button;
        }

        private Button ModeToggleButton(string label, bool active, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.style.height = 22f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            button.style.backgroundColor = active ? new Color(0.32f, 0.55f, 0.82f) : new Color(0f, 0f, 0f, 0f);
            return button;
        }

        private UnityEditor.UIElements.ToolbarMenu LayoutMenu()
        {
            var menu = new UnityEditor.UIElements.ToolbarMenu
            {
                text = "布局",
                tooltip = "选择自动布局的作用范围",
            };
            menu.style.height = 22f;
            menu.style.marginLeft = 1f;
            menu.style.marginRight = 1f;
            menu.menu.AppendAction(
                "全部节点",
                _ => AutoLayoutAll(),
                _ => IsObservation || _document.Tree.Nodes.Count == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "选中节点",
                _ => AutoLayoutSelectedNodes(),
                _ => IsObservation || _graphView.GetSelectedNodeIds().Count < 2
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "选中子树",
                _ => AutoLayoutSelectedSubtree(),
                _ => IsObservation || _selectedNode == null
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "全部节点（固定选中项）",
                _ => AutoLayoutWithSelectedFixed(),
                _ => IsObservation || _graphView.GetSelectedNodeIds().Count == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            return menu;
        }

        private UnityEditor.UIElements.ToolbarMenu SubtreePreviewMenu()
        {
            var menu = new UnityEditor.UIElements.ToolbarMenu
            {
                text = "子树",
                tooltip = "按引用实例折叠或展开预览节点",
            };
            menu.style.height = 22f;
            foreach (var instances in _observedView!.SubtreeInstances.GroupBy(
                instance => instance.InlinedRootNodeId, StringComparer.Ordinal))
            {
                var rootId = instances.Key;
                menu.menu.AppendAction(
                    string.Join(" → ", instances.Select(instance => instance.ReferencedTreeId))
                    + "  ·  " + rootId,
                    _ =>
                    {
                        _graphView.SetSubtreeCollapsed(rootId, !_graphView.IsSubtreeCollapsed(rootId));
                        _graphView.FocusNode(rootId);
                    },
                    _ => _graphView.IsSubtreeCollapsed(rootId)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            return menu;
        }

        private string L(string key) => _localization.Get(key);

        private static VisualElement ToolbarSeparator()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1f,
                    height = 16f,
                    marginLeft = 5f,
                    marginRight = 5f,
                    backgroundColor = new Color(0.33f, 0.33f, 0.33f),
                },
            };
        }

        private void FrameAll()
        {
            _graphView.FrameAll();
            SchedulePersistViewport();
        }

        private void RestorePersistedViewport()
        {
            if (!_workspace.State.TryGetViewport(out var viewport)) return;
            _graphView.UpdateViewTransform(
                new Vector3(viewport.X, viewport.Y, 0f),
                new Vector3(viewport.Scale, viewport.Scale, 1f));
        }

        private void SchedulePersistViewport()
        {
            if (_graphView == null) return;
            rootVisualElement.schedule.Execute(PersistViewport).ExecuteLater(30);
        }

        private void PersistViewport()
        {
            if (_graphView == null) return;
            var transform = _graphView.viewTransform;
            _workspace.State.SetViewport(transform.position.x, transform.position.y, transform.scale.x);
        }

        private void RefreshOverview()
        {
            _overviewPanel?.Refresh(_workspace.State.NodeSearch);
        }

        // ------------------------------------------------------------------
        // 撤销/重做：文档 JSON 快照栈（图操作前压栈；Ctrl+Z / Ctrl+Y）
        // ------------------------------------------------------------------

        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            if (evt == null || !evt.ctrlKey) return;
            if (evt.keyCode == UnityEngine.KeyCode.F)
            {
                _nodeSearchField?.Focus();
                evt.StopPropagation();
            }
            else if (IsObservation)
            {
                return;
            }
            else if (evt.keyCode == UnityEngine.KeyCode.S)
            {
                ExecuteCommand(EditorCommandIds.Save);
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.E && evt.shiftKey)
            {
                ExecuteCommand(EditorCommandIds.Export);
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.L)
            {
                ExecuteCommand(EditorCommandIds.AutoLayout);
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.Z)
            {
                ExecuteCommand(EditorCommandIds.Undo);
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.Y)
            {
                ExecuteCommand(EditorCommandIds.Redo);
                evt.StopPropagation();
            }
        }

        private void PushUndo()
        {
            if (_workspace.RecordExternalMutation()) RefreshChrome();
        }

        private void PushUndoSnapshot(string snapshot)
        {
            if (_workspace.RecordExternalMutation(snapshot)) RefreshChrome();
        }

        private void PerformUndo()
        {
            try
            {
                if (!_workspace.Undo()) return;
                _selectedNode = null;
                RebuildGraph();
                RefreshChrome();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BtAuthoring] 撤销快照损坏，已放弃: " + ex.Message);
            }
        }

        private void PerformRedo()
        {
            try
            {
                if (!_workspace.Redo()) return;
                _selectedNode = null;
                RebuildGraph();
                RefreshChrome();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BtAuthoring] 重做快照损坏，已放弃: " + ex.Message);
            }
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (_workspace.DiscardChanges())
            {
                _selectedNode = null;
                RebuildGraph();
                RefreshChrome();
            }
            base.DiscardChanges();
        }

        private void RefreshChrome()
        {
            if (_modeLabel != null && !IsObservation)
                _modeLabel.text = string.IsNullOrWhiteSpace(_document.Tree.TreeId)
                    ? "行为树"
                    : _document.Tree.TreeId;
            if (_dirtyLabel != null)
            {
                _dirtyLabel.text = L(_isDirty
                    ? "abilitykit.behaviortree.state.dirty"
                    : "abilitykit.behaviortree.state.saved");
                _dirtyLabel.style.color = _isDirty
                    ? new Color(1f, 0.72f, 0.28f)
                    : new Color(0.5f, 0.82f, 0.58f);
                _dirtyLabel.style.opacity = _isDirty ? 1f : 0.8f;
            }
            hasUnsavedChanges = _documentSession.IsDirty;
            _undoButton?.SetEnabled(_documentSession.CanUndo);
            _redoButton?.SetEnabled(_documentSession.CanRedo);
            titleContent = new GUIContent(_catalogPreview
                ? "子树引用（只读）"
                : _previewSession != null
                ? (_previewSession.IsFaulted ? "行为树预览（已停止）" : "行为树预览")
                : IsObservation
                ? "行为树观察"
                : (_isDirty ? "行为树编辑器 *" : "行为树编辑器"));
            RefreshOverview();
        }

        /// <summary>图上校验：分析文档并把结构化诊断交给校验面板渲染。</summary>
        private void ValidateOnGraph()
        {
            _diagnostics.Replace(EditorDiagnostics.Analyze(
                _document,
                EditorNodeCatalog.Registry,
                AuthoringDocumentCatalog.CreateTreeResolver(_document),
                nodeId => _graphView.FocusNode(nodeId)).Items);
            if (_validationPanel == null) return;
            _workspace.State.SetPanelVisible("validation", true);
            _validationPanel.Render(_diagnostics);
        }

        private void ToggleObservationPause()
        {
            if (_previewSession != null)
            {
                if (_previewSession.IsPaused)
                {
                    _previewSession.Resume();
                    _observationController.Resume();
                }
                else
                {
                    _previewSession.Pause();
                    _observationController.Pause();
                }
                RefreshPreviewControls();
            }
            else if (_observationController.Paused) _observationController.Resume();
            else _observationController.Pause();
            if (_observationPauseButton != null)
                _observationPauseButton.text = L(_observationController.Paused
                    ? "abilitykit.behaviortree.command.resume"
                    : "abilitykit.behaviortree.command.pause");
            RefreshObservationModeLabel();
        }

        private void RefreshPreviewControls()
        {
            if (_previewSession == null) return;
            _observationPauseButton?.SetEnabled(!_previewSession.IsFaulted);
            _previewStepButton?.SetEnabled(_previewSession.IsPaused && !_previewSession.IsFaulted);
            _previewResetButton?.SetEnabled(true);
            if (_observationPauseButton != null)
                _observationPauseButton.text = L(_previewSession.IsPaused
                    ? "abilitykit.behaviortree.command.resume"
                    : "abilitykit.behaviortree.command.pause");
        }

        private void StepPreview()
        {
            if (_previewSession == null || !_previewSession.IsPaused) return;
            _previewSession.Step();
            SamplePreviewNow();
            RefreshPreviewControls();
        }

        private void ResetPreview()
        {
            if (_previewSession == null) return;
            if (!_previewSession.TryReset(out var error))
            {
                EditorUtility.DisplayDialog("预览重置失败", error ?? "未知错误", "确定");
                RefreshPreviewControls();
                return;
            }
            _observationController.ClearHistory();
            _displayedObservationSnapshot = null;
            _previousObservationSnapshot = null;
            _displayedObservationDiff = null;
            _graphView.ClearNodeStates();
            UpdatePreviewFaultLabel();
            SamplePreviewNow();
            RefreshPreviewControls();
            titleContent = new GUIContent("行为树预览");
        }

        private void SamplePreviewNow()
        {
            if (_previewSession == null) return;
            if (_observationController.SelectedInstanceId == 0)
                TryBindObservationView(_previewSession.Runtime);
            var snapshot = _observationController.Sample();
            UpdateDisplayedObservationSnapshot(
                snapshot,
                _observationController.Timeline.SampleAt(_observationController.Timeline.Count - 2),
                _observationController.Timeline.LatestDiff);
            if (_displayedObservationSnapshot != null)
            {
                _graphView.ApplyObservationProjection(
                    _displayedObservationSnapshot,
                    _displayedObservationDiff,
                    _observationContributors);
                _inspectorRenderer.RefreshRuntimeDetails();
            }
            _observationState = _observationController.State;
            RefreshObservationModeLabel();
            _eventTimelinePanel?.Refresh();
        }

        private void CopyObservationSnapshot()
        {
            if (_displayedObservationSnapshot == null) return;
            try
            {
                EditorGUIUtility.systemCopyBuffer =
                    ObservationEditorTransport.CreateRuntimeSnapshotJson(_displayedObservationSnapshot);
                ShowNotification(new GUIContent(L(
                    "abilitykit.behaviortree.observation.snapshot-copied")));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BtObservation] 无法复制运行快照: " + ex.Message);
                ShowNotification(new GUIContent(L(
                    "abilitykit.behaviortree.observation.snapshot-failed")));
            }
        }

        private void ObservationTick()
        {
            if (_observedView == null || _graphView == null) return;

            if (_observationController.SelectedInstanceId == 0)
            {
                TryBindObservationView(_observedView);
            }

            _observationController.Poll(EditorApplication.timeSinceStartup, autoSelectFirst: false);
            _observationState = _observationController.State;
            UpdateDisplayedObservationSnapshot(
                _observationController.Latest,
                _observationController.Timeline.SampleAt(_observationController.Timeline.Count - 2),
                _observationController.Timeline.LatestDiff);

            if (_displayedObservationSnapshot != null)
            {
                _graphView.ApplyObservationProjection(
                    _displayedObservationSnapshot,
                    _displayedObservationDiff,
                    _observationContributors);
                _inspectorRenderer.RefreshRuntimeDetails();
            }
            else
            {
                _graphView.ClearNodeStates();
            }

            RefreshObservationModeLabel();
            RefreshInstancePopup();
            _eventTimelinePanel?.Refresh();
        }

        private bool TryBindObservationView(TreeDebugView view)
        {
            foreach (var entry in DebugRegistry.GetEntries())
            {
                if (!ReferenceEquals(entry.View, view)) continue;
                return _observationController.SelectInstance(entry.Id);
            }
            return false;
        }

        private void UpdateDisplayedObservationSnapshot(
            ObservationSnapshot? snapshot,
            ObservationSnapshot? previousSnapshot,
            ObservationDiff? diff)
        {
            if (snapshot == null) return;
            _displayedObservationSnapshot = snapshot;
            _previousObservationSnapshot = previousSnapshot;
            _displayedObservationDiff = diff;
            _observationFrame = snapshot.Frame;
        }

        private void RefreshObservationModeLabel()
        {
            if (_modeLabel == null) return;
            if (_previewSession != null)
            {
                _modeLabel.text = _previewSession.IsFaulted
                    ? "预览已停止 · 帧 " + _previewSession.Frame
                    : "预览 · 帧 " + _previewSession.Frame
                      + (_previewSession.IsPaused ? " · 已暂停" : "");
                return;
            }
            if (_displayedObservationSnapshot == null)
            {
                _modeLabel.text = L("abilitykit.behaviortree.mode.observation");
                return;
            }
            if (_observationState == ObservationSessionState.Disconnected)
            {
                _modeLabel.text = _localization.Format(
                    "abilitykit.behaviortree.observation.frame-disconnected",
                    _observationFrame);
                return;
            }
            if (_modeLabel != null)
                _modeLabel.text = _localization.Format(
                    _observationController.Paused
                        ? "abilitykit.behaviortree.observation.frame-frozen"
                        : "abilitykit.behaviortree.observation.frame",
                    _observationFrame);
        }

        private void RefreshInstancePopup()
        {
            if (_instancePopup == null) return;

            var entries = _observationController.Entries;
            var fingerprint = "";
            foreach (var entry in entries) fingerprint += entry.Id + ",";
            fingerprint += "|" + _observationController.SelectedInstanceId;
            if (fingerprint == _instancePopupFingerprint) return;
            _instancePopupFingerprint = fingerprint;

            _instancePopupIds.Clear();
            var labels = new List<string>();
            var selectedIndex = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                var view = entries[i].View;
                if (view == null) continue;
                if (_previewSession != null && !ReferenceEquals(view, _previewSession.Runtime)) continue;
                if (!string.IsNullOrEmpty(_configTreeId)
                    && !string.Equals(view.TreeId, _configTreeId, StringComparison.Ordinal))
                    continue;
                var label = "#" + entries[i].Id + " · " + view.DisplayName
                            + (string.IsNullOrEmpty(view.OwnerLabel) ? "" : " · " + view.OwnerLabel)
                            + "  [" + view.TreeId + "]";
                labels.Add(label);
                _instancePopupIds.Add(entries[i].Id);
                if (entries[i].Id == _observationController.SelectedInstanceId)
                    selectedIndex = _instancePopupIds.Count - 1;
            }

            if (labels.Count == 0)
            {
                labels.Add(string.IsNullOrEmpty(_configTreeId)
                    ? "(无运行中的实例)"
                    : "(没有使用当前配置的实例)");
                _instancePopupIds.Add(0);
                selectedIndex = 0;
            }

            _instancePopup.choices = labels;
            _instancePopup.SetValueWithoutNotify(selectedIndex >= 0 ? labels[selectedIndex] : labels[0]);
        }

        private void OnInstancePopupChanged(string newValue)
        {
            if (_instancePopup == null) return;
            var index = _instancePopup.choices != null ? _instancePopup.choices.IndexOf(newValue) : -1;
            if (index < 0 || index >= _instancePopupIds.Count) return;
            var id = _instancePopupIds[index];
            if (id == 0 || id == _observationController.SelectedInstanceId) return;
            if (!_observationController.SelectInstance(id)) return;

            _displayedObservationSnapshot = null;
            _previousObservationSnapshot = null;
            _displayedObservationDiff = null;
            _selectedNode = null;
            _graphView?.ClearNodeStates();
            _inspectorRenderer?.Render(null);
            RefreshObservationModeLabel();
        }

        void IAuthoringGraphHost.OnGraphSelectionChanged(NodeDefinition? selected)
        {
            if (ReferenceEquals(selected, _selectedNode)) return;
            _selectedNode = selected;
            _workspace.SetSelection(selected?.Id);
            _inspectorRenderer?.Render(_selectedNode);
            RefreshOverview();
        }

        private void RebuildGraph()
        {
            if (_graphView == null) return;
            AuthoringLayoutUtility.EnsureLayout(_document);
            _graphView.ClearAll();
            foreach (var node in _document.Tree.Nodes)
            {
                _graphView.AddNodeView(node);
            }
            foreach (var node in _document.Tree.Nodes)
            {
                foreach (var childId in node.ChildIds)
                {
                    _graphView.Connect(childId, node.Id);
                }
            }
            _graphView.AddGroups(_document.Groups);
            _graphView.AddNotes(_document.Notes);
            _inspectorRenderer.Render(_selectedNode);
            RestorePersistedViewport();
            RefreshOverview();
        }

        private void AutoLayout()
        {
            if (IsObservation || _document.Tree.Nodes.Count == 0) return;
            var selectedNodeIds = _graphView.GetSelectedNodeIds();
            if (selectedNodeIds.Count > 1)
            {
                AutoLayoutSelectedNodes(selectedNodeIds);
                return;
            }

            AutoLayoutAll();
        }

        private void AutoLayoutAll()
        {
            if (IsObservation || _document.Tree.Nodes.Count == 0) return;
            ApplyAutoLayout(AuthoringLayoutOptions.Full, frameAll: true);
        }

        private void AutoLayoutSelectedNodes()
        {
            AutoLayoutSelectedNodes(_graphView.GetSelectedNodeIds());
        }

        private void AutoLayoutSelectedNodes(IReadOnlyCollection<string> selectedNodeIds)
        {
            if (IsObservation || selectedNodeIds == null || selectedNodeIds.Count < 2) return;
            ApplyAutoLayout(
                CreateSelectionLayoutOptions(_document, selectedNodeIds),
                frameAll: false);
        }

        internal static AuthoringLayoutOptions CreateSelectionLayoutOptions(
            AuthoringSourceDocument document,
            IReadOnlyCollection<string> selectedNodeIds)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (selectedNodeIds == null) throw new ArgumentNullException(nameof(selectedNodeIds));

            var ids = selectedNodeIds
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            var originX = AuthoringLayoutOptions.DefaultOriginX;
            var originY = AuthoringLayoutOptions.DefaultOriginY;
            var hasExistingPosition = false;
            foreach (var layout in document.Layout)
            {
                if (!selected.Contains(layout.NodeId)) continue;
                if (!hasExistingPosition)
                {
                    originX = layout.X;
                    originY = layout.Y;
                    hasExistingPosition = true;
                    continue;
                }

                originX = Math.Min(originX, layout.X);
                originY = Math.Min(originY, layout.Y);
            }

            return new AuthoringLayoutOptions
            {
                LayoutNodeIds = ids,
                OriginX = originX,
                OriginY = originY,
                PreserveUnscopedNodesAsObstacles = true,
            };
        }

        private void AutoLayoutSelectedSubtree()
        {
            if (IsObservation || _selectedNode == null) return;
            ApplyAutoLayout(AuthoringLayoutOptions.Subtree(_selectedNode.Id), frameAll: false);
            _graphView.FocusNode(_selectedNode.Id);
        }

        private void AutoLayoutWithSelectedFixed()
        {
            if (IsObservation || _document.Tree.Nodes.Count == 0) return;
            var fixedIds = _graphView.GetSelectedNodeIds();
            if (fixedIds.Count == 0) return;
            ApplyAutoLayout(new AuthoringLayoutOptions
            {
                FixedNodeIds = fixedIds,
            }, frameAll: true);
        }

        private void ApplyAutoLayout(AuthoringLayoutOptions options, bool frameAll)
        {
            var nodeSizes = _graphView.CaptureNodeSizesForLayout();
            if (!_presenter.ApplyLayout(options, nodeSizes, out var result)) return;
            var updatedGroups = _document.Groups
                .Where(group => result.UpdatedGroupIds.Contains(group.Id))
                .ToList();
            if (!_graphView.TryApplyLayoutResult(result, updatedGroups))
            {
                RebuildGraph();
            }
            else
            {
                RefreshChrome();
            }

            if (frameAll)
            {
                _selectedNode = null;
                rootVisualElement.schedule.Execute(FrameAll).ExecuteLater(30);
            }
        }

        private void AddCanvasNote()
        {
            if (IsObservation) return;
            PushUndo();
            var center = _graphView.GetViewportCenter();
            var note = new AuthoringNoteData
            {
                Id = "note-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Text = "在此输入说明...",
                X = center.x - 120f,
                Y = center.y - 70f,
                Width = 240f,
                Height = 140f,
            };
            _document.Notes.Add(note);
            _graphView.AddNote(note);
            RefreshChrome();
        }

        private void Save()
        {
            if (_asset == null) return;
            _asset.SaveDocument(_document);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            _workspace.MarkSaved();
            RefreshChrome();
            Debug.Log("[BtAuthoring] 已保存。");
        }

        private void ExportRuntime()
        {
            if (_asset == null) return;
            Save();

            if (_project != null && _project.Trees.Contains(_asset))
            {
                var projectReport = _project.ExportTree(_asset, AuthoringMenuUtility.RepositoryRoot);
                AssetDatabase.Refresh();
                var projectErrors = projectReport
                    .Where(entry => entry.Status == ExportStatus.Error)
                    .ToList();
                var projectMessage = projectErrors.Count == 0
                    ? string.Join("\n", projectReport.Select(entry =>
                        $"{EditorDisplayText.ExportStatus(entry.Status)}：{entry.TreeId} -> {entry.Target}"))
                    : string.Join("\n", projectErrors.Select(entry => entry.Message));
                EditorUtility.DisplayDialog(
                    projectErrors.Count == 0 ? "运行时配置导出" : "运行时配置导出失败",
                    projectMessage,
                    "确定");
                return;
            }

            var report = AuthoringRuntimeExporter.Export(_asset);
            var outputs = report.Artifacts.Select(artifact => artifact.Path);
            var successMessage = report.ExportedCount > 0
                ? "已导出：\n" + string.Join("\n", outputs)
                : "内容未变化：\n" + string.Join("\n", outputs);
            EditorUtility.DisplayDialog(
                report.Success ? "运行时配置导出" : "运行时配置导出失败",
                report.Success
                    ? successMessage
                    : string.Join("\n", report.Messages),
                "确定");
        }

        /// <summary>把当前编辑中的文档编译为无头预览实例，并在独立观察窗口中显示。</summary>
        private void StartPreview()
        {
            if (IsObservation) return;

            var sourceSnapshot = AuthoringJson.Load(AuthoringJson.Save(_document));
            var debugName = "预览：" + (_asset != null ? _asset.name : (_document.Tree.TreeId ?? "行为树"));
            var resolver = AuthoringDocumentCatalog.CreateTreeResolver(sourceSnapshot);
            var build = BehaviorTreeBuildPipeline.Build(sourceSnapshot, EditorNodeCatalog.Registry, resolver);
            if (!build.Success || build.CompiledDefinition == null)
            {
                EditorUtility.DisplayDialog("预览失败", string.Join("\n",
                    build.Diagnostics.Select(diagnostic =>
                        "[" + diagnostic.Code + "] " + diagnostic.Message)), "确定");
                return;
            }
            if (build.CompiledDefinition.Blackboard.Keys.Count == 0)
                LaunchPreview(sourceSnapshot, resolver, debugName, null);
            else
                PreviewBlackboardSetupWindow.Open(this,
                    build.CompiledDefinition.Blackboard,
                    overrides => LaunchPreview(sourceSnapshot, resolver, debugName, overrides));
        }

        private void LaunchPreview(
            AuthoringSourceDocument sourceSnapshot,
            TreeDefinitionResolver resolver,
            string debugName,
            IReadOnlyDictionary<string, PropertyValue>? initialOverrides)
        {
            if (!TreePreviewSession.TryStart(
                    sourceSnapshot,
                    EditorNodeCatalog.Registry,
                    resolver,
                    debugName,
                    initialOverrides,
                    out var session,
                    out var error))
            {
                EditorUtility.DisplayDialog("预览失败", error ?? "未知错误", "确定");
                return;
            }

            try
            {
                OpenPreview(session!, this, sourceSnapshot);
            }
            catch
            {
                session?.Dispose();
                throw;
            }
        }

        /// <summary>预览窗口关闭后聚焦原编辑窗口；普通观察窗口则回到对应资产。</summary>
        private void BackToEdit()
        {
            if (_previewOwner != null)
            {
                var owner = _previewOwner;
                Close();
                if (owner != null)
                {
                    owner.Show();
                    owner.Focus();
                }
                return;
            }

            if (_asset == null)
            {
                Close();
                return;
            }

            var asset = _asset;
            var project = _project;
            StopPreview();
            EnterEditMode(asset, project);
        }

        private void StopPreview()
        {
            if (_previewSession != null)
            {
                _previewSession.Faulted -= OnPreviewFaulted;
                _previewSession.Dispose();
                _previewSession = null;
            }
            _previewOwner = null;
            _previewFaultLabel = null;
            _previewStepButton = null;
            _previewResetButton = null;
        }

        private void OnPreviewFaulted(string message)
        {
            UpdatePreviewFaultLabel();
            RefreshPreviewControls();
            RefreshObservationModeLabel();
            titleContent = new GUIContent("行为树预览（已停止）");
            Repaint();
        }

        private void UpdatePreviewFaultLabel()
        {
            if (_previewFaultLabel == null || _previewSession == null) return;
            _previewFaultLabel.text = _previewSession.IsFaulted
                ? "预览已因运行时异常停止：" + (_previewSession.FaultMessage ?? "未知错误")
                : string.Empty;
            _previewFaultLabel.style.display = _previewSession.IsFaulted
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>进入调试（观察）模式：优先观察正在使用当前配置的运行时实例，否则启动无头预览。</summary>
        private void EnterDebugMode()
        {
            if (IsObservation) return;
            var configTreeId = _document.Tree.TreeId;
            TreeDebugView? target = null;
            if (!string.IsNullOrEmpty(configTreeId))
            {
                foreach (var entry in DebugRegistry.GetEntries())
                {
                    if (entry.View != null && string.Equals(entry.View.TreeId, configTreeId, StringComparison.Ordinal))
                    {
                        target = entry.View;
                        break;
                    }
                }
            }
            if (target != null) OpenOwnedObservation(target, this);
            else StartPreview();
        }

        private void AddRoot()
        {
            if (IsObservation) return;
            if (_document.Tree.Nodes.Any(n => string.Equals(n.Id, _document.Tree.RootNodeId, StringComparison.Ordinal)))
            {
                Debug.LogWarning("[BtAuthoring] 已存在根节点。请选中其他节点并使用“设为根节点”。");
                return;
            }

            PushUndo();
            var id = NewNodeId();
            var node = new NodeDefinition { Id = id, Type = BuiltInNodeTypes.Succeed };
            _document.Tree.Nodes.Add(node);
            _document.Tree.RootNodeId = id;
            _document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = id, DisplayName = "根节点" });
            _document.Layout.Add(new NodeLayoutData { NodeId = id, X = 400, Y = 40 });
            _graphView.AddNodeView(node);
            RefreshChrome();
        }

        private string NewNodeId()
        {
            return "n" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        /// <summary>搜索窗建节点入口：写文档 + 布局 + 图视图。</summary>
        private void AddNodeFromDescriptor(NodeDescriptor descriptor, Vector2 graphPosition)
        {
            if (IsObservation) return;
            var id = NewNodeId();
            var node = _presenter.AddNode(descriptor, id, graphPosition.x, graphPosition.y);
            if (node != null)
            {
                _graphView.AddNodeView(node);
                RefreshChrome();
            }
        }

        private string ResolveNodeDisplayName(NodeDefinition node)
        {
            return _presenter.ResolveNodeDisplayName(node);
        }

        private int ResolveChildOrder(string nodeId)
        {
            return _presenter.ResolveChildOrder(nodeId);
        }

        private bool CanConnect(string childId, string parentId, out string error)
        {
            return _presenter.CanConnect(childId, parentId, out error);
        }

        /// <summary>把当前选中的节点包围成一个新分组。</summary>
        private void AddGroupFromSelection()
        {
            if (IsObservation) return;
            var selected = _graphView.selection.OfType<AuthoringNodeView>().ToList();
            if (selected.Count == 0) return;
            PushUndo();

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            var memberIds = new List<string>();
            foreach (var view in selected)
            {
                var rect = view.GetPosition();
                minX = Math.Min(minX, rect.x);
                minY = Math.Min(minY, rect.y);
                maxX = Math.Max(maxX, rect.xMax);
                maxY = Math.Max(maxY, rect.yMax);
                memberIds.Add(view.Node.Id);
            }

            var group = new AuthoringGroupData
            {
                Id = "g" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Title = "分组 " + (_document.Groups.Count + 1),
                X = minX - 20f,
                Y = minY - 40f,
                Width = maxX - minX + 40f,
                Height = maxY - minY + 60f,
                NodeIds = memberIds,
            };
            _document.Groups.Add(group);
            _graphView.AddGroup(group);
            RefreshChrome();
        }

        private void OnEdgeChanged(string childId, string parentId, bool connected)
        {
            if (_workspace.SetConnectedFromRecordedGraphChange(childId, parentId, connected, out var error))
            {
                RefreshChrome();
                return;
            }
            if (!string.IsNullOrWhiteSpace(error)) Debug.LogWarning("[BtAuthoring] " + error);
        }

        AuthoringSourceDocument IAuthoringWorkspaceHost.Document => _document;
        bool IAuthoringWorkspaceHost.IsReadOnly => IsObservation;
        string IAuthoringWorkspaceHost.ResolveNodeDisplayName(NodeDefinition node)
            => ResolveNodeDisplayName(node);
        void IAuthoringWorkspaceHost.RecordChange() => PushUndo();
        void IAuthoringWorkspaceHost.RecordChange(string beforeChangeSnapshot)
            => PushUndoSnapshot(beforeChangeSnapshot);

        bool IAuthoringGraphHost.CanConnect(string childId, string parentId, out string error)
            => CanConnect(childId, parentId, out error);
        void IAuthoringGraphHost.SetConnected(string childId, string parentId, bool connected)
            => OnEdgeChanged(childId, parentId, connected);
        int IAuthoringGraphHost.ResolveChildOrder(string nodeId) => ResolveChildOrder(nodeId);
        Vector2 IAuthoringGraphHost.ScreenToGraphPosition(Vector2 screenPosition)
        {
            var windowRoot = rootVisualElement;
            var windowMousePosition = windowRoot.ChangeCoordinatesTo(
                windowRoot.parent, screenPosition - position.position);
            return _graphView.contentViewContainer.WorldToLocal(windowMousePosition);
        }
        void IAuthoringGraphHost.AddNode(NodeDescriptor descriptor, Vector2 graphPosition)
            => AddNodeFromDescriptor(descriptor, graphPosition);

        ObservationSnapshot? IAuthoringInspectorHost.DisplayedObservationSnapshot => _displayedObservationSnapshot;
        ObservationSnapshot? IAuthoringInspectorHost.PreviousObservationSnapshot => _previousObservationSnapshot;
        ObservationDiff? IAuthoringInspectorHost.DisplayedObservationDiff => _displayedObservationDiff;
        ObservationBlackboard? IAuthoringInspectorHost.InitialRuntimeBlackboard => _previewSession?.InitialBlackboard;
        void IAuthoringInspectorHost.RefreshNodeTitles() => _graphView.RefreshNodeTitles();
        void IAuthoringInspectorHost.RebuildGraph() => RebuildGraph();
        void IAuthoringInspectorHost.RefreshChrome() => RefreshChrome();
        void IAuthoringInspectorHost.FocusNode(string nodeId) => _graphView.FocusNode(nodeId);
    }

}
