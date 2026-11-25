using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Visualization;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// 管理 HFSM 编辑器窗口的当前状态，跟踪选择、导航和编辑状态。
    /// </summary>
    public class EditorContext
    {
        private GraphAsset _graphAsset;

        /// <summary>
        /// 当前打开的图资产。
        /// </summary>
        public GraphAsset GraphAsset
        {
            get => _graphAsset;
            set
            {
                if (_graphAsset != value)
                {
                    _graphAsset = value;
                    Reset();
                }
            }
        }

        /// <summary>
        /// 当前正在查看或编辑的状态机。
        /// </summary>
        public StateMachineNode CurrentStateMachine { get; private set; }

        // ------------------------------------------------------------------
        // 运行时观察：把 LiveRegistry 里绑定实例的实时快照着色到编辑画布
        // ------------------------------------------------------------------

        private string _observedInstanceName;
        private FsmSnapshot _liveSnapshot;

        /// <summary>
        /// 画布当前是否已绑定到活动运行时实例。
        /// </summary>
        public bool IsObserving => !string.IsNullOrEmpty(_observedInstanceName);

        /// <summary>
        /// 绑定的活动实例名称；未观察时为空。
        /// </summary>
        public string ObservedInstanceName => _observedInstanceName;

        /// <summary>
        /// 最新的实时快照；未观察时为 null。
        /// </summary>
        public FsmSnapshot LiveSnapshot => _liveSnapshot;

        /// <summary>
        /// 画布当前显示其子节点的状态机。实时状态路径为 "{machineId}/{stateId}"，嵌套状态机使用此值作为节点 ID 的前缀。
        /// </summary>
        public string CurrentMachineId =>
            CurrentStateMachine != null
                ? CurrentStateMachine.Id
                : (_graphAsset != null ? _graphAsset.RootStateMachineId : string.Empty);

        public void BeginObserve(string instanceName) => _observedInstanceName = instanceName ?? string.Empty;

        public void EndObserve()
        {
            _observedInstanceName = null;
            _liveSnapshot = null;
        }

        public void SetLiveSnapshot(FsmSnapshot snapshot) => _liveSnapshot = snapshot;

        /// <summary>
        /// 节点位于实时运行栈上时返回 true。
        /// </summary>
        public bool IsNodeActive(string nodeId) => LivePathsContain(_liveSnapshot?.activeStatePaths, nodeId);

        /// <summary>
        /// 节点是延迟转换的待进入目标时返回 true。
        /// </summary>
        public bool IsNodeEntering(string nodeId) => LivePathsContain(_liveSnapshot?.pendingStatePaths, nodeId);

        /// <summary>
        /// 节点正在等待所属状态机批准退出时返回 true。
        /// </summary>
        public bool IsNodeExiting(string nodeId) => LivePathsContain(_liveSnapshot?.exitingStatePaths, nodeId);

        private bool LivePathsContain(List<string> paths, string nodeId)
        {
            /*
             * 同时检查绑定状态，防止未绑定时残留快照影响画布着色。
             */
            if (!IsObserving || paths == null || string.IsNullOrEmpty(nodeId)) return false;
            var machineId = CurrentMachineId;
            if (string.IsNullOrEmpty(machineId)) return false;
            return paths.Contains(machineId + "/" + nodeId);
        }

        /// <summary>
        /// 从根状态机到当前状态机的节点路径。
        /// </summary>
        public List<StateMachineNode> StateMachinePath { get; } = new List<StateMachineNode>();

        /// <summary>
        /// 当前选中的节点。
        /// </summary>
        public List<NodeBase> SelectedNodes { get; } = new List<NodeBase>();

        /// <summary>
        /// 当前选中的转换边。
        /// </summary>
        public TransitionEdge SelectedEdge { get; set; }

        /// <summary>
        /// 当前状态机中待显示的子节点。
        /// </summary>
        public List<NodeBase> CurrentChildNodes { get; } = new List<NodeBase>();

        /// <summary>
        /// 当前状态机中待显示的转换。
        /// </summary>
        public List<TransitionEdge> CurrentTransitions { get; } = new List<TransitionEdge>();

        /// <summary>
        /// 当前是否正在创建转换预览。
        /// </summary>
        public bool IsPreviewTransition { get; set; }

        /// <summary>
        /// 创建转换时的源节点。
        /// </summary>
        public NodeBase TransitionSourceNode { get; set; }

        /// <summary>
        /// 创建转换时的目标节点。
        /// </summary>
        public NodeBase TransitionTargetNode { get; set; }

        /// <summary>
        /// 图视图的缩放系数。
        /// </summary>
        public float ZoomFactor { get; set; } = 1.0f;

        /// <summary>
        /// 图视图的平移偏移。
        /// </summary>
        public Vector2 PanOffset { get; set; } = Vector2.zero;

        /// <summary>
        /// 上下文变化时触发的事件。
        /// </summary>
        public event Action OnContextChanged;

        /// <summary>
        /// 当前状态机变化时触发的事件。
        /// </summary>
        public event Action OnStateMachineChanged;

        /// <summary>
        /// 选择变化时触发的事件。
        /// </summary>
        public event Action OnSelectionChanged;

        public bool FocusNode(string nodeId)
        {
            var node = _graphAsset?.GetNodeById(nodeId);
            if (node == null) return false;

            var parent = string.IsNullOrEmpty(node.ParentStateMachineId)
                ? node as StateMachineNode
                : _graphAsset.GetNodeById<StateMachineNode>(node.ParentStateMachineId);
            if (parent != null && !NavigateToMachine(parent)) return false;

            SetSelection(node);
            CenterViewOnNode(node);
            return true;
        }

        public bool FocusTransition(string transitionId)
        {
            var edge = _graphAsset?.GetEdgeById(transitionId);
            if (edge == null) return false;

            StateMachineNode owner = null;
            foreach (var machine in _graphAsset.GetNodesOfType<StateMachineNode>())
            {
                if (machine.TransitionIds.Contains(transitionId) ||
                    machine.AnyStateTransitionIds.Contains(transitionId))
                {
                    owner = machine;
                    break;
                }
            }

            if (owner != null && !NavigateToMachine(owner)) return false;
            SelectEdge(edge);
            var source = _graphAsset.GetNodeById(edge.SourceNodeId);
            if (source != null) CenterViewOnNode(source);
            return true;
        }

        public bool NavigateToMachine(StateMachineNode machine)
        {
            if (_graphAsset == null || machine == null) return false;

            var path = new List<StateMachineNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = machine;
            while (current != null)
            {
                if (!visited.Add(current.Id)) return false;
                path.Add(current);
                current = string.IsNullOrEmpty(current.ParentStateMachineId)
                    ? null
                    : _graphAsset.GetNodeById<StateMachineNode>(current.ParentStateMachineId);
            }

            path.Reverse();
            if (path.Count == 0 || path[0].Id != _graphAsset.RootStateMachineId) return false;
            StateMachinePath.Clear();
            StateMachinePath.AddRange(path);
            CurrentStateMachine = machine;
            ClearSelection();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 重置上下文，显示根状态机。
        /// </summary>
        public void Reset()
        {
            EndObserve();

            if (_graphAsset == null)
            {
                CurrentStateMachine = null;
                StateMachinePath.Clear();
                ClearCurrentView();
                OnContextChanged?.Invoke();
                return;
            }

            var root = _graphAsset.GetRootStateMachine();
            if (root != null)
            {
                StateMachinePath.Clear();
                StateMachinePath.Add(root);
                CurrentStateMachine = root;
            }

            ClearCurrentView();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
        }

        /// <summary>
        /// 进入嵌套状态机。
        /// </summary>
        public void NavigateInto(StateMachineNode stateMachine)
        {
            if (stateMachine == null || stateMachine.NodeType != GraphNodeType.StateMachine)
                return;

            StateMachinePath.Add(stateMachine);
            CurrentStateMachine = stateMachine;
            ClearSelection();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
        }

        /// <summary>
        /// 返回父状态机。
        /// </summary>
        public void NavigateBack()
        {
            if (StateMachinePath.Count <= 1)
                return;

            StateMachinePath.RemoveAt(StateMachinePath.Count - 1);
            CurrentStateMachine = StateMachinePath[StateMachinePath.Count - 1];
            ClearSelection();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
        }

        /// <summary>
        /// 按路径索引导航到指定状态机。
        /// </summary>
        public void NavigateTo(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= StateMachinePath.Count)
                return;

            while (StateMachinePath.Count > pathIndex + 1)
            {
                StateMachinePath.RemoveAt(StateMachinePath.Count - 1);
            }

            CurrentStateMachine = StateMachinePath[pathIndex];
            ClearSelection();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
        }

        /// <summary>
        /// 获取用于面包屑导航显示的路径字符串。
        /// </summary>
        public string GetPathString()
        {
            if (StateMachinePath.Count == 0)
                return "无";

            var parts = new List<string>();
            foreach (var sm in StateMachinePath)
            {
                parts.Add(sm.DisplayName);
            }
            return string.Join(" > ", parts);
        }

        /// <summary>
        /// 从指定节点开始创建转换。
        /// </summary>
        public void StartTransitionPreview(NodeBase fromNode)
        {
            if (fromNode == null)
                return;

            IsPreviewTransition = true;
            TransitionSourceNode = fromNode;
            TransitionTargetNode = null;
        }

        /// <summary>
        /// 更新转换预览的目标。
        /// </summary>
        public void UpdateTransitionPreviewTarget(NodeBase toNode)
        {
            TransitionTargetNode = toNode;
        }

        /// <summary>
        /// 完成转换创建。
        /// </summary>
        public TransitionEdge CompleteTransition()
        {
            if (!IsPreviewTransition || TransitionSourceNode == null || TransitionTargetNode == null)
            {
                CancelTransition();
                return null;
            }

            if (TransitionSourceNode.Id == TransitionTargetNode.Id)
            {
                CancelTransition();
                return null;
            }

            Undo.RecordObject(_graphAsset, "创建转换");
            var edge = _graphAsset.CreateTransition(TransitionSourceNode.Id, TransitionTargetNode.Id);

            /*
             * 将转换加入当前状态机。
             */
            CurrentStateMachine?.AddTransition(edge.Id);

            CancelTransition();
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            OnSelectionChanged?.Invoke();
            return edge;
        }

        /// <summary>
        /// 取消当前转换预览。
        /// </summary>
        public void CancelTransition()
        {
            IsPreviewTransition = false;
            TransitionSourceNode = null;
            TransitionTargetNode = null;
        }

        /// <summary>
        /// 从选择中移除节点。
        /// </summary>
        public void RemoveFromSelection(NodeBase node)
        {
            if (SelectedNodes.Remove(node))
            {
                OnSelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// 将选择设为单个节点。
        /// </summary>
        public void SetSelection(NodeBase node)
        {
            SelectedNodes.Clear();
            if (node != null)
            {
                SelectedNodes.Add(node);
            }
            SelectedEdge = null;
            OnSelectionChanged?.Invoke();
        }

        /// <summary>
        /// 清除全部选择。
        /// </summary>
        public void ClearSelection()
        {
            if (SelectedNodes.Count > 0 || SelectedEdge != null)
            {
                SelectedNodes.Clear();
                SelectedEdge = null;
                OnSelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// 选择转换边。
        /// </summary>
        public void SelectEdge(TransitionEdge edge)
        {
            SelectedNodes.Clear();
            SelectedEdge = edge;
            OnSelectionChanged?.Invoke();
        }

        /// <summary>
        /// 获取是否存在选中节点或转换边。
        /// </summary>
        public bool HasSelection => SelectedNodes.Count > 0 || SelectedEdge != null;

        /// <summary>
        /// 获取指定节点是否已选中。
        /// </summary>
        public bool IsSelected(NodeBase node)
        {
            return SelectedNodes.Contains(node);
        }

        /// <summary>
        /// 获取首个选中节点；未选择节点时返回 null。
        /// </summary>
        public NodeBase FirstSelectedNode => SelectedNodes.Count > 0 ? SelectedNodes[0] : null;

        /// <summary>
        /// 在当前状态机中创建状态。
        /// </summary>
        public StateNode CreateState(string name, Vector2 position)
        {
            if (_graphAsset == null || CurrentStateMachine == null)
                return null;

            Undo.RecordObject(_graphAsset, "创建状态");
            var state = _graphAsset.CreateState(name, position);
            CurrentStateMachine.AddChildNode(state.Id);
            state.ParentStateMachineId = CurrentStateMachine.Id;
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            CenterViewOnNode(state);
            return state;
        }

        /// <summary>
        /// 在当前状态机中创建子状态机。
        /// </summary>
        public StateMachineNode CreateStateMachine(string name, Vector2 position)
        {
            if (_graphAsset == null || CurrentStateMachine == null)
                return null;

            Undo.RecordObject(_graphAsset, "创建状态机");
            var sm = _graphAsset.CreateStateMachine(name, position);
            CurrentStateMachine.AddChildNode(sm.Id);
            sm.ParentStateMachineId = CurrentStateMachine.Id;
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            CenterViewOnNode(sm);
            return sm;
        }

        /// <summary>
        /// 将视图居中到指定节点。
        /// </summary>
        public void CenterViewOnNode(NodeBase node)
        {
            if (node == null)
                return;

            if (_viewBounds.width <= 0)
                return;

            Vector2 nodeCenter = node.Position + node.Size * 0.5f;
            Vector2 viewCenter = new Vector2(_viewBounds.width / 2, _viewBounds.height / 2);
            /*
             * 由 screenPos = contentPos + panOffset，令 nodeCenter 对齐 viewCenter，
             * 得到 panOffset = viewCenter - nodeCenter。
             */
            PanOffset = viewCenter - nodeCenter;
            ZoomFactor = 1f;
            OnContextChanged?.Invoke();
        }

        private bool ViewBoundsSet => _viewBounds.width > 0;
        private Rect _viewBounds;

        /// <summary>
        /// 更新坐标计算使用的视图边界。
        /// </summary>
        public void UpdateViewBounds(Rect viewBounds)
        {
            _viewBounds = viewBounds;
        }

        /// <summary>
        /// 删除指定节点。
        /// </summary>
        public void DeleteNode(NodeBase node)
        {
            if (_graphAsset == null || node == null)
                return;

            Undo.RecordObject(_graphAsset, "删除节点");

            /*
             * 从父状态机移除。
             */
            if (!string.IsNullOrEmpty(node.ParentStateMachineId))
            {
                var parent = _graphAsset.GetNodeById<StateMachineNode>(node.ParentStateMachineId);
                parent?.RemoveChildNode(node.Id);
            }

            _graphAsset.RemoveNode(node);
            RemoveFromSelection(node);
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
        }

        /// <summary>
        /// 删除全部选中节点。
        /// </summary>
        public void DeleteSelectedNodes()
        {
            if (_graphAsset == null)
                return;

            Undo.RecordObject(_graphAsset, "删除所选节点");

            /*
             * 复制列表，避免在遍历过程中修改原集合。
             */
            var nodesToDelete = new List<NodeBase>(SelectedNodes);
            foreach (var node in nodesToDelete)
            {
                if (!string.IsNullOrEmpty(node.ParentStateMachineId))
                {
                    var parent = _graphAsset.GetNodeById<StateMachineNode>(node.ParentStateMachineId);
                    parent?.RemoveChildNode(node.Id);
                }
                _graphAsset.RemoveNode(node);
            }

            SelectedNodes.Clear();
            SelectedEdge = null;
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
        }

        /// <summary>
        /// 删除指定转换边。
        /// </summary>
        public void DeleteEdge(TransitionEdge edge)
        {
            if (_graphAsset == null || edge == null)
                return;

            Undo.RecordObject(_graphAsset, "删除转换");

            /*
             * 即使诊断从其他层级导航而来，也要从实际所属状态机移除。
             */
            foreach (var machine in _graphAsset.GetNodesOfType<StateMachineNode>())
            {
                machine.RemoveTransition(edge.Id);
                machine.RemoveAnyStateTransition(edge.Id);
            }

            _graphAsset.RemoveEdge(edge);

            if (SelectedEdge == edge)
            {
                SelectedEdge = null;
            }

            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
        }

        /// <summary>
        /// 删除选中的转换边。
        /// </summary>
        public void DeleteSelectedEdge()
        {
            if (SelectedEdge != null)
            {
                DeleteEdge(SelectedEdge);
            }
        }

        /// <summary>
        /// 设置当前状态机的默认状态。
        /// </summary>
        public void SetDefaultState(NodeBase state)
        {
            if (_graphAsset == null || CurrentStateMachine == null || state == null)
                return;

            Undo.RecordObject(_graphAsset, "设置默认状态");

            /*
             * 清除原默认状态。
             */
            foreach (var child in CurrentChildNodes)
            {
                child.isDefault = false;
            }

            state.isDefault = true;
            CurrentStateMachine.DefaultStateId = state.Id;
            EditorUtility.SetDirty(_graphAsset);
        }

        /// <summary>
        /// 按给定增量移动全部选中节点。
        /// </summary>
        /// <summary>
        /// 为一次节点拖动手势记录一条撤销。拖动开始时调用一次；后续逐步的 <see cref="MoveSelectedNodes"/> 调用合并到该记录，避免每次鼠标拖动事件都新增撤销。
        /// </summary>
        public void BeginNodeMove()
        {
            if (_graphAsset == null || SelectedNodes.Count == 0)
                return;

            Undo.RecordObject(_graphAsset, "移动节点");
        }

        public void MoveSelectedNodes(Vector2 delta)
        {
            if (_graphAsset == null || SelectedNodes.Count == 0)
                return;

            foreach (var node in SelectedNodes)
            {
                node.Position += delta;
            }
            EditorUtility.SetDirty(_graphAsset);
        }

        private void ClearCurrentView()
        {
            CurrentChildNodes.Clear();
            CurrentTransitions.Clear();
            ClearSelection();
            CancelTransition();
        }

        private void UpdateCurrentView()
        {
            CurrentChildNodes.Clear();
            CurrentTransitions.Clear();

            if (_graphAsset == null || CurrentStateMachine == null)
                return;

            /*
             * 获取子节点。
             */
            foreach (var childId in CurrentStateMachine.ChildNodeIds)
            {
                var node = _graphAsset.GetNodeById(childId);
                if (node != null)
                {
                    CurrentChildNodes.Add(node);
                }
            }

            /*
             * 获取转换。
             */
            foreach (var transitionId in CurrentStateMachine.TransitionIds)
            {
                var edge = _graphAsset.GetEdgeById(transitionId);
                if (edge != null)
                {
                    CurrentTransitions.Add(edge);
                }
            }
        }

        /// <summary>
        /// 刷新当前视图中应显示的子节点和转换。
        /// </summary>
        public void RefreshCurrentView()
        {
            UpdateCurrentView();
            OnContextChanged?.Invoke();
        }

        /// <summary>
        /// 获取全部可见节点的包围范围。
        /// </summary>
        public Rect GetNodesBounds()
        {
            if (CurrentChildNodes.Count == 0)
                return new Rect(0, 0, 400, 300);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (var node in CurrentChildNodes)
            {
                minX = Mathf.Min(minX, node.Position.x);
                minY = Mathf.Min(minY, node.Position.y);
                maxX = Mathf.Max(maxX, node.Position.x + node.Size.x);
                maxY = Mathf.Max(maxY, node.Position.y + node.Size.y);
            }

            return new Rect(minX - 50, minY - 50, maxX - minX + 100, maxY - minY + 100);
        }
    }
}
