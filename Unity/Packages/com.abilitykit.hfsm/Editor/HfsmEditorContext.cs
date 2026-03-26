using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityHFSM.Graph;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Editor context that manages the current state of the HFSM editor window.
    /// This class is responsible for tracking selection, navigation, and editing state.
    /// </summary>
    public class HfsmEditorContext
    {
        private HfsmGraphAsset _graphAsset;

        /// <summary>
        /// The currently opened graph asset.
        /// </summary>
        public HfsmGraphAsset GraphAsset
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
        /// The currently active state machine being viewed/edited.
        /// </summary>
        public HfsmStateMachineNode CurrentStateMachine { get; private set; }

        /// <summary>
        /// Path of state machine nodes from root to current.
        /// </summary>
        public List<HfsmStateMachineNode> StateMachinePath { get; } = new List<HfsmStateMachineNode>();

        /// <summary>
        /// Currently selected nodes.
        /// </summary>
        public List<HfsmNodeBase> SelectedNodes { get; } = new List<HfsmNodeBase>();

        /// <summary>
        /// Currently selected transition edge.
        /// </summary>
        public HfsmTransitionEdge SelectedEdge { get; set; }

        /// <summary>
        /// Child nodes of the current state machine to display.
        /// </summary>
        public List<HfsmNodeBase> CurrentChildNodes { get; } = new List<HfsmNodeBase>();

        /// <summary>
        /// Transitions of the current state machine to display.
        /// </summary>
        public List<HfsmTransitionEdge> CurrentTransitions { get; } = new List<HfsmTransitionEdge>();

        /// <summary>
        /// Whether currently creating a transition preview.
        /// </summary>
        public bool IsPreviewTransition { get; set; }

        /// <summary>
        /// The source node when creating a transition.
        /// </summary>
        public HfsmNodeBase TransitionSourceNode { get; set; }

        /// <summary>
        /// The target node when creating a transition.
        /// </summary>
        public HfsmNodeBase TransitionTargetNode { get; set; }

        /// <summary>
        /// Zoom factor for the graph view.
        /// </summary>
        public float ZoomFactor { get; set; } = 1.0f;

        /// <summary>
        /// Pan offset for the graph view.
        /// </summary>
        public Vector2 PanOffset { get; set; } = Vector2.zero;

        /// <summary>
        /// Event raised when the context changes.
        /// </summary>
        public event Action OnContextChanged;

        /// <summary>
        /// Event raised when the current state machine changes.
        /// </summary>
        public event Action OnStateMachineChanged;

        /// <summary>
        /// Event raised when selection changes.
        /// </summary>
        public event Action OnSelectionChanged;

        /// <summary>
        /// Resets the context to show the root state machine.
        /// </summary>
        public void Reset()
        {
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
        /// Navigates into a nested state machine.
        /// </summary>
        public void NavigateInto(HfsmStateMachineNode stateMachine)
        {
            if (stateMachine == null || stateMachine.NodeType != HfsmNodeType.StateMachine)
                return;

            StateMachinePath.Add(stateMachine);
            CurrentStateMachine = stateMachine;
            ClearSelection();
            UpdateCurrentView();
            OnContextChanged?.Invoke();
            OnStateMachineChanged?.Invoke();
        }

        /// <summary>
        /// Navigates back to the parent state machine.
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
        /// Navigates to a specific state machine by path index.
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
        /// Gets the path string for breadcrumb display.
        /// </summary>
        public string GetPathString()
        {
            if (StateMachinePath.Count == 0)
                return "None";

            var parts = new List<string>();
            foreach (var sm in StateMachinePath)
            {
                parts.Add(sm.DisplayName);
            }
            return string.Join(" > ", parts);
        }

        /// <summary>
        /// Starts creating a new transition from the specified node.
        /// </summary>
        public void StartTransitionPreview(HfsmNodeBase fromNode)
        {
            if (fromNode == null)
                return;

            IsPreviewTransition = true;
            TransitionSourceNode = fromNode;
            TransitionTargetNode = null;
        }

        /// <summary>
        /// Updates the transition preview target.
        /// </summary>
        public void UpdateTransitionPreviewTarget(HfsmNodeBase toNode)
        {
            TransitionTargetNode = toNode;
        }

        /// <summary>
        /// Completes the transition creation.
        /// </summary>
        public HfsmTransitionEdge CompleteTransition()
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

            Undo.RecordObject(_graphAsset, "Create Transition");
            var edge = _graphAsset.CreateTransition(TransitionSourceNode.Id, TransitionTargetNode.Id);

            // Add transition to current state machine
            CurrentStateMachine?.AddTransition(edge.Id);

            CancelTransition();
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            OnSelectionChanged?.Invoke();
            return edge;
        }

        /// <summary>
        /// Cancels the current transition preview.
        /// </summary>
        public void CancelTransition()
        {
            IsPreviewTransition = false;
            TransitionSourceNode = null;
            TransitionTargetNode = null;
        }

        /// <summary>
        /// Adds a node to the selection.
        /// </summary>
        public void AddToSelection(HfsmNodeBase node)
        {
            if (node == null || SelectedNodes.Contains(node))
                return;

            SelectedNodes.Add(node);
            SelectedEdge = null;
            OnSelectionChanged?.Invoke();
        }

        /// <summary>
        /// Removes a node from the selection.
        /// </summary>
        public void RemoveFromSelection(HfsmNodeBase node)
        {
            if (SelectedNodes.Remove(node))
            {
                OnSelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Sets the selection to a single node.
        /// </summary>
        public void SetSelection(HfsmNodeBase node)
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
        /// Clears all selections.
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
        /// Selects a transition edge.
        /// </summary>
        public void SelectEdge(HfsmTransitionEdge edge)
        {
            SelectedNodes.Clear();
            SelectedEdge = edge;
            OnSelectionChanged?.Invoke();
        }

        /// <summary>
        /// Gets whether any node is selected.
        /// </summary>
        public bool HasSelection => SelectedNodes.Count > 0 || SelectedEdge != null;

        /// <summary>
        /// Gets whether a specific node is selected.
        /// </summary>
        public bool IsSelected(HfsmNodeBase node)
        {
            return SelectedNodes.Contains(node);
        }

        /// <summary>
        /// Gets the first selected node if any.
        /// </summary>
        public HfsmNodeBase FirstSelectedNode => SelectedNodes.Count > 0 ? SelectedNodes[0] : null;

        /// <summary>
        /// Creates a new state in the current state machine.
        /// </summary>
        public HfsmStateNode CreateState(string name, Vector2 position)
        {
            if (_graphAsset == null || CurrentStateMachine == null)
                return null;

            Undo.RecordObject(_graphAsset, "Create State");
            var state = _graphAsset.CreateState(name, position);
            CurrentStateMachine.AddChildNode(state.Id);
            state.ParentStateMachineId = CurrentStateMachine.Id;
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            CenterViewOnNode(state);
            return state;
        }

        /// <summary>
        /// Creates a new state machine in the current state machine.
        /// </summary>
        public HfsmStateMachineNode CreateStateMachine(string name, Vector2 position)
        {
            if (_graphAsset == null || CurrentStateMachine == null)
                return null;

            Undo.RecordObject(_graphAsset, "Create State Machine");
            var sm = _graphAsset.CreateStateMachine(name, position);
            CurrentStateMachine.AddChildNode(sm.Id);
            sm.ParentStateMachineId = CurrentStateMachine.Id;
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
            CenterViewOnNode(sm);
            return sm;
        }

        /// <summary>
        /// Centers the view on a specific node.
        /// </summary>
        public void CenterViewOnNode(HfsmNodeBase node)
        {
            if (node == null)
                return;

            if (_viewBounds.width <= 0)
                return;

            Vector2 nodeCenter = node.Position + node.Size * 0.5f;
            Vector2 viewCenter = new Vector2(_viewBounds.width / 2, _viewBounds.height / 2);
            // screenPos = contentPos + panOffset
            // We want nodeCenter to be at viewCenter
            // So: panOffset = viewCenter - nodeCenter
            PanOffset = viewCenter - nodeCenter;
            ZoomFactor = 1f;
            OnContextChanged?.Invoke();
        }

        private bool ViewBoundsSet => _viewBounds.width > 0;
        private Rect _viewBounds;

        /// <summary>
        /// Updates the view bounds for coordinate calculations.
        /// </summary>
        public void UpdateViewBounds(Rect viewBounds)
        {
            _viewBounds = viewBounds;
        }

        /// <summary>
        /// Deletes the specified node.
        /// </summary>
        public void DeleteNode(HfsmNodeBase node)
        {
            if (_graphAsset == null || node == null)
                return;

            Undo.RecordObject(_graphAsset, "Delete Node");

            // Remove from parent state machine
            if (!string.IsNullOrEmpty(node.ParentStateMachineId))
            {
                var parent = _graphAsset.GetNodeById<HfsmStateMachineNode>(node.ParentStateMachineId);
                parent?.RemoveChildNode(node.Id);
            }

            _graphAsset.RemoveNode(node);
            RemoveFromSelection(node);
            EditorUtility.SetDirty(_graphAsset);
            UpdateCurrentView();
        }

        /// <summary>
        /// Deletes all selected nodes.
        /// </summary>
        public void DeleteSelectedNodes()
        {
            if (_graphAsset == null)
                return;

            Undo.RecordObject(_graphAsset, "Delete Selected Nodes");

            // Copy list to avoid modification during iteration
            var nodesToDelete = new List<HfsmNodeBase>(SelectedNodes);
            foreach (var node in nodesToDelete)
            {
                if (!string.IsNullOrEmpty(node.ParentStateMachineId))
                {
                    var parent = _graphAsset.GetNodeById<HfsmStateMachineNode>(node.ParentStateMachineId);
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
        /// Deletes the specified edge.
        /// </summary>
        public void DeleteEdge(HfsmTransitionEdge edge)
        {
            if (_graphAsset == null || edge == null)
                return;

            Undo.RecordObject(_graphAsset, "Delete Transition");

            // Remove from state machine
            if (CurrentStateMachine != null)
            {
                CurrentStateMachine.RemoveTransition(edge.Id);
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
        /// Deletes the selected edge.
        /// </summary>
        public void DeleteSelectedEdge()
        {
            if (SelectedEdge != null)
            {
                DeleteEdge(SelectedEdge);
            }
        }

        /// <summary>
        /// Sets the default state of the current state machine.
        /// </summary>
        public void SetDefaultState(HfsmNodeBase state)
        {
            if (_graphAsset == null || CurrentStateMachine == null || state == null)
                return;

            Undo.RecordObject(_graphAsset, "Set Default State");

            // Clear previous default
            foreach (var child in CurrentChildNodes)
            {
                child.isDefault = false;
            }

            state.isDefault = true;
            CurrentStateMachine.DefaultStateId = state.Id;
            EditorUtility.SetDirty(_graphAsset);
        }

        /// <summary>
        /// Moves a node to a new position.
        /// </summary>
        public void MoveNode(HfsmNodeBase node, Vector2 delta)
        {
            if (_graphAsset == null || node == null)
                return;

            node.Position += delta;
            EditorUtility.SetDirty(_graphAsset);
        }

        /// <summary>
        /// Moves all selected nodes by a delta.
        /// </summary>
        public void MoveSelectedNodes(Vector2 delta)
        {
            if (_graphAsset == null || SelectedNodes.Count == 0)
                return;

            Undo.RecordObject(_graphAsset, "Move Nodes");
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

            // Get child nodes
            foreach (var childId in CurrentStateMachine.ChildNodeIds)
            {
                var node = _graphAsset.GetNodeById(childId);
                if (node != null)
                {
                    CurrentChildNodes.Add(node);
                }
            }

            // Get transitions
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
        /// Gets child nodes that should be visible in the current view.
        /// </summary>
        public void RefreshCurrentView()
        {
            UpdateCurrentView();
            OnContextChanged?.Invoke();
        }

        /// <summary>
        /// Gets the bounds of all visible nodes.
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
