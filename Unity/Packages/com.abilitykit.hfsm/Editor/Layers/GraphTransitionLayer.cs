using System;
using UnityEditor;
using UnityEngine;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// Layer that renders and handles transition edges between states.
    /// </summary>
    public class GraphTransitionLayer : GraphLayer
    {
        public static readonly Color DefaultColor = Color.white;
        public static readonly Color SelectedColor = new Color(0.4f, 0.8f, 1f);
        public static readonly Color PreviewColor = new Color(0.6f, 1f, 0.6f);
        public static readonly Color AnyStateColor = new Color(1f, 0.8f, 0.2f);

        private const float LineWidth = 3f;
        private const float ArrowSize = 12f;
        private const float HitTestWidth = 15f;
        private const float ConditionLabelOffset = 8f;
        private const float BidirectionalOffset = 15f;

        // 条件标签背景贴图：缓存复用，避免每帧 new Texture2D 造成泄漏。
        private static Texture2D s_LabelBackground;

        // Cache for edge grouping (source-target pairs)
        private System.Collections.Generic.Dictionary<string, int> _edgeIndexCache = new System.Collections.Generic.Dictionary<string, int>();

        public GraphTransitionLayer(EditorWindow editorWindow) : base(editorWindow)
        {
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            if (Context.GraphAsset == null)
                return;

            // Build edge index cache for bidirectional edge detection
            BuildEdgeIndexCache();

            // Draw all transitions
            foreach (var edge in Context.CurrentTransitions)
            {
                DrawTransition(edge);
            }

            // Draw preview transition
            if (Context.IsPreviewTransition && Context.TransitionSourceNode != null)
            {
                DrawPreviewTransition();
            }
        }

        private void BuildEdgeIndexCache()
        {
            _edgeIndexCache.Clear();
            foreach (var edge in Context.CurrentTransitions)
            {
                string key = GetEdgePairKey(edge.SourceNodeId, edge.TargetNodeId);
                if (!_edgeIndexCache.ContainsKey(key))
                {
                    _edgeIndexCache[key] = 0;
                }
                else
                {
                    _edgeIndexCache[key]++;
                }
            }
        }

        private string GetEdgePairKey(string sourceId, string targetId)
        {
            // Create a normalized key for bidirectional edges
            if (string.Compare(sourceId, targetId, StringComparison.Ordinal) < 0)
                return sourceId + "|" + targetId;
            else
                return targetId + "|" + sourceId;
        }

        private bool IsBidirectionalEdge(string sourceId, string targetId)
        {
            // Check if there's an edge going the opposite direction
            foreach (var edge in Context.CurrentTransitions)
            {
                if (edge.SourceNodeId == targetId && edge.TargetNodeId == sourceId)
                    return true;
            }
            return false;
        }

        private int GetEdgeIndexInPair(string sourceId, string targetId)
        {
            string key = GetEdgePairKey(sourceId, targetId);
            if (_edgeIndexCache.TryGetValue(key, out int index))
                return index;
            return 0;
        }

        public override void ProcessEvent()
        {
            HandleTransitionClick();
            HandleTransitionContextMenu();
        }

        private void HandleTransitionClick()
        {
            if (Event.current.type != EventType.MouseUp || Event.current.button != 0)
                return;

            Vector2 mousePos = Event.current.mousePosition;

            foreach (var edge in Context.CurrentTransitions)
            {
                if (IsMouseOverTransition(mousePos, edge))
                {
                    Context.SelectEdge(edge);
                    Event.current.Use();
                    return;
                }
            }

            // Clicked on empty space - clear selection
            if (Context.SelectedEdge != null)
            {
                Context.ClearSelection();
                Event.current.Use();
            }
        }

        private void HandleTransitionContextMenu()
        {
            if (Event.current.type != EventType.ContextClick)
                return;

            Vector2 mousePos = Event.current.mousePosition;

            foreach (var edge in Context.CurrentTransitions)
            {
                if (IsMouseOverTransition(mousePos, edge))
                {
                    ShowTransitionContextMenu(edge, mousePos);
                    Event.current.Use();
                    return;
                }
            }
        }

        private void ShowTransitionContextMenu(TransitionEdge edge, Vector2 position)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                Context.SelectEdge(edge);
                Context.DeleteSelectedEdge();
            });

            menu.ShowAsContext();
        }

        private bool IsMouseOverTransition(Vector2 mousePos, TransitionEdge edge)
        {
            var sourceNode = Context.GraphAsset.GetNodeById(edge.SourceNodeId);
            var targetNode = Context.GraphAsset.GetNodeById(edge.TargetNodeId);

            if (sourceNode == null || targetNode == null)
                return false;

            // Calculate edge positions
            Vector2 start, end;
            CalculateEdgePositions(sourceNode, targetNode, edge.SourceNodeId, edge.TargetNodeId, out start, out end);

            // Convert to screen coordinates
            start = ContentPosToScreen(start);
            end = ContentPosToScreen(end);

            // Check distance to line
            float distance = DistanceToLine(start, end, mousePos);
            return distance < HitTestWidth * Context.ZoomFactor;
        }

        private float DistanceToLine(Vector2 lineStart, Vector2 lineEnd, Vector2 point)
        {
            float dx = lineEnd.x - lineStart.x;
            float dy = lineEnd.y - lineStart.y;

            if (Mathf.Approximately(dx, 0) && Mathf.Approximately(dy, 0))
            {
                return Vector2.Distance(lineStart, point);
            }

            float t = Mathf.Clamp01(((point.x - lineStart.x) * dx + (point.y - lineStart.y) * dy) / (dx * dx + dy * dy));
            Vector2 projection = new Vector2(lineStart.x + t * dx, lineStart.y + t * dy);
            return Vector2.Distance(point, projection);
        }

        private void DrawTransition(TransitionEdge edge)
        {
            var sourceNode = Context.GraphAsset.GetNodeById(edge.SourceNodeId);
            var targetNode = Context.GraphAsset.GetNodeById(edge.TargetNodeId);

            if (sourceNode == null || targetNode == null)
                return;

            bool isSelected = Context.SelectedEdge == edge;
            Color color = isSelected ? SelectedColor : DefaultColor;

            Vector2 start, end;
            CalculateEdgePositions(sourceNode, targetNode, edge.SourceNodeId, edge.TargetNodeId, out start, out end);

            DrawArrowLine(start, end, color);

            // Draw condition label if there are conditions
            if (edge.HasConditions)
            {
                // Pass edge info to adjust label position for bidirectional edges
                bool isBidirectional = IsBidirectionalEdge(edge.SourceNodeId, edge.TargetNodeId);
                int edgeIndex = GetEdgeIndexInPair(edge.SourceNodeId, edge.TargetNodeId);
                DrawConditionLabel(start, end, edge, color, isBidirectional, edgeIndex);
            }
        }

        private void DrawPreviewTransition()
        {
            Vector2 start = GetNodeCenter(Context.TransitionSourceNode);
            Vector2 end;

            if (Context.TransitionTargetNode != null)
            {
                Vector2 targetCenter = GetNodeCenter(Context.TransitionTargetNode);
                end = GetNodeEdgePoint(Context.TransitionSourceNode, targetCenter);
            }
            else
            {
                end = ScreenPosToContent(Event.current.mousePosition);
            }

            DrawArrowLine(start, end, PreviewColor);
        }

        private void CalculateEdgePositions(NodeBase source, NodeBase target, string sourceId, string targetId, out Vector2 start, out Vector2 end)
        {
            Vector2 sourceCenter = GetNodeCenter(source);
            Vector2 targetCenter = GetNodeCenter(target);

            // Get base edge points on node boundaries
            start = GetNodeEdgePoint(source, targetCenter);
            end = GetNodeEdgePoint(target, sourceCenter);

            // Check if this is a bidirectional edge that needs offset
            if (IsBidirectionalEdge(sourceId, targetId))
            {
                int edgeIndex = GetEdgeIndexInPair(sourceId, targetId);
                Vector2 direction = (targetCenter - sourceCenter).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);

                // Alternate offset direction based on edge index
                float offsetMultiplier = (edgeIndex % 2 == 0) ? 1 : -1;
                Vector2 offset = perpendicular * BidirectionalOffset * offsetMultiplier;

                // Apply offset to both start and end points
                start += offset;
                end += offset;
            }
        }

        private Vector2 GetNodeCenter(NodeBase node)
        {
            return node.Position + node.Size * 0.5f;
        }

        private Vector2 GetNodeEdgePoint(NodeBase node, Vector2 targetPoint)
        {
            Vector2 center = GetNodeCenter(node);
            Vector2 direction = (targetPoint - center).normalized;

            // Calculate intersection with node rectangle
            Rect nodeRect = new Rect(node.Position, node.Size);

            // Check which edge the line intersects
            float scaleX = direction.x != 0 ? (direction.x > 0 ? (nodeRect.xMax - center.x) : (center.x - nodeRect.xMin)) / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = direction.y != 0 ? (direction.y > 0 ? (nodeRect.yMax - center.y) : (center.y - nodeRect.yMin)) / Mathf.Abs(direction.y) : float.MaxValue;

            float scale = Mathf.Min(scaleX, scaleY);
            return center + direction * scale;
        }

        private void DrawArrowLine(Vector2 from, Vector2 to, Color color)
        {
            from = ContentPosToScreen(from);
            to = ContentPosToScreen(to);

            // Draw shadow/glow for better visibility
            Color shadowColor = new Color(0, 0, 0, 0.3f);

            Handles.BeginGUI();

            // Draw shadow line
            Handles.color = shadowColor;
            Handles.DrawLine(from + new Vector2(1, 1), to + new Vector2(1, 1));

            // Draw main line with thicker width
            Handles.color = color;
            Handles.DrawLine(from, to);

            // Draw arrow head as filled triangle
            Vector2 direction = (to - from).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * 0.5f;
            float arrowSize = ArrowSize * Context.ZoomFactor;

            Vector2 arrowBase = to - direction * arrowSize;
            Vector2 arrowLeft = arrowBase + perpendicular * arrowSize;
            Vector2 arrowRight = arrowBase - perpendicular * arrowSize;

            // Draw arrow shape
            Handles.color = color;
            DrawTriangle(arrowLeft, to, arrowRight);

            Handles.EndGUI();
        }

        private void DrawTriangle(Vector2 p1, Vector2 p2, Vector3 p3)
        {
            // Use Handles.DrawLine to draw the three edges of the triangle
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, new Vector2(p3.x, p3.y));
            Handles.DrawLine(new Vector2(p3.x, p3.y), p1);
        }

        private void DrawConditionLabel(Vector2 from, Vector2 to, TransitionEdge edge, Color lineColor, bool isBidirectional, int edgeIndex)
        {
            // Calculate midpoint
            Vector2 midPoint = Vector2.Lerp(from, to, 0.5f);
            Vector2 screenMid = ContentPosToScreen(midPoint);

            // Calculate direction for text rotation
            Vector2 screenFrom = ContentPosToScreen(from);
            Vector2 screenTo = ContentPosToScreen(to);
            Vector2 direction = (screenTo - screenFrom).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // For bidirectional edges, offset label to avoid overlap
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float offsetMultiplier = isBidirectional ? (edgeIndex % 2 == 0 ? 1.5f : -1.5f) : 1f;
            Vector2 labelOffset = perpendicular * ConditionLabelOffset * offsetMultiplier * Context.ZoomFactor;

            // Draw background for label
            string labelText = GetConditionSummary(edge);
            GUIStyle labelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = LabelBackground() }
            };

            Vector2 labelSize = labelStyle.CalcSize(new GUIContent(labelText));
            Vector2 labelCenter = screenMid + labelOffset;

            Rect labelRect = new Rect(
                labelCenter.x - labelSize.x * 0.5f,
                labelCenter.y - labelSize.y * 0.5f,
                labelSize.x,
                labelSize.y
            );

            // Draw rotated label text using GUI.matrix
            DrawRotatedLabel(labelRect, angle, labelText, labelStyle);
        }

        private static string GetConditionSummary(TransitionEdge edge)
        {
            var conditions = edge.Conditions;
            if (conditions == null || conditions.Count == 0)
                return "始终";
            if (conditions.Count > 1)
                return $"{conditions.Count} 个条件（{(edge.UseAndLogic ? "AND" : "OR")}）";

            var condition = conditions[0];
            if (condition is ParameterCondition parameter)
            {
                if (parameter.ParameterType == ParameterValueType.Bool)
                    return $"{parameter.ParameterName} = {(parameter.BoolValue ? "真" : "假")}";
                if (parameter.ParameterType == ParameterValueType.Trigger)
                    return $"{parameter.ParameterName} 已触发";
                var value = parameter.ParameterType == ParameterValueType.Float
                    ? parameter.FloatValue.ToString()
                    : parameter.IntValue.ToString();
                return $"{parameter.ParameterName} {GetOperatorSymbol(parameter.Operator)} {value}";
            }
            if (condition is TimeElapsedCondition elapsed)
                return $"时间 {GetOperatorSymbol(elapsed.Operator)} {elapsed.Duration:F2} 秒";
            if (condition is BehaviorCompleteCondition)
                return "所有行为均已完成";
            return condition.GetDescription();
        }

        private static string GetOperatorSymbol(CompareOperator value)
        {
            switch (value)
            {
                case CompareOperator.Equal: return "==";
                case CompareOperator.NotEqual: return "!=";
                case CompareOperator.GreaterThan: return ">";
                case CompareOperator.LessThan: return "<";
                case CompareOperator.GreaterOrEqual: return ">=";
                case CompareOperator.LessOrEqual: return "<=";
                default: return "?";
            }
        }

        private static Texture2D LabelBackground()
        {
            if (s_LabelBackground == null)
            {
                s_LabelBackground = new Texture2D(1, 1);
                // Use a semi-transparent dark background for better readability with white text
                s_LabelBackground.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 0.9f));
                s_LabelBackground.Apply();
                s_LabelBackground.hideFlags = HideFlags.HideAndDontSave;
            }

            return s_LabelBackground;
        }

        private void DrawRotatedLabel(Rect rect, float angleDegrees, string text, GUIStyle style)
        {
            Vector2 pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);

            // Save current matrix
            Matrix4x4 oldMatrix = GUI.matrix;

            // Rotate around pivot
            GUIUtility.RotateAroundPivot(angleDegrees, pivot);

            // Draw the label at the rect position
            GUI.Label(rect, text, style);

            // Restore matrix
            GUI.matrix = oldMatrix;
        }

    }
}
