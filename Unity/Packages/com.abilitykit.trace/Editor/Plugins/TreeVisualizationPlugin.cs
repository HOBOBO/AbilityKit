#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AbilityKit.Editor.Framework;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 树可视化插件 - 在详情区域绘制树形结构
    /// </summary>
    public class TreeVisualizationPlugin : BaseWindowPlugin<TraceRootViewData>
    {
        private TraceTreeViewModel _viewModel;
        private Vector2 _scrollPosition;
        private Vector2 _panOffset;
        private float _zoom = 1.0f;
        private bool _isPanning;
        private Vector2 _lastMousePosition;
        private long _selectedNodeId;

        public TreeVisualizationPlugin(TraceTreeViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override int Priority => 10;

        public override void OnDetailGUI(TraceRootViewData item)
        {
            if (item == null) return;

            DrawTreeVisualization(item);
        }

        public override void OnSelectionChanged(TraceRootViewData previous, TraceRootViewData current)
        {
            _selectedNodeId = 0;
            _scrollPosition = Vector2.zero;
            _panOffset = Vector2.zero;
        }

        public override void OnDestroy()
        {
            // 清理资源
        }

        private void DrawTreeVisualization(TraceRootViewData rootData)
        {
            // 工具栏
            DrawTreeToolbar();

            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

            var canvasRect = GUILayoutUtility.GetRect(
                1200 * _zoom,
                800 * _zoom,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            HandleCanvasInput(canvasRect);
            DrawTree(canvasRect, rootData);

            EditorGUILayout.EndVertical();

            // 状态提示
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Zoom: {_zoom:F1}x | Pan: Right-drag | Nodes: {_viewModel.CurrentNodes.Count}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Select: Left-click", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTreeToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Tree View", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _zoom = 1.0f;
                _panOffset = Vector2.zero;
            }

            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _zoom = Mathf.Clamp(_zoom - 0.1f, 0.3f, 3.0f);
            }
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _zoom = Mathf.Clamp(_zoom + 0.1f, 0.3f, 3.0f);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            var currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && canvasRect.Contains(currentEvent.mousePosition))
            {
                _lastMousePosition = currentEvent.mousePosition;

                if (currentEvent.button == 0) // Left click - select node
                {
                    var clickedNode = GetNodeAtPosition(currentEvent.mousePosition, canvasRect);
                    if (clickedNode.HasValue)
                    {
                        _selectedNodeId = clickedNode.Value;
                        _viewModel.SelectNode(clickedNode.Value);
                    }
                    currentEvent.Use();
                }
                else if (currentEvent.button == 2) // Right click - pan
                {
                    _isPanning = true;
                    currentEvent.Use();
                }
            }
            else if (currentEvent.type == EventType.MouseDrag && _isPanning)
            {
                var delta = currentEvent.mousePosition - _lastMousePosition;
                _panOffset += delta;
                _lastMousePosition = currentEvent.mousePosition;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                _isPanning = false;
            }
            else if (currentEvent.type == EventType.ScrollWheel && canvasRect.Contains(currentEvent.mousePosition))
            {
                float zoomDelta = currentEvent.delta.y > 0 ? -0.1f : 0.1f;
                _zoom = Mathf.Clamp(_zoom + zoomDelta, 0.3f, 3.0f);
                currentEvent.Use();
            }
        }

        private long? GetNodeAtPosition(Vector2 mousePos, Rect canvasRect)
        {
            foreach (var node in _viewModel.CurrentNodes)
            {
                var nodePos = CalculateNodePosition(node, canvasRect);
                var nodeRect = new Rect(nodePos.x, nodePos.y, 150 * _zoom, 50 * _zoom);

                if (nodeRect.Contains(mousePos))
                {
                    return node.ContextId;
                }
            }
            return null;
        }

        private Vector2 CalculateNodePosition(TraceNodeViewData node, Rect canvasRect)
        {
            float levelWidth = 180 * _zoom;
            float levelHeight = 70 * _zoom;

            float x = node.Level * levelWidth + 20 * _zoom + _panOffset.x + canvasRect.x;
            float y = node.OrderInLevel * levelHeight + 20 * _zoom + _panOffset.y + canvasRect.y;

            return new Vector2(x, y);
        }

        private void DrawTree(Rect canvasRect, TraceRootViewData rootData)
        {
            if (_viewModel.CurrentNodes.Count == 0) return;

            DrawConnections(canvasRect);
            DrawNodes(canvasRect);
        }

        private void DrawConnections(Rect canvasRect)
        {
            var handlesColor = Handles.color;
            Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);

            foreach (var node in _viewModel.CurrentNodes)
            {
                if (node.ParentId != 0)
                {
                    var parentNode = _viewModel.GetNodeById(node.ParentId);
                    if (parentNode != null)
                    {
                        var parentPos = CalculateNodePosition(parentNode, canvasRect);
                        var childPos = CalculateNodePosition(node, canvasRect);

                        var startX = parentPos.x + 75 * _zoom;
                        var startY = parentPos.y + 50 * _zoom;
                        var endX = childPos.x + 75 * _zoom;
                        var endY = childPos.y;

                        Handles.DrawBezier(
                            new Vector3(startX, startY, 0),
                            new Vector3(endX, endY, 0),
                            new Vector3(startX, startY + 20 * _zoom, 0),
                            new Vector3(endX, endY - 20 * _zoom, 0),
                            handlesColor,
                            null,
                            2f
                        );
                    }
                }
            }

            Handles.color = handlesColor;
        }

        private void DrawNodes(Rect canvasRect)
        {
            foreach (var node in _viewModel.CurrentNodes)
            {
                DrawNode(node, canvasRect);
            }
        }

        private void DrawNode(TraceNodeViewData node, Rect canvasRect)
        {
            var pos = CalculateNodePosition(node, canvasRect);
            var nodeWidth = 150 * _zoom;
            var nodeHeight = 50 * _zoom;

            var nodeRect = new Rect(pos.x, pos.y, nodeWidth, nodeHeight);

            // 节点背景颜色
            Color bgColor;
            if (node.IsRoot)
                bgColor = new Color(0.2f, 0.6f, 0.9f, 0.9f);
            else if (node.IsEnded)
                bgColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
            else
                bgColor = new Color(0.2f, 0.8f, 0.4f, 0.9f);

            if (_selectedNodeId == node.ContextId)
                bgColor = new Color(1f, 0.6f, 0.2f, 1f);

            // 绘制背景
            EditorGUI.DrawRect(nodeRect, bgColor);

            // 绘制内容
            GUI.BeginGroup(nodeRect);

            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(10 * _zoom),
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            var kindLabel = string.IsNullOrEmpty(node.KindName) ? $"Kind:{node.Kind}" : node.KindName;
            if (kindLabel.Length > 15)
                kindLabel = kindLabel.Substring(0, 12) + "...";

            GUI.Label(new Rect(5, 8 * _zoom, nodeWidth - 10, 14 * _zoom), kindLabel, labelStyle);

            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Mathf.RoundToInt(8 * _zoom),
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(5, 26 * _zoom, nodeWidth - 10, 12 * _zoom), $"#{node.ContextId}", subLabelStyle);

            if (node.IsEnded)
            {
                GUI.Label(new Rect(nodeWidth - 18, 5, 12, 12), "●",
                    new GUIStyle(EditorStyles.label) { fontSize = 8, normal = { textColor = Color.red } });
            }

            GUI.EndGroup();

            // 绘制边框
            var borderColor = _selectedNodeId == node.ContextId ?
                new Color(1f, 0.8f, 0.2f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.5f);

            DrawRectBorder(nodeRect, borderColor);
        }

        private void DrawRectBorder(Rect rect, Color color)
        {
            var texture = Texture2D.whiteTexture;
            var guiColor = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), texture);
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), texture);

            GUI.color = guiColor;
        }
    }
}
#endif
