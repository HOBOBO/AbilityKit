using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
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
    public sealed class BtAuthoringGraphWindow : EditorWindow
    {
        private BtAuthoringAsset? _asset;
        private BtAuthoringSourceDocument _document = new();
        private BtAuthoringGraphView _graphView = null!;
        private ScrollView _inspectorScroll = null!;
        private BtNodeDefinition? _selectedNode;
        private Label? _modeLabel;
        private Label? _validationLabel;
        private List<string> _undoList = new();
        private Stack<string> _redoStack = new();
        private const int UndoDepthLimit = 64;

        /// <summary>观察模式：绑定一个运行中实例，把实时节点状态着色到画布（只读）。</summary>
        private IBtTreeDebugView? _observedView;

        internal bool IsObservation => _observedView != null;

        public static void Open(BtAuthoringAsset asset)
        {
            var window = GetWindow<BtAuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Authoring");
            window.minSize = new Vector2(900f, 560f);
            window.EnterEditMode(asset);
        }

        /// <summary>以观察模式打开：从运行时实例的树定义渲染图，实时着色节点状态。</summary>
        public static void OpenObservation(IBtTreeDebugView view)
        {
            if (view == null) return;
            var window = GetWindow<BtAuthoringGraphWindow>();
            window.titleContent = new GUIContent("BT Observation Graph");
            window.minSize = new Vector2(900f, 560f);
            window.EnterObservationMode(view);
        }

        private void EnterEditMode(BtAuthoringAsset asset)
        {
            _observedView = null;
            _asset = asset;
            _document = asset != null ? asset.LoadDocument() : new BtAuthoringSourceDocument();
            _selectedNode = null;
            BuildUi();
            RebuildGraph();
        }

        private void EnterObservationMode(IBtTreeDebugView view)
        {
            _observedView = view;
            _asset = null;
            _selectedNode = null;
            // 从运行时定义构造只读文档（布局为空，节点按层级自动排布）
            _document = BuildObservationDocument(view.TreeDefinition);
            BuildUi();
            RebuildGraph();
        }

        private static BtAuthoringSourceDocument BuildObservationDocument(BtTreeDefinition definition)
        {
            if (definition == null) return new BtAuthoringSourceDocument();
            var document = BtTreeExporter.Import(definition);
            // 简单层级布局：深度为列，同深度依次下行
            var depthOf = new Dictionary<string, int>();
            var ordered = new List<(string id, int depth)>();
            ComputeDepth(definition, definition.RootNodeId, 0, depthOf, ordered);
            var rowByDepth = new Dictionary<int, int>();
            foreach (var (id, depth) in ordered)
            {
                rowByDepth.TryGetValue(depth, out var row);
                rowByDepth[depth] = row + 1;
                var layout = document.Layout.Find(l => l.NodeId == id);
                if (layout != null)
                {
                    layout.X = depth * 260f;
                    layout.Y = row * 130f;
                }
            }
            return document;
        }

        private static void ComputeDepth(
            BtTreeDefinition definition, string nodeId, int depth,
            Dictionary<string, int> depthOf, List<(string id, int depth)> ordered)
        {
            if (depthOf.ContainsKey(nodeId)) return;
            depthOf[nodeId] = depth;
            ordered.Add((nodeId, depth));
            foreach (var node in definition.Nodes)
            {
                if (string.Equals(node.Id, nodeId, System.StringComparison.Ordinal))
                {
                    foreach (var childId in node.ChildIds)
                    {
                        ComputeDepth(definition, childId, depth + 1, depthOf, ordered);
                    }
                    break;
                }
            }
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
            _modeLabel = new Label(IsObservation ? "观察模式（只读）" : "Behavior Tree");
            toolbar.Add(_modeLabel);
            if (IsObservation)
            {
                toolbar.Add(new Button(() => ExitObservation()) { text = "退出观察" });
                toolbar.Add(new Button(() => _graphView.FrameAll()) { text = "Frame All" });
            }
            else
            {
                toolbar.Add(new Button(() => Save()) { text = "Save" });
                toolbar.Add(new Button(() => ExportRuntime()) { text = "Export Runtime" });
                toolbar.Add(new Button(() => AddRoot()) { text = "Add Root" });
                toolbar.Add(new Button(() => PerformUndo()) { text = "撤销 (Ctrl+Z)" });
                toolbar.Add(new Button(() => PerformRedo()) { text = "重做 (Ctrl+Y)" });
                toolbar.Add(new Button(() => AddGroupFromSelection()) { text = "包围所选建分组" });
                toolbar.Add(new Button(() => ValidateOnGraph()) { text = "校验" });
            }
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(0, 260f, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(_graphView);

            var rightPane = new VisualElement();
            _inspectorScroll = new ScrollView();
            rightPane.Add(_inspectorScroll);
            _validationLabel = new Label { style = { whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal, paddingTop = 6 } };
            rightPane.Add(_validationLabel);
            split.Add(rightPane);
            rootVisualElement.Add(split);
        }

        // ------------------------------------------------------------------
        // 撤销/重做：文档 JSON 快照栈（图操作前压栈；Ctrl+Z / Ctrl+Y）
        // ------------------------------------------------------------------

        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            if (IsObservation) return;
            if (evt == null || !evt.ctrlKey) return;
            if (evt.keyCode == UnityEngine.KeyCode.Z)
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

        internal void PushUndo()
        {
            if (IsObservation) return;
            _redoStack.Clear();
            _undoList.Add(BtAuthoringJson.Save(_document));
            if (_undoList.Count > UndoDepthLimit)
            {
                _undoList.RemoveAt(0);   // 丢弃最旧快照
            }
        }

        private void PerformUndo()
        {
            if (IsObservation || _undoList.Count == 0) return;
            _redoStack.Push(BtAuthoringJson.Save(_document));
            ApplySnapshot(_undoList[_undoList.Count - 1]);
            _undoList.RemoveAt(_undoList.Count - 1);
        }

        private void PerformRedo()
        {
            if (IsObservation || _redoStack.Count == 0) return;
            _undoList.Add(BtAuthoringJson.Save(_document));
            ApplySnapshot(_redoStack.Pop());
        }

        private void ApplySnapshot(string documentJson)
        {
            try
            {
                _document = BtAuthoringJson.Load(documentJson);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BtAuthoring] 撤销快照损坏，已放弃: " + ex.Message);
                return;
            }
            _selectedNode = null;
            RebuildGraph();
        }

        /// <summary>图上校验：右栏列出错误，错误涉及的节点标红边框。</summary>
        private void ValidateOnGraph()
        {
            var errors = BtTreeValidator.Validate(_document.Tree, BtEditorNodeCatalog.Registry);
            if (_validationLabel == null) return;
            if (errors.Count == 0)
            {
                _validationLabel.text = "✔ 校验通过";
                _validationLabel.style.color = new Color(0.55f, 0.9f, 0.62f);
                _graphView.ClearNodeStates();
            }
            else
            {
                _validationLabel.text = "✘ " + errors.Count + " 个错误：\n" + string.Join("\n", errors);
                _validationLabel.style.color = new Color(0.95f, 0.5f, 0.45f);
                _graphView.MarkErrorNodes(errors);
            }
        }

        private void ExitObservation()
        {
            EnterEditMode(null);
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
                _graphView.ClearNodeStates();
                return;
            }

            _graphView.ApplyNodeStates(_observedView.GetNodeStates());
            if (_modeLabel != null) _modeLabel.text = "观察模式  frame " + _observedView.LastFrame;
        }

        private void UpdateSelectedInspector()
        {
            if (_graphView == null) return;
            var view = _graphView.selection?.OfType<BtAuthoringNodeView>().FirstOrDefault();
            var selected = view?.Node;
            if (ReferenceEquals(selected, _selectedNode)) return;
            _selectedNode = selected;
            RedrawInspector();
        }

        private void RebuildGraph()
        {
            if (_graphView == null) return;
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
            RedrawInspector();
        }

        private void RedrawInspector()
        {
            _inspectorScroll.Clear();
            if (_selectedNode == null)
            {
                DrawTreePanel();
                return;
            }

            var node = _selectedNode;
            _inspectorScroll.Add(new Label(node.Name));
            _inspectorScroll.Add(new Label("Type: " + node.Type));

            if (!BtEditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out var descriptor))
            {
                return;
            }

            _inspectorScroll.Add(new Label("Properties"));
            foreach (var field in descriptor.PropertySchema.OrderBy(f => f.Order))
            {
                var fieldRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                fieldRow.Add(new Label(field.Name) { style = { width = 140 } });

                var current = node.Properties.TryGet(field.Name, out var existing)
                    ? existing
                    : (field.Default ?? DefaultOf(field.Type));

                if (IsObservation)
                {
                    // 观察模式只读：仅展示当前值
                    fieldRow.Add(new Label(FormatFieldValue(field, current)));
                    _inspectorScroll.Add(fieldRow);
                    continue;
                }

                if (field.Kind == BtPropertyFieldKind.Enum)
                {
                    var options = field.Options.Count > 0 ? field.Options : new[] { "<空>" };
                    var index = (int)Math.Clamp(current.Int64Value, 0, options.Count - 1);
                    var popup = new PopupField<string>(new List<string>(options), index);
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        PushUndo();
                        node.Properties.Set(field.Name, BtPropertyValue.Of((long)popup.index));
                    });
                    fieldRow.Add(popup);
                }
                else if (field.Kind == BtPropertyFieldKind.BlackboardKeyRef)
                {
                    var choices = new List<string>();
                    foreach (var key in _document.Tree.Blackboard.Keys)
                    {
                        if (!choices.Contains(key.Name)) choices.Add(key.Name);
                    }
                    if (!choices.Contains(current.StringValue)) choices.Add(current.StringValue);
                    var popup = new PopupField<string>(choices, current.StringValue);
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        PushUndo();
                        node.Properties.Set(field.Name, BtPropertyValue.Of(evt.newValue));
                    });
                    fieldRow.Add(popup);
                }
                else
                {
                    switch (field.Type)
                    {
                        case BtValueType.Bool:
                            var toggle = new Toggle { value = current.BoolValue };
                            toggle.RegisterValueChangedCallback(evt =>
                            {
                                PushUndo();
                                node.Properties.Set(field.Name, BtPropertyValue.Of(evt.newValue));
                            });
                            fieldRow.Add(toggle);
                            break;

                        case BtValueType.Int64:
                            var intField = new IntegerField { value = (int)current.Int64Value };
                            intField.RegisterValueChangedCallback(evt =>
                            {
                                PushUndo();
                                node.Properties.Set(field.Name, BtPropertyValue.Of((long)evt.newValue));
                            });
                            fieldRow.Add(intField);
                            break;

                        case BtValueType.Fixed64:
                            var fixedField = new FloatField
                            {
                                value = AbilityKit.Deterministic.Fixed64.FromRaw(current.Fixed64Raw).ToSingle(),
                            };
                            fixedField.RegisterValueChangedCallback(evt =>
                            {
                                PushUndo();
                                node.Properties.Set(field.Name,
                                    BtPropertyValue.Of(AbilityKit.Deterministic.Fixed64.FromSingle(evt.newValue)));
                            });
                            fieldRow.Add(fixedField);
                            break;

                        case BtValueType.String:
                            var textField = new TextField { value = current.StringValue };
                            textField.RegisterValueChangedCallback(evt =>
                            {
                                PushUndo();
                                node.Properties.Set(field.Name, BtPropertyValue.Of(evt.newValue));
                            });
                            fieldRow.Add(textField);
                            break;
                    }
                }

                if (field.Min.HasValue || field.Max.HasValue)
                {
                    fieldRow.Add(new Label($"[{field.Min?.ToString() ?? "-∞"}, {field.Max?.ToString() ?? "+∞"}]")
                        { style = { opacity = 0.5f } });
                }

                _inspectorScroll.Add(fieldRow);
            }
        }

        private static string FormatFieldValue(BtPropertyField field, BtPropertyValue value)
        {
            if (field.Kind == BtPropertyFieldKind.Enum)
            {
                var index = (int)value.Int64Value;
                return index >= 0 && index < field.Options.Count ? field.Options[index] : index.ToString();
            }
            return value?.ToString() ?? "";
        }

        /// <summary>未选中节点时的树级面板：TreeId / 描述 / 黑板 schema 编辑。</summary>
        private void DrawTreePanel()
        {
            _inspectorScroll.Add(new Label("Tree"));
            _inspectorScroll.Add(new Label("（选中图上节点编辑其属性）") { style = { opacity = 0.6f } });

            if (IsObservation)
            {
                _inspectorScroll.Add(new Label("TreeId: " + _document.Tree.TreeId));
                return;
            }

            var treeIdField = new TextField("TreeId（=导出文件名）") { value = _document.Tree.TreeId };
            treeIdField.RegisterValueChangedCallback(evt =>
            {
                PushUndo();
                _document.Tree.TreeId = evt.newValue;
            });
            _inspectorScroll.Add(treeIdField);

            var descriptionField = new TextField("描述") { value = _document.Metadata.Description };
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                PushUndo();
                _document.Metadata.Description = evt.newValue;
            });
            _inspectorScroll.Add(descriptionField);

            _inspectorScroll.Add(new Label("Blackboard Schema") { style = { paddingTop = 8 } });
            _inspectorScroll.Add(new Label("改名不会同步节点里的 key 引用——改完后跑一次校验抓失效引用。")
                { style = { opacity = 0.6f, whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal } });

            for (var i = 0; i < _document.Tree.Blackboard.Keys.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var nameField = new TextField { value = _document.Tree.Blackboard.Keys[index].Name, isDelayed = true };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    PushUndo();
                    _document.Tree.Blackboard.Keys[index].Name = evt.newValue;
                });
                row.Add(nameField);

                var typeField = new EnumField(_document.Tree.Blackboard.Keys[index].Type);
                typeField.RegisterValueChangedCallback(evt =>
                {
                    PushUndo();
                    _document.Tree.Blackboard.Keys[index].Type = (BtValueType)evt.newValue;
                });
                row.Add(typeField);

                var removeButton = new Button(() =>
                {
                    PushUndo();
                    _document.Tree.Blackboard.Keys.RemoveAt(index);
                    RedrawInspector();
                }) { text = "-" };
                row.Add(removeButton);
                _inspectorScroll.Add(row);
            }

            _inspectorScroll.Add(new Button(() =>
            {
                PushUndo();
                _document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                {
                    Name = "key" + _document.Tree.Blackboard.Keys.Count,
                    Type = BtValueType.Int64,
                });
                RedrawInspector();
            }) { text = "+ 添加 Key" });

            _inspectorScroll.Add(new Label("Groups") { style = { paddingTop = 8 } });
            for (var i = 0; i < _document.Groups.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var titleField = new TextField { value = _document.Groups[index].Title, isDelayed = true };
                titleField.RegisterValueChangedCallback(evt =>
                {
                    PushUndo();
                    _document.Groups[index].Title = evt.newValue;
                    RebuildGraph();
                });
                row.Add(titleField);
                row.Add(new Label(_document.Groups[index].NodeIds.Count + " 节点")
                    { style = { opacity = 0.5f } });
                row.Add(new Button(() =>
                {
                    PushUndo();
                    _document.Groups.RemoveAt(index);
                    RebuildGraph();
                }) { text = "-" });
                _inspectorScroll.Add(row);
            }
        }

        private static BtPropertyValue DefaultOf(BtValueType type) => type switch
        {
            BtValueType.Bool => BtPropertyValue.Of(false),
            BtValueType.Int64 => BtPropertyValue.Of(0L),
            BtValueType.Fixed64 => BtPropertyValue.Of(AbilityKit.Deterministic.Fixed64.Zero),
            BtValueType.String => BtPropertyValue.Of(""),
            _ => BtPropertyValue.Of(0L),
        };

        private void Save()
        {
            if (_asset == null) return;
            _asset.SaveDocument(_document);
            EditorUtility.SetDirty(_asset);
            Debug.Log("[BtAuthoring] Saved.");
        }

        private void ExportRuntime()
        {
            if (_asset == null) return;
            var ok = BtAuthoringRuntimeExporter.Export(_asset, out var outputs, out var errors);
            EditorUtility.DisplayDialog(
                "Runtime Export",
                ok ? "Exported:\n" + string.Join("\n", outputs) : string.Join("\n", errors),
                "OK");
        }

        private void AddRoot()
        {
            PushUndo();
            var id = NewNodeId();
            var node = new BtNodeDefinition { Id = id, Type = BtBuiltInNodeTypes.Sequence, Name = "Root" };
            _document.Tree.Nodes.Add(node);
            _document.Tree.RootNodeId = id;
            _document.Layout.Add(new BtNodeLayoutData { NodeId = id, X = 400, Y = 40 });
            _graphView.AddNodeView(node);
        }

        internal string NewNodeId()
        {
            return "n" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        internal BtAuthoringGraphView GraphView => _graphView;

        /// <summary>搜索窗建节点入口：写文档 + 布局 + 图视图。</summary>
        internal void AddNodeFromDescriptor(BtNodeDescriptor descriptor, Vector2 graphPosition)
        {
            PushUndo();
            var id = NewNodeId();
            var node = new BtNodeDefinition
            {
                Id = id,
                Type = descriptor.TypeId,
                Name = descriptor.DisplayName,
            };
            foreach (var field in descriptor.PropertySchema)
            {
                if (field.Default != null)
                {
                    node.Properties.Set(field.Name, field.Default);
                }
            }
            _document.Tree.Nodes.Add(node);
            _document.Layout.Add(new BtNodeLayoutData { NodeId = id, X = graphPosition.x, Y = graphPosition.y });
            _graphView.AddNodeView(node);
        }

        internal BtAuthoringSourceDocument Document => _document;

        /// <summary>把当前选中的节点包围成一个新分组。</summary>
        internal void AddGroupFromSelection()
        {
            PushUndo();
            var selected = _graphView.selection.OfType<BtAuthoringNodeView>().ToList();
            if (selected.Count == 0) return;

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

        internal void OnEdgeChanged(string childId, string parentId, bool connected)
        {
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
    }

    /// <summary>GraphView 实现：节点视图 + 边回调 + 描述符驱动创建菜单。</summary>
    internal sealed class BtAuthoringGraphView : GraphView
    {
        private readonly BtAuthoringGraphWindow _window;
        private readonly Dictionary<string, BtAuthoringNodeView> _nodeViewsById =
            new(System.StringComparer.Ordinal);
        private readonly Dictionary<Group, BtAuthoringGroupData> _groupDataByElement = new();

        public BtAuthoringGraphView(BtAuthoringGraphWindow window)
        {
            _window = window;

            AddSearchWindow();
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            graphViewChanged += OnGraphViewChanged;
            serializeGraphElements = SerializeElements;
            canPasteSerializedData = _ => false;
        }

        private void AddSearchWindow()
        {
            var provider = ScriptableObject.CreateInstance<BtNodeSearchProvider>();
            provider.Init(_window);
            nodeCreationRequest = context =>
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), provider);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_window.IsObservation)
            {
                // 观察模式只读：吞掉增删边/节点变更（返回空 change 取消操作；拖动仅影响画布不落盘）
                return new GraphViewChange();
            }

            // 一次用户动作（建边/删选区）压一份撤销快照
            if ((change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                || (change.elementsToRemove != null && change.elementsToRemove.Count > 0))
            {
                _window.PushUndo();
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.input.node is BtAuthoringNodeView parentView
                        && edge.output.node is BtAuthoringNodeView childView)
                    {
                        _window.OnEdgeChanged(childView.Node.Id, parentView.Node.Id, true);
                    }
                }
            }
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge
                        && edge.input.node is BtAuthoringNodeView parentView
                        && edge.output.node is BtAuthoringNodeView childView)
                    {
                        _window.OnEdgeChanged(childView.Node.Id, parentView.Node.Id, false);
                    }
                    if (element is BtAuthoringNodeView nodeView)
                    {
                        _window.Document.Tree.Nodes.RemoveAll(n => n.Id == nodeView.Node.Id);
                        _window.Document.Layout.RemoveAll(l => l.NodeId == nodeView.Node.Id);
                    }
                    if (element is Group group)
                    {
                        if (_groupDataByElement.TryGetValue(group, out var groupData))
                        {
                            _window.Document.Groups.Remove(groupData);
                            _groupDataByElement.Remove(group);
                        }
                    }
                }
            }
            if (change.movedElements != null)
            {
                // 拖动后把画布坐标写回文档，Save 时持久化
                foreach (var element in change.movedElements)
                {
                    if (element is BtAuthoringNodeView nodeView)
                    {
                        var position = nodeView.GetPosition();
                        var layout = _window.Document.Layout.Find(l => l.NodeId == nodeView.Node.Id);
                        if (layout != null)
                        {
                            layout.X = position.x;
                            layout.Y = position.y;
                        }
                    }
                    else if (element is Group group && _groupDataByElement.TryGetValue(group, out var groupData))
                    {
                        var rect = group.GetPosition();
                        groupData.X = rect.x;
                        groupData.Y = rect.y;
                        groupData.Width = rect.width;
                        groupData.Height = rect.height;
                    }
                }
            }
            return change;
        }

        private string SerializeElements(IEnumerable<GraphElement> elements) => string.Empty;

        public void AddNodeView(BtNodeDefinition node)
        {
            var layout = _window.Document.Layout.Find(l => l.NodeId == node.Id);
            var view = new BtAuthoringNodeView(node, layout?.X ?? 0f, layout?.Y ?? 0f);
            if (string.Equals(node.Id, _window.Document.Tree.RootNodeId, System.StringComparison.Ordinal))
            {
                view.title = "★ " + view.title;
            }
            _nodeViewsById[node.Id] = view;
            AddElement(view);
        }

        public void Connect(string childId, string parentId)
        {
            var parent = FindNodeView(parentId);
            var child = FindNodeView(childId);
            if (parent == null || child == null) return;

            var edge = parent.inputContainer.Children().OfType<Port>().FirstOrDefault()?.ConnectTo(child.OutputPort);
            if (edge != null) AddElement(edge);
        }

        public void ClearAll()
        {
            _nodeViewsById.Clear();
            _groupDataByElement.Clear();
            foreach (var element in graphElements.ToList()) RemoveElement(element);
        }

        /// <summary>观察模式：把运行时节点状态着色到画布（运行中加边框高亮）。</summary>
        public void ApplyNodeStates(IEnumerable<BtNodeDebugInfo> states)
        {
            foreach (var state in states)
            {
                if (state == null || !_nodeViewsById.TryGetValue(state.NodeId, out var view)) continue;
                view.ApplyRuntimeState(state.State, state.OnStackCount > 0);
            }
        }

        public void ClearNodeStates()
        {
            foreach (var view in _nodeViewsById.Values)
            {
                view.ApplyRuntimeState(BtNodeState.Inactive, false);
            }
        }

        /// <summary>校验错误标红：错误消息以 'nodeId' 引用节点，命中的节点加红边框。</summary>
        public void MarkErrorNodes(List<string> errors)
        {
            foreach (var pair in _nodeViewsById)
            {
                var quoted = "'" + pair.Key + "'";
                var hasError = false;
                foreach (var error in errors)
                {
                    if (error.Contains(quoted)) { hasError = true; break; }
                }
                pair.Value.SetErrorBorder(hasError);
            }
        }

        private BtAuthoringNodeView FindNodeView(string nodeId)
        {
            return _nodeViewsById.TryGetValue(nodeId, out var view) ? view : null;
        }

        /// <summary>按文档分组数据渲染分组框，并把成员节点加入分组。</summary>
        public void AddGroups(IEnumerable<BtAuthoringGroupData> groups)
        {
            if (groups == null) return;
            foreach (var groupData in groups)
            {
                if (groupData == null) continue;
                AddGroup(groupData);
            }
        }

        public void AddGroup(BtAuthoringGroupData groupData)
        {
            var group = new Group
            {
                title = string.IsNullOrEmpty(groupData.Title) ? "分组" : groupData.Title,
            };
            group.SetPosition(new Rect(groupData.X, groupData.Y,
                Mathf.Max(groupData.Width, 120f), Mathf.Max(groupData.Height, 60f)));
            AddElement(group);
            _groupDataByElement[group] = groupData;

            foreach (var nodeId in groupData.NodeIds)
            {
                var view = FindNodeView(nodeId);
                if (view != null) group.AddElement(view);
            }
        }
    }

    /// <summary>
    /// 节点视图：输出端口（parent，连向父节点）所有节点都有；
    /// 输入端口（child，接收子节点）仅组合/装饰节点有（按描述符 Kind 判定）。
    /// </summary>
    internal sealed class BtAuthoringNodeView : Node
    {
        public BtNodeDefinition Node { get; }

        public BtAuthoringNodeView(BtNodeDefinition node, float x, float y)
        {
            Node = node;
            title = string.IsNullOrEmpty(node.Name) ? node.Type : node.Name;
            SetPosition(new Rect(x, y, 160, 60));

            BtNodeDescriptor? descriptor = null;
            var isParentKind = BtEditorNodeCatalog.Registry.TryGetDescriptor(node.Type, out descriptor)
                && (descriptor.Kind == BtNodeKind.Composite || descriptor.Kind == BtNodeKind.Decorator);

            if (descriptor != null)
            {
                titleContainer.style.backgroundColor = ResolveNodeColor(descriptor);
            }
            if (isParentKind)
            {
                // 按 Kind 约束端口：装饰节点恰好一个子，组合节点多个子
                var inputCapacity = descriptor!.Kind == BtNodeKind.Decorator
                    ? Port.Capacity.Single
                    : Port.Capacity.Multi;
                var input = Port.Create<Edge>(Orientation.Vertical, Direction.Input, inputCapacity, typeof(Port));
                input.portName = "child";
                inputContainer.Add(input);
            }

            var output = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(Port));
            output.portName = "parent";
            OutputPort = output;
            outputContainer.Add(output);

            RefreshExpandedState();
        }

        public Port OutputPort { get; }

        /// <summary>节点主题色：描述符 ColorHint 优先，否则按 Kind 给默认色。</summary>
        private static Color ResolveNodeColor(BtNodeDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptor.ColorHint) && ColorUtility.TryParseHtmlString(descriptor.ColorHint, out var custom))
            {
                return custom;
            }
            return descriptor.Kind switch
            {
                BtNodeKind.Composite => new Color(0.22f, 0.42f, 0.62f),
                BtNodeKind.Decorator => new Color(0.42f, 0.3f, 0.58f),
                BtNodeKind.Condition => new Color(0.2f, 0.48f, 0.32f),
                BtNodeKind.Action => new Color(0.48f, 0.4f, 0.2f),
                _ => new Color(0.3f, 0.3f, 0.3f),
            };
        }

        /// <summary>观察模式着色：标题栏按状态着色，运行中节点加亮色边框。</summary>
        public void ApplyRuntimeState(BtNodeState state, bool onStack)
        {
            titleContainer.style.backgroundColor = state switch
            {
                BtNodeState.Running => new Color(0.65f, 0.55f, 0.1f),
                BtNodeState.Success => new Color(0.18f, 0.5f, 0.25f),
                BtNodeState.Failure => new Color(0.55f, 0.18f, 0.15f),
                _ => new Color(0.18f, 0.18f, 0.18f, 0.4f),
            };

            var border = onStack ? new Color(1f, 0.85f, 0.3f) : new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderBottomWidth = onStack ? 2f : 0f;
            style.borderTopWidth = onStack ? 2f : 0f;
            style.borderLeftWidth = onStack ? 2f : 0f;
            style.borderRightWidth = onStack ? 2f : 0f;
        }

        /// <summary>编辑模式校验错误标记（红边框；观察模式的运行高亮互不干扰——不同模式使用）。</summary>
        public void SetErrorBorder(bool hasError)
        {
            var border = hasError ? new Color(0.95f, 0.25f, 0.2f) : new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = border;
            style.borderTopColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderBottomWidth = hasError ? 2f : 0f;
            style.borderTopWidth = hasError ? 2f : 0f;
            style.borderLeftWidth = hasError ? 2f : 0f;
            style.borderRightWidth = hasError ? 2f : 0f;
        }
    }

    /// <summary>节点创建菜单：从描述符目录拉取分组与类型。</summary>
    internal sealed class BtNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private BtAuthoringGraphWindow? _window;

        public void Init(BtAuthoringGraphWindow window)
        {
            _window = window;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node")),
            };

            foreach (var group in BtEditorNodeCatalog.Registry.Descriptors
                         .Select(d => d.Category)
                         .Distinct()
                         .OrderBy(c => c, StringComparer.Ordinal))
            {
                entries.Add(new SearchTreeGroupEntry(new GUIContent(group), 1));
                foreach (var descriptor in BtEditorNodeCatalog.Registry.Descriptors
                             .Where(d => d.Category == group)
                             .OrderBy(d => d.MenuOrder)
                             .ThenBy(d => d.DisplayName, StringComparer.Ordinal))
                {
                    entries.Add(new SearchTreeEntry(new GUIContent(descriptor.DisplayName))
                    {
                        level = 2,
                        userData = descriptor,
                    });
                }
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is not BtNodeDescriptor descriptor || _window == null) return false;
            if (_window.IsObservation) return false;   // 观察模式只读

            // 屏幕坐标 → 窗口坐标 → 图内容坐标（Unity GraphView 标准换算）
            var windowRoot = _window.rootVisualElement;
            var windowMousePosition = windowRoot.ChangeCoordinatesTo(
                windowRoot.parent, context.screenMousePosition - _window.position.position);
            var graphPosition = _window.GraphView.contentViewContainer.WorldToLocal(windowMousePosition);
            _window.AddNodeFromDescriptor(descriptor, graphPosition);
            return true;
        }
    }
}
