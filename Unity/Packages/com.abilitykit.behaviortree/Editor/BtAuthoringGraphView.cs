#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    internal interface IBtAuthoringGraphHost
    {
        BtAuthoringSourceDocument Document { get; }
        bool IsReadOnly { get; }
        void RecordChange();
        void RecordChange(string beforeChangeSnapshot);
        bool CanConnect(string childId, string parentId, out string error);
        void SetConnected(string childId, string parentId, bool connected);
        string ResolveNodeDisplayName(BtNodeDefinition node);
        int ResolveChildOrder(string nodeId);
        Vector2 ScreenToGraphPosition(Vector2 screenPosition);
        void AddNode(BtNodeDescriptor descriptor, Vector2 graphPosition);
    }

    /// <summary>GraphView 实现：节点视图 + 边回调 + 描述符驱动创建菜单。</summary>
    internal sealed class BtAuthoringGraphView : GraphView
    {
        private readonly IBtAuthoringGraphHost _host;
        private readonly Dictionary<string, BtAuthoringNodeView> _nodeViewsById =
            new(System.StringComparer.Ordinal);
        private readonly Dictionary<Group, BtAuthoringGroupData> _groupDataByElement = new();

        public BtAuthoringGraphView(IBtAuthoringGraphHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
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
            provider.Init(_host);
            nodeCreationRequest = context =>
            {
                if (!_host.IsReadOnly)
                    SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), provider);
            };
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_host.IsReadOnly)
            {
                // 观察模式只读：吞掉增删边/节点变更（返回空 change 取消操作；拖动仅影响画布不落盘）
                return new GraphViewChange();
            }

            if (change.edgesToCreate != null)
            {
                for (var i = change.edgesToCreate.Count - 1; i >= 0; i--)
                {
                    var edge = change.edgesToCreate[i];
                    if (edge.input.node is not BtAuthoringNodeView childView
                        || edge.output.node is not BtAuthoringNodeView parentView)
                    {
                        change.edgesToCreate.RemoveAt(i);
                        continue;
                    }
                    if (!_host.CanConnect(childView.Node.Id, parentView.Node.Id, out var error))
                    {
                        Debug.LogWarning("[BtAuthoring] 无法连接节点: " + error);
                        change.edgesToCreate.RemoveAt(i);
                    }
                }
            }

            // 一次用户动作（建边/删除/移动）压一份撤销快照
            if ((change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                || (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
                || (change.movedElements != null && change.movedElements.Count > 0))
            {
                _host.RecordChange();
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.input.node is BtAuthoringNodeView childView
                        && edge.output.node is BtAuthoringNodeView parentView)
                    {
                        _host.SetConnected(childView.Node.Id, parentView.Node.Id, true);
                    }
                }
            }
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge
                        && edge.input.node is BtAuthoringNodeView childView
                        && edge.output.node is BtAuthoringNodeView parentView)
                    {
                        _host.SetConnected(childView.Node.Id, parentView.Node.Id, false);
                    }
                    if (element is BtAuthoringNodeView nodeView)
                    {
                        _host.Document.Tree.Nodes.RemoveAll(n => n.Id == nodeView.Node.Id);
                        _host.Document.Layout.RemoveAll(l => l.NodeId == nodeView.Node.Id);
                        _host.Document.NodeMetadata.RemoveAll(m => m.NodeId == nodeView.Node.Id);
                        foreach (var authoringGroup in _host.Document.Groups)
                            authoringGroup.NodeIds.RemoveAll(id => id == nodeView.Node.Id);
                        if (string.Equals(_host.Document.Tree.RootNodeId, nodeView.Node.Id, StringComparison.Ordinal))
                            _host.Document.Tree.RootNodeId = "";
                    }
                    if (element is Group group)
                    {
                        if (_groupDataByElement.TryGetValue(group, out var groupData))
                        {
                            _host.Document.Groups.Remove(groupData);
                            _groupDataByElement.Remove(group);
                        }
                    }
                    if (element is BtAuthoringNoteView noteView)
                    {
                        _host.Document.Notes.Remove(noteView.Data);
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
                        var layout = _host.Document.Layout.Find(l => l.NodeId == nodeView.Node.Id);
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
                    else if (element is BtAuthoringNoteView noteView)
                    {
                        var rect = noteView.GetPosition();
                        noteView.Data.X = rect.x;
                        noteView.Data.Y = rect.y;
                        noteView.Data.Width = rect.width;
                        noteView.Data.Height = rect.height;
                    }
                }
            }
            if ((change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                || (change.elementsToRemove != null && change.elementsToRemove.Count > 0))
            {
                RefreshNodeTitles();
            }
            return change;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var candidate in ports.ToList())
            {
                if (candidate == startPort
                    || candidate.direction == startPort.direction
                    || candidate.node == startPort.node)
                {
                    continue;
                }

                var output = startPort.direction == Direction.Output ? startPort : candidate;
                var input = startPort.direction == Direction.Input ? startPort : candidate;
                if (output.node is BtAuthoringNodeView parent
                    && input.node is BtAuthoringNodeView child
                    && _host.CanConnect(child.Node.Id, parent.Node.Id, out _))
                {
                    compatible.Add(candidate);
                }
            }
            return compatible;
        }

        private string SerializeElements(IEnumerable<GraphElement> elements) => string.Empty;

        public void AddNodeView(BtNodeDefinition node)
        {
            var layout = _host.Document.Layout.Find(l => l.NodeId == node.Id);
            var view = new BtAuthoringNodeView(
                node,
                _host.ResolveNodeDisplayName(node),
                string.Equals(node.Id, _host.Document.Tree.RootNodeId, StringComparison.Ordinal),
                layout?.X ?? 0f,
                layout?.Y ?? 0f);
            _nodeViewsById[node.Id] = view;
            AddElement(view);
            RefreshNodeTitle(view);
        }

        public void RefreshNodeTitles()
        {
            foreach (var view in _nodeViewsById.Values) RefreshNodeTitle(view);
        }

        private void RefreshNodeTitle(BtAuthoringNodeView view)
        {
            var title = _host.ResolveNodeDisplayName(view.Node);
            var order = _host.ResolveChildOrder(view.Node.Id);
            if (order > 0) title = order + ". " + title;
            if (view.Node.Type == BtBuiltInNodeTypes.Subtree
                && view.Node.Properties.TryGet(BtSubtreeNode.TreeIdProperty, out var treeIdValue)
                && treeIdValue.TryGetString(out var refTreeId)
                && !string.IsNullOrEmpty(refTreeId))
            {
                title += " -> " + refTreeId;
            }
            view.title = string.Equals(view.Node.Id, _host.Document.Tree.RootNodeId, StringComparison.Ordinal)
                ? "★ " + title
                : title;
        }

        public void FocusNode(string nodeId)
        {
            if (!_nodeViewsById.TryGetValue(nodeId, out var view)) return;
            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }

        public bool FocusFirstMatch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            foreach (var node in _host.Document.Tree.Nodes)
            {
                if (!node.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !node.Type.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !_host.ResolveNodeDisplayName(node).Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FocusNode(node.Id);
                return true;
            }
            return false;
        }

        public void Connect(string childId, string parentId)
        {
            var parent = FindNodeView(parentId);
            var child = FindNodeView(childId);
            if (parent == null || child == null) return;

            if (parent.OutputPort == null || child.InputPort == null) return;
            var edge = parent.OutputPort.ConnectTo(child.InputPort);
            if (edge != null) AddElement(edge);
        }

        public Vector2 GetViewportCenter()
        {
            return contentViewContainer.WorldToLocal(worldBound.center);
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
                view.ApplyRuntimeState(state);
            }
        }

        public void ClearNodeStates()
        {
            foreach (var view in _nodeViewsById.Values)
            {
                view.ClearRuntimeState();
            }
        }

        public void ClearErrorNodes()
        {
            foreach (var view in _nodeViewsById.Values) view.SetErrorBorder(false);
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

        private BtAuthoringNodeView? FindNodeView(string nodeId)
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

        public void AddNotes(IEnumerable<BtAuthoringNoteData> notes)
        {
            if (notes == null) return;
            foreach (var note in notes)
            {
                if (note == null) continue;
                AddElement(new BtAuthoringNoteView(note, _host));
            }
        }

        public void AddNote(BtAuthoringNoteData note)
        {
            AddElement(new BtAuthoringNoteView(note, _host));
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
}
