using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 行为树图编辑器。节点目录、端口数量、属性面板全部由 <see cref="BtNodeRegistry"/>
    /// 描述符驱动生成——编辑器只认识描述符，新增包外节点零编辑器代码。
    /// 边方向：子 → 父（输出端口在上，输入端口在下，连线建立 ChildIds 关系）。
    /// </summary>
    public sealed class BtAuthoringGraphWindow : EditorWindow, IBtAuthoringGraphHost, IBtAuthoringInspectorHost
    {
        private BtAuthoringAsset? _asset;
        private readonly BtAuthoringDocumentSession _documentSession = new();
        private BtAuthoringSourceDocument _document => _documentSession.Document;
        private BtAuthoringGraphView _graphView = null!;
        private BtAuthoringInspectorRenderer _inspectorRenderer = null!;
        private BtNodeDefinition? _selectedNode;
        private Label? _modeLabel;
        private Label? _dirtyLabel;
        private Label? _validationLabel;
        private VisualElement? _validationPanel;
        private UnityEditor.UIElements.ToolbarSearchField? _nodeSearchField;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _observationPauseButton;
        private readonly List<BtNodeDebugInfo> _observationStates = new();
        private bool _isDirty => _documentSession.IsDirty;
        private bool _observationPaused;
        private int _observationFrame;

        /// <summary>观察模式：绑定一个运行中实例，把实时节点状态着色到画布（只读）。</summary>
        private IBtTreeDebugView? _observedView;

        internal bool IsObservation => _documentSession.IsReadOnly;

        public static void Open(BtAuthoringAsset asset)
        {
            var window = Resources.FindObjectsOfTypeAll<BtAuthoringGraphWindow>()
                .FirstOrDefault(candidate => !candidate.IsObservation);
            if (window != null && ReferenceEquals(window._asset, asset))
            {
                window.Show();
                window.Focus();
                return;
            }
            if (window != null && !window.ConfirmAssetSwitch(asset)) return;
            window ??= CreateWindow<BtAuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Authoring");
            window.minSize = new Vector2(900f, 560f);
            window.EnterEditMode(asset);
            window.Show();
            window.Focus();
        }

        /// <summary>以观察模式打开：从运行时实例的树定义渲染图，实时着色节点状态。</summary>
        public static void OpenObservation(IBtTreeDebugView view)
        {
            if (view == null) return;
            var window = Resources.FindObjectsOfTypeAll<BtAuthoringGraphWindow>()
                .FirstOrDefault(candidate => ReferenceEquals(candidate._observedView, view))
                ?? CreateWindow<BtAuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Observation Graph");
            window.minSize = new Vector2(900f, 560f);
            window.EnterObservationMode(view);
            window.Show();
            window.Focus();
        }

        private void EnterEditMode(BtAuthoringAsset asset)
        {
            _observedView = null;
            _asset = asset;
            _documentSession.Open(asset != null ? asset.LoadDocument() : new BtAuthoringSourceDocument());
            _selectedNode = null;
            _observationPaused = false;
            _observationFrame = 0;
            _observationStates.Clear();
            hasUnsavedChanges = false;
            BuildUi();
            RebuildGraph();
        }

        private void EnterObservationMode(IBtTreeDebugView view)
        {
            _observedView = view;
            _asset = null;
            _selectedNode = null;
            _observationPaused = false;
            _observationFrame = view.LastFrame;
            _observationStates.Clear();
            hasUnsavedChanges = false;
            // 从运行时定义构造只读文档（布局为空，节点按层级自动排布）
            _documentSession.Open(
                BtAuthoringDocumentCatalog.BuildObservationDocument(view, BtEditorNodeCatalog.Registry),
                isReadOnly: true);
            BuildUi();
            RebuildGraph();
        }

        private bool ConfirmAssetSwitch(BtAuthoringAsset nextAsset)
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
            _graphView = new BtAuthoringGraphView(this);
            BuildUi();
            _graphView.RegisterCallback<KeyDownEvent>(OnGraphKeyDown);

            // 选择变化通知在该版本 GraphView 上无公开事件，用轻量轮询驱动属性面板
            rootVisualElement.schedule.Execute(UpdateSelectedInspector).Every(200);
            // 观察模式的实时状态着色
            rootVisualElement.schedule.Execute(ObservationTick).Every(150);
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();

            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.style.minHeight = 27f;
            _modeLabel = new Label(IsObservation ? "观察模式（只读）" : "Behavior Tree")
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
                toolbar.Add(ToolbarButton("关闭", "关闭当前观察图", Close));
                _observationPauseButton = ToolbarButton("冻结", "冻结显示，运行时继续推进", ToggleObservationPause);
                toolbar.Add(_observationPauseButton);
                toolbar.Add(ToolbarButton("复制快照", "复制当前运行状态 JSON", CopyObservationSnapshot));
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(ToolbarButton("适应画布", "显示全部节点", () => _graphView.FrameAll()));
            }
            else
            {
                toolbar.Add(ToolbarButton("保存", "保存授权文档 (Ctrl+S)", Save));
                toolbar.Add(ToolbarButton("导出", "保存并导出纯运行时 IR (Ctrl+Shift+E)", ExportRuntime));
                toolbar.Add(ToolbarSeparator());
                _undoButton = ToolbarButton("撤销", "撤销上一步 (Ctrl+Z)", PerformUndo);
                _redoButton = ToolbarButton("重做", "重做下一步 (Ctrl+Y)", PerformRedo);
                toolbar.Add(_undoButton);
                toolbar.Add(_redoButton);
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(ToolbarButton("添加根", "为空树创建可直接运行的根节点", AddRoot));
                toolbar.Add(ToolbarButton("分组", "将当前选中节点放入新分组", AddGroupFromSelection));
                toolbar.Add(ToolbarButton("注释", "在画布中心添加不参与运行时导出的说明", AddCanvasNote));
                toolbar.Add(ToolbarButton("自动布局", "按父子层级整理全部节点 (Ctrl+L)", AutoLayout));
                toolbar.Add(ToolbarButton("适应画布", "显示全部节点", () => _graphView.FrameAll()));
                toolbar.Add(ToolbarSeparator());
                toolbar.Add(ToolbarButton("校验", "校验结构、属性和黑板引用", ValidateOnGraph));
                _dirtyLabel = new Label { style = { marginLeft = 8f, opacity = 0.75f } };
                toolbar.Add(_dirtyLabel);
            }

            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });

            _nodeSearchField = new UnityEditor.UIElements.ToolbarSearchField
            {
                tooltip = "按显示名、节点 ID 或类型查找 (Ctrl+F)",
            };
            _nodeSearchField.style.width = 170f;
            _nodeSearchField.style.marginRight = 6f;
            _nodeSearchField.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrWhiteSpace(evt.newValue))
                    _graphView.FocusFirstMatch(evt.newValue);
            });
            toolbar.Add(_nodeSearchField);
            rootVisualElement.Add(toolbar);

            // Keep the inspector at a readable width while allowing the graph canvas to use the remaining space.
            var split = new TwoPaneSplitView(1, 340f, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(_graphView);

            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1f;
            var inspectorScroll = new ScrollView();
            inspectorScroll.style.flexGrow = 1f;
            _inspectorRenderer = new BtAuthoringInspectorRenderer(inspectorScroll, this);
            rightPane.Add(inspectorScroll);
            _validationPanel = new ScrollView
            {
                style =
                {
                    display = DisplayStyle.None,
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

        private static Button ToolbarButton(string text, string tooltip, Action action)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.style.height = 22f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            return button;
        }

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
                Save();
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.E && evt.shiftKey)
            {
                ExportRuntime();
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.L)
            {
                AutoLayout();
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.Z)
            {
                PerformUndo();
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.Y)
            {
                PerformRedo();
                evt.StopPropagation();
            }
        }

        private void PushUndo()
        {
            if (_documentSession.RecordChange()) RefreshChrome();
        }

        private void PushUndoSnapshot(string snapshot)
        {
            if (_documentSession.RecordChange(snapshot)) RefreshChrome();
        }

        private void PerformUndo()
        {
            try
            {
                if (!_documentSession.Undo()) return;
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
                if (!_documentSession.Redo()) return;
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
            if (_documentSession.DiscardChanges())
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
                _dirtyLabel.text = _isDirty ? "未保存" : "已保存";
            hasUnsavedChanges = _documentSession.IsDirty;
            _undoButton?.SetEnabled(_documentSession.CanUndo);
            _redoButton?.SetEnabled(_documentSession.CanRedo);
            titleContent = new GUIContent(IsObservation
                ? "BT Observation"
                : (_isDirty ? "BT Authoring *" : "BT Authoring"));
        }

        /// <summary>图上校验：右栏列出错误，错误涉及的节点标红边框。</summary>
        private void ValidateOnGraph()
        {
            var errors = BtTreeValidator.Validate(_document.Tree, BtEditorNodeCatalog.Registry);
            if (_validationLabel == null || _validationPanel == null) return;
            _validationPanel.Clear();
            _validationPanel.style.display = DisplayStyle.Flex;
            if (errors.Count == 0)
            {
                _validationLabel.text = "✔ 校验通过";
                _validationLabel.style.color = new Color(0.55f, 0.9f, 0.62f);
                _validationPanel.Add(_validationLabel);
                _graphView.ClearErrorNodes();
            }
            else
            {
                _validationLabel.text = "✘ " + errors.Count + " 个错误";
                _validationLabel.style.color = new Color(0.95f, 0.5f, 0.45f);
                _validationPanel.Add(_validationLabel);
                foreach (var error in errors)
                {
                    var nodeId = FindErrorNodeId(error);
                    if (nodeId == null)
                    {
                        _validationPanel.Add(new Label(error)
                        {
                            style = { whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal },
                        });
                        continue;
                    }

                    var focusError = new Button(() => _graphView.FocusNode(nodeId))
                    {
                        text = error,
                        tooltip = "定位节点 " + nodeId,
                    };
                    focusError.style.unityTextAlign = TextAnchor.MiddleLeft;
                    focusError.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal;
                    _validationPanel.Add(focusError);
                }
                _graphView.MarkErrorNodes(errors);
            }
        }

        private string? FindErrorNodeId(string error)
        {
            foreach (var node in _document.Tree.Nodes.OrderByDescending(n => n.Id.Length))
            {
                if (error.Contains("'" + node.Id + "'")) return node.Id;
            }
            return null;
        }

        private void ToggleObservationPause()
        {
            _observationPaused = !_observationPaused;
            if (_observationPauseButton != null)
                _observationPauseButton.text = _observationPaused ? "继续" : "冻结";
        }

        private void CopyObservationSnapshot()
        {
            if (_observedView == null) return;
            try
            {
                EditorGUIUtility.systemCopyBuffer = BtTreeJson.SaveSnapshot(_observedView.CaptureState());
                ShowNotification(new GUIContent("运行快照已复制"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BtObservation] 无法复制运行快照: " + ex.Message);
                ShowNotification(new GUIContent("快照复制失败"));
            }
        }

        private void ObservationTick()
        {
            if (_observedView == null || _graphView == null) return;

            // 实例已注销（战局结束/树销毁）：提示并停止着色
            var alive = false;
            foreach (var entry in BtDebugRegistry.GetEntries())
            {
                if (ReferenceEquals(entry.View, _observedView)) { alive = true; break; }
            }
            if (!alive)
            {
                if (_modeLabel != null) _modeLabel.text = "观察模式（实例已停止）";
                return;
            }

            if (!_observationPaused)
            {
                _observationStates.Clear();
                _observationStates.AddRange(_observedView.GetNodeStates());
                _observationFrame = _observedView.LastFrame;
                _graphView.ApplyNodeStates(_observationStates);
                _inspectorRenderer.RefreshRuntimeDetails();
            }
            if (_modeLabel != null)
                _modeLabel.text = "观察模式  frame " + _observationFrame
                    + (_observationPaused ? "（已冻结）" : "");
        }

        private void UpdateSelectedInspector()
        {
            if (_graphView == null) return;
            var view = _graphView.selection?.OfType<BtAuthoringNodeView>().FirstOrDefault();
            var selected = view?.Node;
            if (ReferenceEquals(selected, _selectedNode)) return;
            _selectedNode = selected;
            _inspectorRenderer.Render(_selectedNode);
        }

        private void RebuildGraph()
        {
            if (_graphView == null) return;
            BtAuthoringLayoutUtility.EnsureLayout(_document);
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
        }

        private void AutoLayout()
        {
            if (IsObservation || _document.Tree.Nodes.Count == 0) return;
            var before = BtAuthoringJson.Save(_document);
            if (!BtAuthoringLayoutUtility.ApplyLayout(_document)) return;
            PushUndoSnapshot(before);
            _selectedNode = null;
            RebuildGraph();
            rootVisualElement.schedule.Execute(() => _graphView.FrameAll()).ExecuteLater(30);
        }

        private void AddCanvasNote()
        {
            if (IsObservation) return;
            PushUndo();
            var center = _graphView.GetViewportCenter();
            var note = new BtAuthoringNoteData
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
        }

        private void Save()
        {
            if (_asset == null) return;
            _asset.SaveDocument(_document);
            EditorUtility.SetDirty(_asset);
            _documentSession.MarkSaved();
            RefreshChrome();
            Debug.Log("[BtAuthoring] Saved.");
        }

        private void ExportRuntime()
        {
            if (_asset == null) return;
            Save();
            var report = BtAuthoringRuntimeExporter.Export(_asset);
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
            var node = new BtNodeDefinition { Id = id, Type = BtBuiltInNodeTypes.Succeed };
            _document.Tree.Nodes.Add(node);
            _document.Tree.RootNodeId = id;
            _document.NodeMetadata.Add(new BtAuthoringNodeMetadata { NodeId = id, DisplayName = "Root" });
            _document.Layout.Add(new BtNodeLayoutData { NodeId = id, X = 400, Y = 40 });
            _graphView.AddNodeView(node);
        }

        private string NewNodeId()
        {
            return "n" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        /// <summary>搜索窗建节点入口：写文档 + 布局 + 图视图。</summary>
        private void AddNodeFromDescriptor(BtNodeDescriptor descriptor, Vector2 graphPosition)
        {
            if (IsObservation) return;
            PushUndo();
            var id = NewNodeId();
            var node = new BtNodeDefinition
            {
                Id = id,
                Type = descriptor.TypeId,
            };
            foreach (var field in descriptor.PropertySchema)
            {
                if (field.Default != null)
                {
                    node.Properties.Set(field.Name, field.Default);
                }
            }
            _document.Tree.Nodes.Add(node);
            _document.NodeMetadata.Add(new BtAuthoringNodeMetadata
            {
                NodeId = id,
                DisplayName = descriptor.DisplayName,
            });
            _document.Layout.Add(new BtNodeLayoutData { NodeId = id, X = graphPosition.x, Y = graphPosition.y });
            _graphView.AddNodeView(node);
        }

        private string ResolveNodeDisplayName(BtNodeDefinition node)
        {
            if (_document.TryGetNodeMetadata(node.Id, out var metadata)
                && !string.IsNullOrWhiteSpace(metadata.DisplayName))
            {
                return metadata.DisplayName;
            }
            return BtEditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out var descriptor)
                ? descriptor.DisplayName
                : node.Type;
        }

        private int ResolveChildOrder(string nodeId)
        {
            foreach (var parent in _document.Tree.Nodes)
            {
                var index = parent.ChildIds.IndexOf(nodeId);
                if (index >= 0) return index + 1;
            }
            return 0;
        }

        private bool CanConnect(string childId, string parentId, out string error)
        {
            if (!BtEditorNodeCatalog.Registry.TryGetDescriptor(
                    _document.Tree.Nodes.Find(n => n.Id == parentId)?.Type ?? "", out var descriptor))
            {
                error = $"父节点 '{parentId}' 的类型未注册。";
                return false;
            }
            return BtAuthoringGraphOperations.CanConnect(
                _document.Tree, parentId, childId, descriptor.MaxChildren, out error);
        }

        /// <summary>把当前选中的节点包围成一个新分组。</summary>
        private void AddGroupFromSelection()
        {
            if (IsObservation) return;
            var selected = _graphView.selection.OfType<BtAuthoringNodeView>().ToList();
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

            var group = new BtAuthoringGroupData
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
        }

        private void OnEdgeChanged(string childId, string parentId, bool connected)
        {
            if (IsObservation) return;
            var parent = _document.Tree.Nodes.Find(n => n.Id == parentId);
            if (parent == null) return;
            if (connected)
            {
                if (!parent.ChildIds.Contains(childId)) parent.ChildIds.Add(childId);
            }
            else
            {
                parent.ChildIds.Remove(childId);
            }
        }

        BtAuthoringSourceDocument IBtAuthoringGraphHost.Document => _document;
        bool IBtAuthoringGraphHost.IsReadOnly => IsObservation;
        void IBtAuthoringGraphHost.RecordChange() => PushUndo();
        void IBtAuthoringGraphHost.RecordChange(string beforeChangeSnapshot)
            => PushUndoSnapshot(beforeChangeSnapshot);
        bool IBtAuthoringGraphHost.CanConnect(string childId, string parentId, out string error)
            => CanConnect(childId, parentId, out error);
        void IBtAuthoringGraphHost.SetConnected(string childId, string parentId, bool connected)
            => OnEdgeChanged(childId, parentId, connected);
        string IBtAuthoringGraphHost.ResolveNodeDisplayName(BtNodeDefinition node)
            => ResolveNodeDisplayName(node);
        int IBtAuthoringGraphHost.ResolveChildOrder(string nodeId) => ResolveChildOrder(nodeId);
        Vector2 IBtAuthoringGraphHost.ScreenToGraphPosition(Vector2 screenPosition)
        {
            var windowRoot = rootVisualElement;
            var windowMousePosition = windowRoot.ChangeCoordinatesTo(
                windowRoot.parent, screenPosition - position.position);
            return _graphView.contentViewContainer.WorldToLocal(windowMousePosition);
        }
        void IBtAuthoringGraphHost.AddNode(BtNodeDescriptor descriptor, Vector2 graphPosition)
            => AddNodeFromDescriptor(descriptor, graphPosition);

        BtAuthoringSourceDocument IBtAuthoringInspectorHost.Document => _document;
        bool IBtAuthoringInspectorHost.IsReadOnly => IsObservation;
        IBtTreeDebugView? IBtAuthoringInspectorHost.ObservedView => _observedView;
        IReadOnlyList<BtNodeDebugInfo> IBtAuthoringInspectorHost.ObservationStates => _observationStates;
        string IBtAuthoringInspectorHost.ResolveNodeDisplayName(BtNodeDefinition node)
            => ResolveNodeDisplayName(node);
        void IBtAuthoringInspectorHost.RecordChange() => PushUndo();
        void IBtAuthoringInspectorHost.RecordChange(string beforeChangeSnapshot)
            => PushUndoSnapshot(beforeChangeSnapshot);
        void IBtAuthoringInspectorHost.RefreshNodeTitles() => _graphView.RefreshNodeTitles();
        void IBtAuthoringInspectorHost.RebuildGraph() => RebuildGraph();
        void IBtAuthoringInspectorHost.RefreshChrome() => RefreshChrome();
        void IBtAuthoringInspectorHost.FocusNode(string nodeId) => _graphView.FocusNode(nodeId);
    }

}
