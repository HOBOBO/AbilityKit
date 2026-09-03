#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    internal enum PipelineDebuggerPhaseEdgeState
    {
        Normal,
        Active,
        Selected,
        Rejected
    }

    internal sealed class PipelineDebuggerPhaseGraphModel
    {
        internal const float NodeWidth = 176f;
        internal const float NodeHeight = 68f;
        internal const float NodeGap = 34f;
        internal const float LevelGap = 54f;
        internal const float MinZoom = 0.45f;
        internal const float MaxZoom = 1.6f;

        private readonly Dictionary<string, PipelinePhaseDebugState> _states =
            new Dictionary<string, PipelinePhaseDebugState>();
        private readonly Dictionary<string, PipelinePhaseDebugNode> _nodes =
            new Dictionary<string, PipelinePhaseDebugNode>();
        private readonly Dictionary<string, Rect> _nodeRects =
            new Dictionary<string, Rect>();
        private readonly List<PipelinePhaseDebugNode> _nodeOrder =
            new List<PipelinePhaseDebugNode>();

        public IReadOnlyDictionary<string, PipelinePhaseDebugState> States => _states;
        public IReadOnlyDictionary<string, PipelinePhaseDebugNode> Nodes => _nodes;
        public IReadOnlyDictionary<string, Rect> NodeRects => _nodeRects;
        public IReadOnlyList<PipelinePhaseDebugNode> NodeOrder => _nodeOrder;
        public float Zoom { get; private set; } = 1f;
        public Vector2 Pan { get; private set; } = new Vector2(24f, 24f);
        public string? SelectedNodeKey { get; private set; }

        public void Rebuild(
            PipelineDebugGraphSnapshot graph,
            PipelineDebugGraphLayout? layout,
            IReadOnlyList<PipelinePhaseDebugState> states)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (states == null) throw new ArgumentNullException(nameof(states));

            _states.Clear();
            for (int i = 0; i < states.Count; i++)
                _states[states[i].NodeKey] = states[i];

            _nodes.Clear();
            _nodeRects.Clear();
            _nodeOrder.Clear();

            float cursorX = 24f;
            for (int i = 0; i < graph.Roots.Count; i++)
            {
                PipelinePhaseDebugNode root = graph.Roots[i];
                Collect(root);
                float width = GetSubtreeWidth(root);
                LayoutSubtree(root, cursorX, 28f, width);
                cursorX += width + NodeGap * 1.5f;
            }

            if (layout != null)
            {
                for (int i = 0; i < layout.Nodes.Count; i++)
                {
                    PipelineDebugNodeLayout position = layout.Nodes[i];
                    if (_nodeRects.ContainsKey(position.NodeKey))
                    {
                        _nodeRects[position.NodeKey] =
                            new Rect(position.X, position.Y, NodeWidth, NodeHeight);
                    }
                }
            }

            if (SelectedNodeKey != null && !_nodes.ContainsKey(SelectedNodeKey))
                SelectedNodeKey = null;
        }

        public bool TrySelect(Vector2 canvasPoint, out PipelinePhaseDebugNode? node)
        {
            for (int i = _nodeOrder.Count - 1; i >= 0; i--)
            {
                PipelinePhaseDebugNode candidate = _nodeOrder[i];
                if (!Transform(_nodeRects[candidate.NodeKey]).Contains(canvasPoint))
                    continue;

                SelectedNodeKey = candidate.NodeKey;
                node = candidate;
                return true;
            }

            SelectedNodeKey = null;
            node = null;
            return false;
        }

        public bool TryGetNode(string nodeKey, out PipelinePhaseDebugNode? node)
        {
            if (_nodes.TryGetValue(nodeKey, out PipelinePhaseDebugNode value))
            {
                node = value;
                return true;
            }

            node = null;
            return false;
        }

        public void ClearSelection()
        {
            SelectedNodeKey = null;
        }

        public Rect Transform(Rect logical)
        {
            return new Rect(
                Pan.x + logical.x * Zoom,
                Pan.y + logical.y * Zoom,
                logical.width * Zoom,
                logical.height * Zoom);
        }

        public void ZoomAt(Vector2 canvasPoint, float factor)
        {
            float oldZoom = Zoom;
            float nextZoom = Mathf.Clamp(oldZoom * factor, MinZoom, MaxZoom);
            Vector2 logical = (canvasPoint - Pan) / oldZoom;
            Zoom = nextZoom;
            Pan = canvasPoint - logical * Zoom;
        }

        public void PanBy(Vector2 delta)
        {
            Pan += delta;
        }

        public bool Fit(Vector2 canvasSize)
        {
            if (!TryGetBounds(out Rect bounds)) return false;

            float widthZoom = Mathf.Max(0.01f, (canvasSize.x - 56f) / bounds.width);
            float heightZoom = Mathf.Max(0.01f, (canvasSize.y - 56f) / bounds.height);
            Zoom = Mathf.Clamp(Mathf.Min(widthZoom, heightZoom), MinZoom, 1.15f);
            Pan = canvasSize * 0.5f - bounds.center * Zoom;
            return true;
        }

        public bool Focus(string nodeKey, Vector2 canvasSize)
        {
            if (!_nodeRects.TryGetValue(nodeKey, out Rect rect)) return false;

            Zoom = Mathf.Clamp(Zoom, 0.75f, 1.2f);
            Pan = canvasSize * 0.5f - rect.center * Zoom;
            SelectedNodeKey = nodeKey;
            return true;
        }

        public bool TryFindFocusNode(out string? nodeKey)
        {
            for (int i = 0; i < _nodeOrder.Count; i++)
            {
                string key = _nodeOrder[i].NodeKey;
                if (_states.TryGetValue(key, out PipelinePhaseDebugState state)
                    && state.State == EPipelineDebugExecutionState.Failed)
                {
                    nodeKey = key;
                    return true;
                }
            }

            for (int i = 0; i < _nodeOrder.Count; i++)
            {
                string key = _nodeOrder[i].NodeKey;
                if (_states.TryGetValue(key, out PipelinePhaseDebugState state)
                    && state.State == EPipelineDebugExecutionState.Active)
                {
                    nodeKey = key;
                    return true;
                }
            }

            nodeKey = null;
            return false;
        }

        public EPipelineDebugExecutionState ResolveState(
            PipelinePhaseDebugNode node,
            IReadOnlyList<AbilityPipelinePhaseId> activePhases,
            IReadOnlyList<PipelineTraceEvent> trace)
        {
            if (_states.TryGetValue(node.NodeKey, out PipelinePhaseDebugState state))
                return state.State;

            for (int i = 0; i < activePhases.Count; i++)
            {
                if (activePhases[i] == node.PhaseId)
                    return EPipelineDebugExecutionState.Active;
            }

            EPipelineDebugExecutionState fallback = EPipelineDebugExecutionState.Pending;
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace[i].PhaseId != node.PhaseId) continue;
                if (trace[i].Type == EPipelineTraceEventType.PhaseError)
                    fallback = EPipelineDebugExecutionState.Failed;
                else if (trace[i].Type == EPipelineTraceEventType.PhaseComplete
                         && fallback != EPipelineDebugExecutionState.Failed)
                    fallback = EPipelineDebugExecutionState.Completed;
            }

            return fallback;
        }

        public PipelineDebuggerPhaseEdgeState ResolveEdgeState(PipelinePhaseDebugEdge edge)
        {
            if (_states.TryGetValue(edge.SourceNodeKey, out PipelinePhaseDebugState source)
                && edge.Kind == EPipelineDebugEdgeKind.Condition
                && edge.ChildIndex >= 0)
            {
                if (source.SelectedChildIndex == edge.ChildIndex)
                    return PipelineDebuggerPhaseEdgeState.Selected;
                if (edge.ChildIndex < source.ChildConditions.Count
                    && source.ChildConditions[edge.ChildIndex] == EPipelineDebugConditionResult.Rejected)
                    return PipelineDebuggerPhaseEdgeState.Rejected;
            }

            if (_states.TryGetValue(edge.TargetNodeKey, out PipelinePhaseDebugState target)
                && target.State == EPipelineDebugExecutionState.Active)
                return PipelineDebuggerPhaseEdgeState.Active;

            return PipelineDebuggerPhaseEdgeState.Normal;
        }

        public bool TryGetBounds(out Rect bounds)
        {
            bounds = default;
            bool found = false;
            foreach (KeyValuePair<string, Rect> pair in _nodeRects)
            {
                bounds = found ? Union(bounds, pair.Value) : pair.Value;
                found = true;
            }

            return found;
        }

        private void Collect(PipelinePhaseDebugNode node)
        {
            _nodes[node.NodeKey] = node;
            _nodeOrder.Add(node);
            for (int i = 0; i < node.Children.Count; i++) Collect(node.Children[i]);
        }

        private static float GetSubtreeWidth(PipelinePhaseDebugNode node)
        {
            if (node.Children.Count == 0) return NodeWidth;

            float width = 0f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0) width += NodeGap;
                width += GetSubtreeWidth(node.Children[i]);
            }

            return Mathf.Max(NodeWidth, width);
        }

        private void LayoutSubtree(PipelinePhaseDebugNode node, float left, float top, float width)
        {
            _nodeRects[node.NodeKey] = new Rect(
                left + (width - NodeWidth) * 0.5f,
                top,
                NodeWidth,
                NodeHeight);
            if (node.Children.Count == 0) return;

            float childLeft = left;
            float childTop = top + NodeHeight + LevelGap;
            for (int i = 0; i < node.Children.Count; i++)
            {
                float childWidth = GetSubtreeWidth(node.Children[i]);
                LayoutSubtree(node.Children[i], childLeft, childTop, childWidth);
                childLeft += childWidth + NodeGap;
            }
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }
    }
}

#endif
