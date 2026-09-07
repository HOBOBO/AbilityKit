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
        private AuthoringAsset? _asset;
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
        private Label? _validationLabel;
        private VisualElement? _validationPanel;
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

        public AuthoringGraphWindow()
        {
            _presenter = new AuthoringWorkspacePresenter(_workspace);
        }

        internal bool IsObservation => _documentSession.IsReadOnly;

        public static void Open(AuthoringAsset asset)
        {
            var window = Resources.FindObjectsOfTypeAll<AuthoringGraphWindow>()
                .FirstOrDefault(candidate => !candidate.IsObservation);
            if (window != null && ReferenceEquals(window._asset, asset))
            {
                window.Show();
                window.Focus();
                return;
            }
            if (window != null && !window.ConfirmAssetSwitch(asset)) return;
            window ??= CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Authoring");
            window.minSize = new Vector2(900f, 560f);
            window.EnterEditMode(asset);
            window.Show();
            window.Focus();
        }

        /// <summary>以观察模式打开：从运行时实例的树定义渲染图，实时着色节点状态。</summary>
        public static void OpenObservation(TreeDebugView view)
        {
            if (view == null) return;
            var window = Resources.FindObjectsOfTypeAll<AuthoringGraphWindow>()
                .FirstOrDefault(candidate => ReferenceEquals(candidate._observedView, view))
                ?? CreateWindow<AuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Observation Graph");
            window.minSize = new Vector2(900f, 560f);
            window.EnterObservationMode(view);
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
            window.titleContent = new GUIContent("BT Observation Graph");
            window.minSize = new Vector2(900f, 560f);
            window.EnterObservationMode(
                new ObservationSnapshotDebugView(snapshot, definition),
                snapshot,
                previousSnapshot,
                diff);
            window.Show();
            window.Focus();
        }

        private void EnterEditMode(AuthoringAsset asset)
        {
            _observedView = null;
            _asset = asset;
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
            ObservationDiff initialDiff = null)
        {
            _observedView = view;
            _asset = null;
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
                AuthoringDocumentCatalog.BuildObservationDocument(view, EditorNodeCatalog.Registry),
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
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
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

            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.style.minHeight = 27f;
            _modeLabel = new Label(L(IsObservation
                ? "abilitykit.behaviortree.mode.observation"
                : "abilitykit.behaviortree.mode.edit"))
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    minWidth = 130f,
                    marginLeft = 6f,
                    marginRight = 6f,
                },
            };
            toolbar.Add(_modeLabel);
            if (IsObservation)
            {
                toolbar.Add(CommandButton(EditorCommandIds.Close, "close"));
                _observationPauseButton = CommandButton(EditorCommandIds.PauseObservation, "pause");
                toolbar.Add(_observationPauseButton);
                toolbar.Add(CommandButton(EditorCommandIds.CopySnapshot, "copy-snapshot"));
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(CommandButton(EditorCommandIds.FrameAll, "frame-all"));
            }
            else
            {
                toolbar.Add(CommandButton(EditorCommandIds.Save, "save"));
                toolbar.Add(CommandButton(EditorCommandIds.Export, "export"));
                toolbar.Add(ToolbarSeparator());
                _undoButton = CommandButton(EditorCommandIds.Undo, "undo");
                _redoButton = CommandButton(EditorCommandIds.Redo, "redo");
                toolbar.Add(_undoButton);
                toolbar.Add(_redoButton);
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(CommandButton(EditorCommandIds.AddRoot, "add-root"));
                toolbar.Add(CommandButton(EditorCommandIds.Group, "group"));
                toolbar.Add(CommandButton(EditorCommandIds.Note, "note"));
                toolbar.Add(CommandButton(EditorCommandIds.AutoLayout, "auto-layout"));
                toolbar.Add(LayoutMenu());
                toolbar.Add(CommandButton(EditorCommandIds.FrameAll, "frame-all"));
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(CommandButton(EditorCommandIds.Validate, "validate"));
                _dirtyLabel = new Label { style = { marginLeft = 8f, opacity = 0.75f } };
                toolbar.Add(_dirtyLabel);
            }

            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });

            _nodeSearchField = new UnityEditor.UIElements.ToolbarSearchField
            {
                tooltip = L("abilitykit.behaviortree.search.tooltip"),
            };
            _nodeSearchField.SetValueWithoutNotify(_workspace.State.NodeSearch);
            _nodeSearchField.style.width = 170f;
            _nodeSearchField.style.marginRight = 6f;
            _nodeSearchField.RegisterValueChangedCallback(evt =>
            {
                _workspace.State.NodeSearch = evt.newValue ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(evt.newValue))
                    _graphView.FocusFirstMatch(evt.newValue);
                RefreshOverview();
            });
            toolbar.Add(_nodeSearchField);
            rootVisualElement.Add(toolbar);

            // Keep the inspector at a readable width while allowing the graph canvas to use the remaining space.
            var split = new TwoPaneSplitView(1, _workspace.State.InspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(_graphView);

            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1f;
            rightPane.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width >= 240f)
                    _workspace.State.InspectorWidth = evt.newRect.width;
            });
            _overviewPanel = new AuthoringOverviewPanel(
                _presenter,
                _workspace.State,
                nodeId => _graphView.FocusNode(nodeId),
                AutoLayout,
                AutoLayoutWithSelectedFixed);
            rightPane.Add(_overviewPanel.Root);
            var inspectorScroll = new ScrollView();
            inspectorScroll.style.flexGrow = 1f;
            _inspectorRenderer = new AuthoringInspectorRenderer(inspectorScroll, this);
            rightPane.Add(inspectorScroll);
            _validationPanel = new ScrollView
            {
                style =
                {
                    display = _workspace.State.GetPanelVisible("validation", false)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None,
                    maxHeight = 190f,
                    paddingTop = 6f,
                    borderTopWidth = 1f,
                    borderTopColor = new Color(0.3f, 0.3f, 0.3f),
                },
            };
            _validationLabel = new Label { style = { whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal } };
            _validationPanel.Add(_validationLabel);
            rightPane.Add(_validationPanel);
            split.Add(rightPane);
            rootVisualElement.Add(split);
            RefreshChrome();
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

        private UnityEditor.UIElements.ToolbarMenu LayoutMenu()
        {
            var menu = new UnityEditor.UIElements.ToolbarMenu
            {
                text = "Layout",
                tooltip = "Adaptive layout operations",
            };
            menu.style.height = 22f;
            menu.style.marginLeft = 1f;
            menu.style.marginRight = 1f;
            menu.menu.AppendAction(
                "All",
                _ => AutoLayout(),
                _ => IsObservation || _document.Tree.Nodes.Count == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "Selected Subtree",
                _ => AutoLayoutSelectedSubtree(),
                _ => IsObservation || _selectedNode == null
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "All, Keep Selection Fixed",
                _ => AutoLayoutWithSelectedFixed(),
                _ => IsObservation || _graphView.GetSelectedNodeIds().Count == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
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
                    ? "Behavior Tree"
                    : _document.Tree.TreeId;
            if (_dirtyLabel != null)
                _dirtyLabel.text = L(_isDirty
                    ? "abilitykit.behaviortree.state.dirty"
                    : "abilitykit.behaviortree.state.saved");
            hasUnsavedChanges = _documentSession.IsDirty;
            _undoButton?.SetEnabled(_documentSession.CanUndo);
            _redoButton?.SetEnabled(_documentSession.CanRedo);
            titleContent = new GUIContent(IsObservation
                ? "BT Observation"
                : (_isDirty ? "BT Authoring *" : "BT Authoring"));
            RefreshOverview();
        }

        /// <summary>图上校验：右栏列出结构化诊断，节点定位由诊断动作显式提供。</summary>
        private void ValidateOnGraph()
        {
            _diagnostics.Replace(EditorDiagnostics.Analyze(
                _document,
                EditorNodeCatalog.Registry,
                nodeId => _graphView.FocusNode(nodeId)).Items);
            if (_validationLabel == null || _validationPanel == null) return;
            _validationPanel.Clear();
            _validationPanel.style.display = DisplayStyle.Flex;
            _workspace.State.SetPanelVisible("validation", true);
            if (!_diagnostics.HasErrors)
            {
                _validationLabel.text = L("abilitykit.behaviortree.validation.success");
                _validationLabel.style.color = new Color(0.55f, 0.9f, 0.62f);
                _validationPanel.Add(_validationLabel);
                _graphView.ClearErrorNodes();
                return;
            }

            _validationLabel.text = _localization.Format(
                "abilitykit.behaviortree.validation.errors",
                _diagnostics.ErrorCount);
            _validationLabel.style.color = new Color(0.95f, 0.5f, 0.45f);
            _validationPanel.Add(_validationLabel);
            foreach (var diagnostic in _diagnostics.Items)
            {
                if (!diagnostic.CanLocate)
                {
                    _validationPanel.Add(new Label(diagnostic.Message)
                    {
                        style = { whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal },
                    });
                    continue;
                }

                var nodeId = diagnostic.Path.Substring("nodes/".Length);
                var focusError = new Button(() => diagnostic.Locate?.Invoke())
                {
                    text = diagnostic.Message,
                    tooltip = _localization.Format(
                        "abilitykit.behaviortree.validation.locate",
                        nodeId),
                };
                focusError.style.unityTextAlign = TextAnchor.MiddleLeft;
                focusError.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal;
                _validationPanel.Add(focusError);
            }
            _graphView.MarkErrorNodes(_diagnostics.Items
                .Where(item => item.CanLocate)
                .Select(item => item.Path.Substring("nodes/".Length)));
        }

        private void ToggleObservationPause()
        {
            if (_observationController.Paused) _observationController.Resume();
            else _observationController.Pause();
            if (_observationPauseButton != null)
                _observationPauseButton.text = L(_observationController.Paused
                    ? "abilitykit.behaviortree.command.resume"
                    : "abilitykit.behaviortree.command.pause");
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
            ApplyAutoLayout(AuthoringLayoutOptions.Full, frameAll: true);
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
            _workspace.MarkSaved();
            RefreshChrome();
            Debug.Log("[BtAuthoring] Saved.");
        }

        private void ExportRuntime()
        {
            if (_asset == null) return;
            Save();
            var report = AuthoringRuntimeExporter.Export(_asset);
            var outputs = report.Artifacts.Select(artifact => artifact.Path);
            var successMessage = report.ExportedCount > 0
                ? "Exported:\n" + string.Join("\n", outputs)
                : "Unchanged:\n" + string.Join("\n", outputs);
            EditorUtility.DisplayDialog(
                report.Success ? "Runtime Export" : "Runtime Export Failed",
                report.Success
                    ? successMessage
                    : string.Join("\n", report.Messages),
                "OK");
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
            _document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = id, DisplayName = "Root" });
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

        AuthoringSourceDocument IAuthoringGraphHost.Document => _document;
        bool IAuthoringGraphHost.IsReadOnly => IsObservation;
        void IAuthoringGraphHost.RecordChange() => PushUndo();
        void IAuthoringGraphHost.RecordChange(string beforeChangeSnapshot)
            => PushUndoSnapshot(beforeChangeSnapshot);
        bool IAuthoringGraphHost.CanConnect(string childId, string parentId, out string error)
            => CanConnect(childId, parentId, out error);
        void IAuthoringGraphHost.SetConnected(string childId, string parentId, bool connected)
            => OnEdgeChanged(childId, parentId, connected);
        string IAuthoringGraphHost.ResolveNodeDisplayName(NodeDefinition node)
            => ResolveNodeDisplayName(node);
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

        AuthoringSourceDocument IAuthoringInspectorHost.Document => _document;
        bool IAuthoringInspectorHost.IsReadOnly => IsObservation;
        ObservationSnapshot? IAuthoringInspectorHost.DisplayedObservationSnapshot => _displayedObservationSnapshot;
        ObservationSnapshot? IAuthoringInspectorHost.PreviousObservationSnapshot => _previousObservationSnapshot;
        ObservationDiff? IAuthoringInspectorHost.DisplayedObservationDiff => _displayedObservationDiff;
        string IAuthoringInspectorHost.ResolveNodeDisplayName(NodeDefinition node)
            => ResolveNodeDisplayName(node);
        void IAuthoringInspectorHost.RecordChange() => PushUndo();
        void IAuthoringInspectorHost.RecordChange(string beforeChangeSnapshot)
            => PushUndoSnapshot(beforeChangeSnapshot);
        void IAuthoringInspectorHost.RefreshNodeTitles() => _graphView.RefreshNodeTitles();
        void IAuthoringInspectorHost.RebuildGraph() => RebuildGraph();
        void IAuthoringInspectorHost.RefreshChrome() => RefreshChrome();
        void IAuthoringInspectorHost.FocusNode(string nodeId) => _graphView.FocusNode(nodeId);
    }

}
