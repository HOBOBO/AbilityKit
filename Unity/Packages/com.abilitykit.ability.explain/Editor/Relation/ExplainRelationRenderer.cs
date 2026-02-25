using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainRelationRenderer
    {
        private readonly ScrollView _view;
        private readonly Action<string> _onNodeIdClicked;
        private readonly Action<PipelineItemKey> _onEntityInvoked;

        private readonly Dictionary<string, VisualElement> _nodeElementIndex = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> _entityElementIndex = new Dictionary<string, VisualElement>();

        private ExplainRelationGraph _graph;
        private VisualElement _overlay;

        private string _selectedNodeId;
        private string _selectedEntityKey;

        private Button _selectedNodeButton;
        private Label _selectedEntityLabel;

        public ExplainRelationRenderer(ScrollView view, Action<string> onNodeIdClicked, Action<PipelineItemKey> onEntityInvoked)
        {
            _view = view;
            _onNodeIdClicked = onNodeIdClicked;
            _onEntityInvoked = onEntityInvoked;
        }

        public void Clear()
        {
            _graph = null;
            _nodeElementIndex.Clear();
            _entityElementIndex.Clear();
            _selectedNodeId = null;
            _selectedEntityKey = null;
            _selectedNodeButton = null;
            _selectedEntityLabel = null;
            _view?.Clear();
        }

        public void Render(ExplainForest forest, Dictionary<string, ExplainTreeRoot> expandedRoots = null)
        {
            if (_view == null) return;

            _view.Clear();
            if (forest == null) return;

            _selectedNodeId = null;
            _selectedEntityKey = null;
            _selectedNodeButton = null;
            _selectedEntityLabel = null;

            _graph = ExplainRelationGraphBuilder.Build(forest, expandedRoots);
            if (_graph == null) return;

            _nodeElementIndex.Clear();
            _entityElementIndex.Clear();

            var header = new Label("关系 (连线)");
            AbilityExplainStyles.ApplyTreeHeader(header);
            _view.Add(header);

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.position = Position.Relative;
            root.style.width = Length.Percent(100);
            _view.Add(root);

            _overlay = new VisualElement();
            _overlay.pickingMode = PickingMode.Ignore;
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.generateVisualContent += OnGenerateVisualContent;
            root.Add(_overlay);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            columns.style.width = Length.Percent(100);
            root.Add(columns);

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexBasis = 0;
            left.style.minWidth = 260;
            left.style.paddingLeft = AbilityExplainStyles.Padding;
            left.style.paddingRight = AbilityExplainStyles.Padding;
            left.style.backgroundColor = new Color(0f, 0f, 0f, 0.04f);
            columns.Add(left);

            var right = new VisualElement();
            right.style.flexGrow = 0;
            right.style.width = 420;
            right.style.flexShrink = 0;
            right.style.paddingLeft = AbilityExplainStyles.Padding + 22;
            right.style.paddingRight = AbilityExplainStyles.Padding;
            right.style.backgroundColor = new Color(0f, 0f, 0f, 0.04f);
            columns.Add(right);

            RenderNodes(left, _graph);
            RenderEntities(right, _graph);

            root.RegisterCallback<GeometryChangedEvent>(_ => _overlay?.MarkDirtyRepaint());
            _view.RegisterCallback<GeometryChangedEvent>(_ => _overlay?.MarkDirtyRepaint());
            _view.verticalScroller.valueChanged += _ => _overlay?.MarkDirtyRepaint();
        }

        private void RenderNodes(VisualElement parent, ExplainRelationGraph graph)
        {
            if (graph?.Nodes == null) return;

            var title = new Label("节点");
            AbilityExplainStyles.ApplyTreeHeader(title);
            parent.Add(title);

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.DiscoveredStartIndex >= 0 && i == graph.DiscoveredStartIndex)
                {
                    var discoveredHeader = new Label("自发现");
                    AbilityExplainStyles.ApplyTreeHeader(discoveredHeader);
                    discoveredHeader.style.marginTop = 12;
                    parent.Add(discoveredHeader);
                }

                var n = graph.Nodes[i];
                if (n == null) continue;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Column;
                row.style.marginBottom = 8;
                row.style.paddingLeft = AbilityExplainStyles.Padding + n.Depth * 12;
                row.style.width = Length.Percent(100);

                var titleRow = new VisualElement();
                titleRow.style.flexDirection = FlexDirection.Row;
                titleRow.style.alignItems = Align.Center;
                titleRow.style.flexGrow = 1;
                titleRow.style.width = Length.Percent(100);

                Button btn = null;
                btn = new Button(() =>
                {
                    if (_selectedNodeId == n.NodeId) _selectedNodeId = null;
                    else
                    {
                        _selectedNodeId = n.NodeId;
                        _selectedEntityKey = null;
                    }

                    SetSelectedNodeButton(btn);
                    SetSelectedEntityLabel(null);
                    TryScrollToFirstEntityOfNode(_selectedNodeId);
                    _overlay?.MarkDirtyRepaint();
                    _onNodeIdClicked?.Invoke(n.NodeId);

                    if (n.Kind == "discovered_entry" && n.ReferencedEntities.Count > 0)
                    {
                        _onEntityInvoked?.Invoke(n.ReferencedEntities[0]);
                    }
                })
                {
                    text = n.Title
                };
                btn.style.flexGrow = 1;
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                titleRow.Add(btn);

                if (!string.IsNullOrEmpty(n.Kind))
                {
                    var kind = new Label(n.Kind);
                    kind.style.unityFontStyleAndWeight = FontStyle.Italic;
                    kind.style.marginLeft = 6;
                    titleRow.Add(kind);
                }

                row.Add(titleRow);

                if (n.Kind != "discovered_entry" && n.ReferencedEntities.Count > 0)
                {
                    var refsFoldout = new Foldout { text = $"引用 ({n.ReferencedEntities.Count})", value = false };
                    refsFoldout.style.marginLeft = 14;

                    var refs = new Label(FormatRefs(n.ReferencedEntities));
                    refs.style.opacity = 0.8f;
                    refs.style.whiteSpace = WhiteSpace.Normal;
                    refsFoldout.Add(refs);

                    row.Add(refsFoldout);
                }

                parent.Add(row);

                if (!string.IsNullOrEmpty(n.NodeId) && !_nodeElementIndex.ContainsKey(n.NodeId))
                {
                    _nodeElementIndex[n.NodeId] = titleRow;
                }
            }
        }

        private void RenderEntities(VisualElement parent, ExplainRelationGraph graph)
        {
            if (graph?.EntityIndex == null || graph.EntityIndex.Count <= 0) return;

            var title = new Label("实体");
            AbilityExplainStyles.ApplyTreeHeader(title);
            parent.Add(title);

            foreach (var kv in graph.EntityIndex)
            {
                var e = kv.Value;
                if (e == null) continue;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Column;
                row.style.marginBottom = 12;
                row.style.paddingLeft = AbilityExplainStyles.Padding + 6;
                row.style.minHeight = 32;
                row.style.justifyContent = Justify.Center;

                var itemTitle = new Label($"{e.Key.Type}#{e.Key.Id}");
                itemTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemTitle.style.paddingLeft = 2;
                itemTitle.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;

                    if (_selectedEntityKey == kv.Key) _selectedEntityKey = null;
                    else
                    {
                        _selectedEntityKey = kv.Key;
                        _selectedNodeId = null;
                    }

                    SetSelectedEntityLabel(_selectedEntityKey == kv.Key ? itemTitle : null);
                    SetSelectedNodeButton(null);
                    TryScrollToFirstNodeOfEntity(_selectedEntityKey);
                    _overlay?.MarkDirtyRepaint();

                    if (evt.clickCount >= 2)
                    {
                        _onEntityInvoked?.Invoke(e.Key);
                    }
                    evt.StopPropagation();
                });
                row.Add(itemTitle);

                if (e.ReferencedByNodeIds.Count > 0)
                {
                    var total = e.ReferencedByNodeIds.Count;
                    var mainCount = 0;
                    var discoveredCount = 0;
                    var kindCount = new Dictionary<string, int>();

                    for (var i = 0; i < e.ReferencedByNodeIds.Count; i++)
                    {
                        var nodeId = e.ReferencedByNodeIds[i];
                        if (string.IsNullOrEmpty(nodeId)) continue;

                        var isDiscovered = nodeId.StartsWith("discovered:", StringComparison.Ordinal);
                        if (isDiscovered) discoveredCount++;
                        else mainCount++;

                        if (graph.NodeIndex != null && graph.NodeIndex.TryGetValue(nodeId, out var n) && n != null)
                        {
                            var kind = string.IsNullOrEmpty(n.Kind) ? "(无)" : n.Kind;
                            if (!kindCount.TryGetValue(kind, out var c)) c = 0;
                            kindCount[kind] = c + 1;
                        }
                    }

                    var kinds = string.Empty;
                    foreach (var kk in kindCount)
                    {
                        if (!string.IsNullOrEmpty(kinds)) kinds += "，";
                        kinds += $"{kk.Key}×{kk.Value}";
                    }

                    var by = new Label($"被引用：{total}（主流程 {mainCount}，自发现 {discoveredCount}）" + (string.IsNullOrEmpty(kinds) ? string.Empty : $"；节点类型：{kinds}"));
                    by.style.opacity = 0.8f;
                    by.style.whiteSpace = WhiteSpace.Normal;
                    row.Add(by);
                }

                parent.Add(row);

                if (!_entityElementIndex.ContainsKey(kv.Key))
                {
                    _entityElementIndex[kv.Key] = itemTitle;
                }
            }
        }

        private void TryScrollToFirstEntityOfNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (_graph == null) return;

            if (!_graph.NodeIndex.TryGetValue(nodeId, out var n) || n == null) return;
            if (n.ReferencedEntities == null || n.ReferencedEntities.Count <= 0) return;

            var k = n.ReferencedEntities[0].ToString();
            if (_entityElementIndex.TryGetValue(k, out var el) && el != null)
            {
                _view.ScrollTo(el);
            }
        }

        private void TryScrollToFirstNodeOfEntity(string entityKey)
        {
            if (string.IsNullOrEmpty(entityKey)) return;
            if (_graph == null) return;

            if (!_graph.EntityIndex.TryGetValue(entityKey, out var e) || e == null) return;
            if (e.ReferencedByNodeIds == null || e.ReferencedByNodeIds.Count <= 0) return;

            var nodeId = e.ReferencedByNodeIds[0];
            if (_nodeElementIndex.TryGetValue(nodeId, out var el) && el != null)
            {
                _view.ScrollTo(el);
            }
        }

        private void SetSelectedNodeButton(Button btn)
        {
            if (_selectedNodeButton != null)
            {
                _selectedNodeButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            _selectedNodeButton = btn;
            if (_selectedNodeButton != null)
            {
                _selectedNodeButton.style.backgroundColor = new Color(0.2f, 0.55f, 0.95f, 0.22f);
            }
        }

        private void SetSelectedEntityLabel(Label label)
        {
            if (_selectedEntityLabel != null)
            {
                _selectedEntityLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            _selectedEntityLabel = label;
            if (_selectedEntityLabel != null)
            {
                _selectedEntityLabel.style.backgroundColor = new Color(0.2f, 0.55f, 0.95f, 0.22f);
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_graph == null) return;

            var painter = mgc.painter2D;
            if (painter == null) return;

            painter.lineCap = LineCap.Round;

            var hasSelection = !string.IsNullOrEmpty(_selectedNodeId) || !string.IsNullOrEmpty(_selectedEntityKey);
            var weakColor = new Color(0.2f, 0.55f, 0.95f, 0.12f);
            var strongColor = new Color(0.2f, 0.55f, 0.95f, 0.7f);

            DrawTreeEdges(painter, hasSelection);

            if (hasSelection)
            {
                for (var i = 0; i < _graph.Nodes.Count; i++)
                {
                    var n = _graph.Nodes[i];
                    if (n == null) continue;
                    if (string.IsNullOrEmpty(n.NodeId)) continue;
                    if (!_nodeElementIndex.TryGetValue(n.NodeId, out var fromEl) || fromEl == null) continue;

                    var from = WorldAnchorToOverlay(fromEl, 1f);
                    if (from == null) continue;

                    for (var r = 0; r < n.ReferencedEntities.Count; r++)
                    {
                        var k = n.ReferencedEntities[r];
                        var keyStr = k.ToString();
                        if (!_entityElementIndex.TryGetValue(keyStr, out var toEl) || toEl == null) continue;

                        var to = WorldAnchorToOverlay(toEl, 0f);
                        if (to == null) continue;

                        var start = from.Value;
                        var end = to.Value;
                        var dx = Mathf.Max(40f, (end.x - start.x) * 0.5f);
                        var c1 = new Vector2(start.x + dx, start.y);
                        var c2 = new Vector2(end.x - dx, end.y);

                        var isHit = (!string.IsNullOrEmpty(_selectedNodeId) && _selectedNodeId == n.NodeId)
                            || (!string.IsNullOrEmpty(_selectedEntityKey) && _selectedEntityKey == keyStr);

                        painter.lineWidth = isHit ? 2.5f : 1.2f;
                        painter.strokeColor = isHit ? strongColor : weakColor;

                        painter.BeginPath();
                        painter.MoveTo(start);
                        DrawCubicBezierApprox(painter, start, c1, c2, end);
                        painter.Stroke();
                    }
                }
            }
        }

        private void DrawTreeEdges(Painter2D painter, bool hasSelection)
        {
            if (_graph?.Nodes == null || _graph.Nodes.Count <= 0) return;

            var weak = new Color(0f, 0f, 0f, 0.10f);
            var strong = new Color(0.2f, 0.55f, 0.95f, 0.40f);

            for (var i = 0; i < _graph.Nodes.Count; i++)
            {
                var n = _graph.Nodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.NodeId) || string.IsNullOrEmpty(n.ParentNodeId)) continue;

                if (!_nodeElementIndex.TryGetValue(n.NodeId, out var childEl) || childEl == null) continue;
                if (!_nodeElementIndex.TryGetValue(n.ParentNodeId, out var parentEl) || parentEl == null) continue;

                var from = WorldAnchorToOverlay(parentEl, 0f);
                var to = WorldAnchorToOverlay(childEl, 0f);
                if (from == null || to == null) continue;

                var isHit = !hasSelection
                    || (!string.IsNullOrEmpty(_selectedNodeId) && (_selectedNodeId == n.NodeId || _selectedNodeId == n.ParentNodeId));

                painter.lineWidth = isHit ? 1.6f : 1.0f;
                painter.strokeColor = isHit ? strong : weak;

                painter.BeginPath();
                painter.MoveTo(from.Value);

                var start = from.Value;
                var end = to.Value;
                var dx = Mathf.Max(25f, (end.x - start.x) * 0.35f);
                var c1 = new Vector2(start.x + dx, start.y);
                var c2 = new Vector2(end.x - dx, end.y);
                DrawCubicBezierApprox(painter, start, c1, c2, end);

                painter.Stroke();
            }
        }

        private static void DrawCubicBezierApprox(Painter2D painter, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            const int segments = 18;
            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var pt = CubicBezier(p0, p1, p2, p3, t);
                painter.LineTo(pt);
            }
        }

        private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var u = 1f - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;

            var p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        private Vector2? WorldAnchorToOverlay(VisualElement el, float xNormalized)
        {
            if (_overlay == null || el == null) return null;

            var world = el.worldBound;
            var x = Mathf.Lerp(world.xMin, world.xMax, Mathf.Clamp01(xNormalized));
            var y = world.yMin + world.height * 0.5f;
            return _overlay.WorldToLocal(new Vector2(x, y));
        }

        private static string FormatRefs(List<PipelineItemKey> refs)
        {
            if (refs == null || refs.Count <= 0) return string.Empty;

            var s = "引用: ";
            for (var i = 0; i < refs.Count; i++)
            {
                if (i > 0) s += ", ";
                s += $"{refs[i].Type}#{refs[i].Id}";
            }
            return s;
        }
    }
}
