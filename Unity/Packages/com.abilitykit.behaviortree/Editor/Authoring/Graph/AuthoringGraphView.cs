#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
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
    internal interface IAuthoringGraphHost
    {
        AuthoringSourceDocument Document { get; }
        bool IsReadOnly { get; }
        void OnGraphSelectionChanged(NodeDefinition? node);
        void RecordChange();
        void RecordChange(string beforeChangeSnapshot);
        bool CanConnect(string childId, string parentId, out string error);
        void SetConnected(string childId, string parentId, bool connected);
        string ResolveNodeDisplayName(NodeDefinition node);
        int ResolveChildOrder(string nodeId);
        Vector2 ScreenToGraphPosition(Vector2 screenPosition);
        void AddNode(NodeDescriptor descriptor, Vector2 graphPosition);
    }

    /// <summary>GraphView 实现：节点视图 + 边回调 + 描述符驱动创建菜单。</summary>
    internal sealed class AuthoringGraphView : GraphView
    {
        private readonly IAuthoringGraphHost _host;
        private readonly Dictionary<string, AuthoringNodeView> _nodeViewsById =
            new(System.StringComparer.Ordinal);
        private readonly Dictionary<Group, AuthoringGroupData> _groupDataByElement = new();
        private readonly Dictionary<string, Edge> _edgeViewsByKey = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeObservationEdges = new(StringComparer.Ordinal);
        private readonly HashSet<string> _projectionNodeIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _nextObservationEdges = new(StringComparer.Ordinal);
        private readonly List<ObservationOverlay> _overlayBuffer = new();
        private ObservationSnapshot? _projectedObservationSnapshot;

        public AuthoringGraphView(IAuthoringGraphHost host)
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
            canPasteSerializedData = data => !_host.IsReadOnly && AuthoringMutationService.CanDeserializeSubgraph(data);
            unserializeAndPaste = PasteSerializedData;
            RegisterCallback<MouseUpEvent>(_ => NotifySelectionChanged());
            RegisterCallback<KeyUpEvent>(_ => NotifySelectionChanged());
        }

        private void AddSearchWindow()
        {
            var provider = ScriptableObject.CreateInstance<NodeSearchProvider>();
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
                    if (edge.input.node is not AuthoringNodeView childView
                        || edge.output.node is not AuthoringNodeView parentView)
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
            var beforeChangeSnapshot = NeedsRecordedMutation(change)
                ? AuthoringJson.Save(_host.Document)
                : string.Empty;
            var changedDocument = false;

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.input.node is AuthoringNodeView childView
                        && edge.output.node is AuthoringNodeView parentView)
                    {
                        changedDocument |= AuthoringMutationService.SetConnected(
                            _host.Document,
                            childView.Node.Id,
                            parentView.Node.Id,
                            true);
                        _edgeViewsByKey[EdgeKey(childView.Node.Id, parentView.Node.Id)] = edge;
                    }
                }
            }
            if (change.elementsToRemove != null)
            {
                var removedNodeIds = new List<string>();
                var removedGroupIds = new List<string>();
                var removedNoteIds = new List<string>();
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge
                        && edge.input.node is AuthoringNodeView childView
                        && edge.output.node is AuthoringNodeView parentView)
                    {
                        _edgeViewsByKey.Remove(EdgeKey(childView.Node.Id, parentView.Node.Id));
                        changedDocument |= AuthoringMutationService.SetConnected(
                            _host.Document,
                            childView.Node.Id,
                            parentView.Node.Id,
                            false);
                    }
                    if (element is AuthoringNodeView nodeView)
                    {
                        removedNodeIds.Add(nodeView.Node.Id);
                        _nodeViewsById.Remove(nodeView.Node.Id);
                    }
                    if (element is Group group)
                    {
                        if (_groupDataByElement.TryGetValue(group, out var groupData))
                        {
                            removedGroupIds.Add(groupData.Id);
                            _groupDataByElement.Remove(group);
                        }
                    }
                    if (element is AuthoringNoteView noteView)
                    {
                        removedNoteIds.Add(noteView.Data.Id);
                    }
                }
                if (removedNodeIds.Count > 0 || removedGroupIds.Count > 0 || removedNoteIds.Count > 0)
                {
                    AuthoringMutationService.DeleteSelection(
                        _host.Document,
                        removedNodeIds,
                        removedGroupIds,
                        removedNoteIds);
                    RemoveCachedEdgesTouching(removedNodeIds);
                    changedDocument = true;
                }
            }
            if (change.movedElements != null)
            {
                // 拖动后把画布坐标写回文档，Save 时持久化
                foreach (var element in change.movedElements)
                {
                    if (element is AuthoringNodeView nodeView)
                    {
                        var position = nodeView.GetPosition();
                        changedDocument |= AuthoringMutationService.UpdateNodeLayout(
                            _host.Document,
                            nodeView.Node.Id,
                            position.x,
                            position.y);
                    }
                    else if (element is Group group && _groupDataByElement.TryGetValue(group, out var groupData))
                    {
                        changedDocument |= AuthoringMutationService.UpdateGroupLayout(groupData, group.GetPosition());
                    }
                    else if (element is AuthoringNoteView noteView)
                    {
                        changedDocument |= AuthoringMutationService.UpdateNoteLayout(noteView.Data, noteView.GetPosition());
                    }
                }
            }
            if (changedDocument && !string.IsNullOrEmpty(beforeChangeSnapshot))
                _host.RecordChange(beforeChangeSnapshot);
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
                if (output.node is AuthoringNodeView parent
                    && input.node is AuthoringNodeView child
                    && _host.CanConnect(child.Node.Id, parent.Node.Id, out _))
                {
                    compatible.Add(candidate);
                }
            }
            return compatible;
        }

        private static bool NeedsRecordedMutation(GraphViewChange change)
        {
            return (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                || (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
                || (change.movedElements != null && change.movedElements.Count > 0);
        }

        private void RemoveCachedEdgesTouching(IEnumerable<string> nodeIds)
        {
            var removed = new HashSet<string>(nodeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (removed.Count == 0) return;
            foreach (var key in _edgeViewsByKey.Keys.ToList())
            {
                var separator = key.IndexOf('\u001f');
                if (separator < 0) continue;
                var childId = key.Substring(0, separator);
                var parentId = key.Substring(separator + 1);
                if (removed.Contains(childId) || removed.Contains(parentId))
                    _edgeViewsByKey.Remove(key);
            }
        }

        private string SerializeElements(IEnumerable<GraphElement> elements)
        {
            if (_host.IsReadOnly || elements == null) return string.Empty;
            var selectedNodes = elements.OfType<AuthoringNodeView>().Select(view => view.Node.Id);
            var selectedGroups = elements.OfType<Group>()
                .Select(group => _groupDataByElement.TryGetValue(group, out var data) ? data.Id : "")
                .Where(id => !string.IsNullOrWhiteSpace(id));
            var selectedNotes = elements.OfType<AuthoringNoteView>().Select(view => view.Data.Id);
            return AuthoringMutationService.SerializeSubgraph(
                _host.Document,
                selectedNodes,
                selectedGroups,
                selectedNotes);
        }

        private void PasteSerializedData(string operationName, string serializedData)
        {
            if (_host.IsReadOnly) return;
            if (!AuthoringMutationService.TryDeserializeSubgraph(serializedData, out var clipboard)) return;

            var before = AuthoringJson.Save(_host.Document);
            var result = AuthoringMutationService.PasteSubgraph(_host.Document, clipboard, GetViewportCenter());
            if (!result.Changed) return;
            _host.RecordChange(before);
            RenderPasteResult(result);
        }

        public bool DuplicateSelection()
        {
            var serialized = SerializeElements(selection.OfType<GraphElement>());
            if (!AuthoringMutationService.TryDeserializeSubgraph(serialized, out var clipboard)) return false;
            var before = AuthoringJson.Save(_host.Document);
            var result = AuthoringMutationService.PasteSubgraph(_host.Document, clipboard);
            if (!result.Changed) return false;
            _host.RecordChange(before);
            RenderPasteResult(result);
            return true;
        }

        private void RenderPasteResult(AuthoringClipboardPasteResult result)
        {
            ClearSelection();
            foreach (var nodeId in result.CreatedNodeIds)
            {
                var node = _host.Document.Tree.Nodes.Find(item =>
                    string.Equals(item.Id, nodeId, StringComparison.Ordinal));
                if (node == null) continue;
                AddNodeView(node);
                if (_nodeViewsById.TryGetValue(node.Id, out var view)) AddToSelection(view);
            }

            foreach (var nodeId in result.CreatedNodeIds)
            {
                var parent = _host.Document.Tree.Nodes.Find(item =>
                    string.Equals(item.Id, nodeId, StringComparison.Ordinal));
                if (parent == null) continue;
                foreach (var childId in parent.ChildIds)
                    Connect(childId, parent.Id);
            }

            AddGroups(_host.Document.Groups.Where(group => result.CreatedGroupIds.Contains(group.Id)));
            AddNotes(_host.Document.Notes.Where(note => result.CreatedNoteIds.Contains(note.Id)));
            RefreshNodeTitles();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            var selected = selection.OfType<AuthoringNodeView>().FirstOrDefault();
            _host.OnGraphSelectionChanged(selected?.Node);
        }

        public void AddNodeView(NodeDefinition node)
        {
            var layout = _host.Document.Layout.Find(l => l.NodeId == node.Id);
            var view = new AuthoringNodeView(
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

        private void RefreshNodeTitle(AuthoringNodeView view)
        {
            var title = _host.ResolveNodeDisplayName(view.Node);
            var order = _host.ResolveChildOrder(view.Node.Id);
            if (order > 0) title = order + ". " + title;
            if (view.Node.Type == BuiltInNodeTypes.Subtree
                && view.Node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var treeIdValue)
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
            NotifySelectionChanged();
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
            if (edge != null)
            {
                _edgeViewsByKey[EdgeKey(childId, parentId)] = edge;
                AddElement(edge);
            }
        }

        public Vector2 GetViewportCenter()
        {
            return contentViewContainer.WorldToLocal(worldBound.center);
        }

        public IReadOnlyList<string> GetSelectedNodeIds()
        {
            var nodeIds = new List<string>();
            foreach (var view in selection.OfType<AuthoringNodeView>())
            {
                if (!string.IsNullOrWhiteSpace(view.Node.Id)) nodeIds.Add(view.Node.Id);
            }
            return nodeIds;
        }

        public IReadOnlyDictionary<string, AuthoringLayoutSize> CaptureNodeSizesForLayout()
        {
            var sizes = new Dictionary<string, AuthoringLayoutSize>(StringComparer.Ordinal);
            foreach (var pair in _nodeViewsById)
            {
                var rect = pair.Value.GetPosition();
                sizes[pair.Key] = new AuthoringLayoutSize(rect.width, rect.height);
            }
            return sizes;
        }

        public bool TryApplyLayoutResult(AuthoringLayoutResult result, IEnumerable<AuthoringGroupData> groups)
        {
            if (result == null) return false;
            foreach (var nodeId in result.NodePositions.Keys)
            {
                if (!_nodeViewsById.ContainsKey(nodeId)) return false;
            }

            foreach (var pair in result.NodePositions)
            {
                var view = _nodeViewsById[pair.Key];
                var rect = view.GetPosition();
                view.SetPosition(new Rect(pair.Value.X, pair.Value.Y, rect.width, rect.height));
            }

            if (groups != null)
            {
                foreach (var groupData in groups)
                {
                    var groupElement = FindGroupElement(groupData);
                    if (groupElement == null) return false;
                    groupElement.SetPosition(new Rect(
                        groupData.X,
                        groupData.Y,
                        Mathf.Max(groupData.Width, 120f),
                        Mathf.Max(groupData.Height, 60f)));
                }
            }

            return true;
        }

        public void ClearAll()
        {
            _nodeViewsById.Clear();
            _groupDataByElement.Clear();
            _edgeViewsByKey.Clear();
            _activeObservationEdges.Clear();
            _projectedObservationSnapshot = null;
            foreach (var element in graphElements.ToList()) RemoveElement(element);
            _host.OnGraphSelectionChanged(null);
        }

        /// <summary>观察模式：把运行时节点状态着色到画布（运行中加边框高亮）。</summary>
        public void ApplyNodeStates(IEnumerable<NodeDebugInfo> states)
        {
            foreach (var state in states)
            {
                if (state == null || !_nodeViewsById.TryGetValue(state.NodeId, out var view)) continue;
                view.ApplyRuntimeState(state);
            }
        }

        public void ApplyObservationProjection(
            ObservationSnapshot snapshot,
            ObservationDiff? diff,
            ObservationContributorRegistry contributors)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            contributors ??= ObservationContributorRegistry.Default;
            if (_projectedObservationSnapshot != null
                && ReferenceEquals(_projectedObservationSnapshot, snapshot))
            {
                return;
            }

            var targetNodeIds = BuildProjectionNodeSet(snapshot, diff);
            foreach (var nodeId in targetNodeIds)
            {
                if (!_nodeViewsById.TryGetValue(nodeId, out var view)) continue;
                if (!snapshot.TryGetNode(nodeId, out var info))
                {
                    view.ClearObservationState();
                    continue;
                }

                contributors.CollectOverlays(new ObservationOverlayContext(
                    snapshot.InstanceId,
                    snapshot,
                    info),
                    _overlayBuffer);
                for (var index = _overlayBuffer.Count - 1; index >= 0; index--)
                {
                    if (!string.Equals(_overlayBuffer[index].NodeId, nodeId, StringComparison.Ordinal))
                        _overlayBuffer.RemoveAt(index);
                }
                view.ApplyObservation(info, _overlayBuffer, snapshot.IsActive(nodeId));
            }

            ProjectActiveEdges(snapshot);
            _projectedObservationSnapshot = snapshot;
        }

        public void ClearNodeStates()
        {
            foreach (var view in _nodeViewsById.Values)
            {
                view.ClearObservationState();
            }
            foreach (var edge in _edgeViewsByKey.Values) SetObservationEdgeActive(edge, false);
            _activeObservationEdges.Clear();
            _projectedObservationSnapshot = null;
        }

        private HashSet<string> BuildProjectionNodeSet(ObservationSnapshot snapshot, ObservationDiff? diff)
        {
            _projectionNodeIds.Clear();
            var previousSnapshot = _projectedObservationSnapshot;
            var fullProjection = previousSnapshot == null
                || diff == null
                || diff.AddedNodes.Count > 0
                || diff.RemovedNodes.Count > 0
                || previousSnapshot.InstanceId != snapshot.InstanceId
                || !string.Equals(previousSnapshot.TreeId, snapshot.TreeId, StringComparison.Ordinal);

            if (fullProjection)
            {
                foreach (var nodeId in _nodeViewsById.Keys) _projectionNodeIds.Add(nodeId);
                foreach (var node in snapshot.Nodes) _projectionNodeIds.Add(node.NodeId);
                return _projectionNodeIds;
            }

            if (diff == null || previousSnapshot == null) return _projectionNodeIds;
            foreach (var nodeId in diff.ChangedNodeIds) _projectionNodeIds.Add(nodeId);
            foreach (var nodeId in snapshot.ActiveNodeIds) _projectionNodeIds.Add(nodeId);
            foreach (var nodeId in previousSnapshot.ActiveNodeIds) _projectionNodeIds.Add(nodeId);
            return _projectionNodeIds;
        }

        private void ProjectActiveEdges(ObservationSnapshot snapshot)
        {
            var next = BuildActiveEdgeSet(snapshot);
            foreach (var edgeKey in _activeObservationEdges)
            {
                if (next.Contains(edgeKey)) continue;
                if (_edgeViewsByKey.TryGetValue(edgeKey, out var edge)) SetObservationEdgeActive(edge, false);
            }
            foreach (var edgeKey in next)
            {
                if (_activeObservationEdges.Contains(edgeKey)) continue;
                if (_edgeViewsByKey.TryGetValue(edgeKey, out var edge)) SetObservationEdgeActive(edge, true);
            }
            _activeObservationEdges.Clear();
            foreach (var edgeKey in next) _activeObservationEdges.Add(edgeKey);
        }

        private HashSet<string> BuildActiveEdgeSet(ObservationSnapshot snapshot)
        {
            _nextObservationEdges.Clear();
            foreach (var parent in _host.Document.Tree.Nodes)
            {
                if (!snapshot.TryGetNode(parent.Id, out var parentInfo)
                    || parentInfo.OnStackCount <= 0)
                {
                    continue;
                }

                if (parentInfo.RunningChildIndex >= 0
                    && parentInfo.RunningChildIndex < parent.ChildIds.Count)
                {
                    _nextObservationEdges.Add(EdgeKey(parent.ChildIds[parentInfo.RunningChildIndex], parent.Id));
                    continue;
                }

                foreach (var childId in parent.ChildIds)
                {
                    if (snapshot.TryGetNode(childId, out var childInfo)
                        && childInfo.OnStackCount > 0)
                    {
                        _nextObservationEdges.Add(EdgeKey(childId, parent.Id));
                    }
                }
            }
            return _nextObservationEdges;
        }

        private static string EdgeKey(string childId, string parentId) => childId + "\u001f" + parentId;

        private static void SetObservationEdgeActive(Edge edge, bool active)
        {
            if (edge == null) return;
            var color = active ? new Color(1f, 0.82f, 0.25f) : new Color(0.55f, 0.55f, 0.55f);
            edge.edgeControl.inputColor = color;
            edge.edgeControl.outputColor = color;
            edge.edgeControl.edgeWidth = active ? 4 : 2;
            edge.edgeControl.MarkDirtyRepaint();
        }

        internal AuthoringNodeView? GetNodeViewForTests(string nodeId) => FindNodeView(nodeId);

        internal Edge? GetEdgeForTests(string childId, string parentId) =>
            _edgeViewsByKey.TryGetValue(EdgeKey(childId, parentId), out var edge) ? edge : null;

        public void ClearErrorNodes()
        {
            foreach (var view in _nodeViewsById.Values) view.SetErrorBorder(false);
        }

        /// <summary>按结构化诊断明确给出的节点 ID 标记错误边框。</summary>
        public void MarkErrorNodes(IEnumerable<string> nodeIds)
        {
            var marked = new HashSet<string>(
                nodeIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (var pair in _nodeViewsById)
                pair.Value.SetErrorBorder(marked.Contains(pair.Key));
        }

        private AuthoringNodeView? FindNodeView(string nodeId)
        {
            return _nodeViewsById.TryGetValue(nodeId, out var view) ? view : null;
        }

        /// <summary>按文档分组数据渲染分组框，并把成员节点加入分组。</summary>
        public void AddGroups(IEnumerable<AuthoringGroupData> groups)
        {
            if (groups == null) return;
            foreach (var groupData in groups)
            {
                if (groupData == null) continue;
                AddGroup(groupData);
            }
        }

        public void AddNotes(IEnumerable<AuthoringNoteData> notes)
        {
            if (notes == null) return;
            foreach (var note in notes)
            {
                if (note == null) continue;
                AddElement(new AuthoringNoteView(note, _host));
            }
        }

        public void AddNote(AuthoringNoteData note)
        {
            AddElement(new AuthoringNoteView(note, _host));
        }

        public void AddGroup(AuthoringGroupData groupData)
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

        private Group? FindGroupElement(AuthoringGroupData groupData)
        {
            foreach (var pair in _groupDataByElement)
            {
                if (ReferenceEquals(pair.Value, groupData)
                    || string.Equals(pair.Value.Id, groupData.Id, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }
            return null;
        }
    }
}
