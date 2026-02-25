using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class AbilityExplainWindowView
    {
        private readonly VisualElement _root;

        private TextField _searchField;
        private Toggle _includeDiscoveredToggle;
        private IntegerField _maxDepthField;
        private Toggle _relationToggle;
        private VisualElement _entityListRoot;
        private VisualElement _entityFiltersContainer;
        private VisualElement _entityHeaderContainer;
        private Button _contextEditorButton;
        private ScrollView _entityGroupsContainer;
        private ScrollView _forestView;
        private ScrollView _relationView;
        private ScrollView _detailsView;
        private ScrollView _issuesView;
        private ScrollView _debugView;

        private ExplainForestRenderer _forestRenderer;
        private ExplainRelationRenderer _relationRenderer;
        private ExplainDetailsRenderer _detailsRenderer;
        private ExplainIssuesRenderer _issuesRenderer;
        private ExplainDebugRenderer _debugRenderer;

        private ExplainDetailsContext _detailsContext;

        public event Action<string> SearchChanged;
        public event Action RefreshClicked;
        public event Action OptionsChanged;
        public event Action<bool> RelationModeChanged;
        public event Action<PipelineItemKey> RelationEntityInvoked;
        public event Action<PipelineItemKey> EntitySelected;
        public event Action ContextEditorClicked;
        public event Action<ExplainNode, bool> NodeInvoked;
        public event Action<ExplainNode, DropdownMenu> NodeContextMenuPopulateRequested;
        public event Action<ExplainTreeDiscovery, bool> DiscoveryToggleRequested;
        public event Action<ExplainAction> ActionInvoked;
        public event Action<ExplainIssue> IssueInvoked;

        public string SearchText => _searchField?.value;

        public bool IncludeDiscovered => _includeDiscoveredToggle == null || _includeDiscoveredToggle.value;
        public int MaxDepth => _maxDepthField != null ? _maxDepthField.value : 0;

        public void SetSearchTextWithoutNotify(string value)
        {
            if (_searchField == null) return;
            _searchField.SetValueWithoutNotify(value ?? string.Empty);
        }

        public void SetIncludeDiscoveredWithoutNotify(bool value)
        {
            if (_includeDiscoveredToggle == null) return;
            _includeDiscoveredToggle.SetValueWithoutNotify(value);
        }

        public void SetMaxDepthWithoutNotify(int value)
        {
            if (_maxDepthField == null) return;
            _maxDepthField.SetValueWithoutNotify(value);
        }

        public AbilityExplainWindowView(VisualElement root)
        {
            _root = root;
            ConstructUI();
        }

        public void SetEntityListFilters(VisualElement filters)
        {
            if (_entityFiltersContainer == null) return;

            _entityFiltersContainer.Clear();
            if (filters != null)
            {
                _entityFiltersContainer.Add(filters);
            }
        }

        public void RenderEntityGroups(List<ExplainEntityListGroup> groups, PipelineItemKey? selected)
        {
            if (_entityGroupsContainer == null) return;

            _entityGroupsContainer.Clear();

            if (groups == null || groups.Count <= 0)
            {
                return;
            }

            for (var gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                if (g == null) continue;

                VisualElement parent = _entityGroupsContainer;
                if (!string.IsNullOrEmpty(g.Title))
                {
                    var foldout = new Foldout { text = g.Title, value = true };
                    foldout.style.marginTop = 6;
                    foldout.style.marginBottom = 2;
                    foldout.style.paddingLeft = 6;
                    _entityGroupsContainer.Add(foldout);
                    parent = foldout;
                }

                if (g.Items == null || g.Items.Count <= 0) continue;

                for (var i = 0; i < g.Items.Count; i++)
                {
                    var key = g.Items[i];
                    if (string.IsNullOrEmpty(key.Type) && string.IsNullOrEmpty(key.Id)) continue;

                    var provider = AbilityExplainRegistry.GetEntityProvider();
                    var text = provider != null ? provider.GetDisplayName(in key) : key.ToString();

                    var row = new Button(() => EntitySelected?.Invoke(key))
                    {
                        text = text
                    };

                    row.style.unityTextAlign = TextAnchor.MiddleLeft;
                    row.style.paddingLeft = 6;
                    row.style.paddingRight = 6;
                    row.style.height = 20;
                    row.style.marginLeft = 2;
                    row.style.marginRight = 2;
                    row.style.marginBottom = 2;

                    if (selected.HasValue && selected.Value.Type == key.Type && selected.Value.Id == key.Id)
                    {
                        row.style.backgroundColor = new Color(0.2f, 0.55f, 0.95f, 0.22f);
                    }

                    parent.Add(row);
                }
            }
        }

        public void ClearForest() => _forestView?.Clear();
        public void ClearDetails() => _detailsView?.Clear();
        public void ClearIssues() => _issuesRenderer?.Clear();
        public void ClearDebug() => _debugRenderer?.Clear();

        public void RenderMissingSetupHint()
        {
            if (_forestView == null) return;

            var hint = new Label("未发现项目端实现：请注册 IEntityProvider/IExplainResolver/INavigator。\n\n预览界面：在 Package Manager 中选择 com.abilitykit.ability.explain -> Samples -> Import ‘Mock Integration’。");
            AbilityExplainStyles.ApplyMissingHint(hint);

            _forestView.Add(hint);
        }

        public void RenderForest(ExplainForest forest)
        {
            _forestRenderer?.Render(forest);
        }

        public void ClearRelation() => _relationRenderer?.Clear();

        public void RenderRelation(ExplainForest forest)
        {
            RenderRelation(forest, null);
        }

        public void RenderRelation(ExplainForest forest, Dictionary<string, ExplainTreeRoot> expandedRoots)
        {
            _relationRenderer?.Render(forest, expandedRoots);
        }

        public void SetForestDiffMap(Dictionary<string, ExplainDiffKind> diffMap)
        {
            _forestRenderer?.SetDiffMap(diffMap);
        }

        public void SetSelectedNodeId(string nodeId)
        {
            _forestRenderer?.SetSelectedNodeId(nodeId);
        }

        public void AppendExpandedRoot(ExplainTreeRoot root)
        {
            _forestRenderer?.AppendRoot(root);
        }

        public void SetDiscoveryExpanded(ExplainTreeDiscovery discovery, ExplainTreeRoot root)
        {
            _forestRenderer?.SetDiscoveryExpanded(discovery, root);
        }

        public void SetDiscoveryCollapsed(ExplainTreeDiscovery discovery)
        {
            _forestRenderer?.SetDiscoveryCollapsed(discovery);
        }

        public void AppendOrUpdateDiscoveries(List<ExplainTreeDiscovery> discoveries)
        {
            _forestRenderer?.AppendOrUpdateDiscoveries(discoveries);
        }

        public bool TryFocusNode(string nodeId, out ExplainNode node)
        {
            node = null;
            return _forestRenderer != null && _forestRenderer.TryFocusNode(nodeId, out node);
        }

        public bool TryFocusDiscovery(in PipelineItemKey key)
        {
            return _forestRenderer != null && _forestRenderer.TryFocusDiscovery(in key);
        }

        public void RenderNodeDetails(ExplainNode node)
        {
            _detailsRenderer?.Render(node, _detailsContext);
        }

        public void SetContextEditorButton(string text, bool visible)
        {
            if (_contextEditorButton == null) return;
            _contextEditorButton.text = string.IsNullOrEmpty(text) ? "强化/构筑" : text;
            _contextEditorButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetDetailsContext(ExplainDetailsContext context)
        {
            _detailsContext = context;
        }

        public void RenderIssues(List<ExplainIssue> issues)
        {
            _issuesRenderer?.Render(issues);
        }

        public void RenderDebug(ExplainResolveResult result)
        {
            _debugRenderer?.Render(result);
        }

        private void ConstructUI()
        {
            _root.Clear();

            var toolbar = new VisualElement();
            AbilityExplainStyles.ApplyToolbar(toolbar);

            _searchField = new TextField { value = string.Empty };
            _searchField.style.flexGrow = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.minWidth = 140;
            _searchField.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));
            toolbar.Add(_searchField);

            // _includeDiscoveredToggle = new Toggle("自发现") { value = true };
            // _includeDiscoveredToggle.style.marginLeft = 8;
            // _includeDiscoveredToggle.RegisterValueChangedCallback(_ => OptionsChanged?.Invoke());
            // toolbar.Add(_includeDiscoveredToggle);

            // _maxDepthField = new IntegerField("最大深度") { value = 0 };
            // _maxDepthField.style.marginLeft = 8;
            // _maxDepthField.style.width = 140;
            // _maxDepthField.RegisterValueChangedCallback(_ => OptionsChanged?.Invoke());
            // toolbar.Add(_maxDepthField);

            _relationToggle = new Toggle("关系图") { value = false };
            _relationToggle.style.marginLeft = 8;
            _relationToggle.RegisterValueChangedCallback(evt => RelationModeChanged?.Invoke(evt.newValue));
            toolbar.Add(_relationToggle);

            // var refreshBtn = new Button(() => RefreshClicked?.Invoke()) { text = "刷新" };
            // refreshBtn.style.marginLeft = 8;
            // toolbar.Add(refreshBtn);

            _root.Add(toolbar);

            var main = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

            const float minLeftWidth = 180f;
            const float minCenterWidth = 240f;
            const float minRightWidth = 220f;
            const float splitterWidth = 4f;

            var left = new VisualElement { style = { width = 260, flexShrink = 0, borderRightWidth = 1, minWidth = minLeftWidth } };
            var center = new VisualElement { style = { flexGrow = 1, borderRightWidth = 1, minWidth = minCenterWidth } };
            var right = new VisualElement { style = { width = 360, flexShrink = 0, minWidth = minRightWidth } };

            VisualElement CreateSplitter()
            {
                var s = new VisualElement();
                s.style.width = splitterWidth;
                s.style.flexShrink = 0;
                s.style.backgroundColor = new Color(0f, 0f, 0f, 0.06f);
                return s;
            }

            void AttachLeftSplitterDrag(VisualElement splitter)
            {
                var dragging = false;
                var startX = 0f;
                var startLeftWidth = 0f;

                splitter.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startLeftWidth = left.resolvedStyle.width;
                    splitter.CaptureMouse();
                    evt.StopPropagation();
                });

                splitter.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    if (!dragging) return;

                    var mainWidth = main.resolvedStyle.width;
                    var rightWidth = right.resolvedStyle.width;
                    var dx = evt.mousePosition.x - startX;

                    var maxLeft = mainWidth - rightWidth - minCenterWidth - splitterWidth - splitterWidth;
                    var nextLeft = Mathf.Clamp(startLeftWidth + dx, minLeftWidth, maxLeft);
                    left.style.width = nextLeft;
                    evt.StopPropagation();
                });

                splitter.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    if (!dragging) return;
                    dragging = false;
                    splitter.ReleaseMouse();
                    evt.StopPropagation();
                });
            }

            void AttachRightSplitterDrag(VisualElement splitter)
            {
                var dragging = false;
                var startX = 0f;
                var startRightWidth = 0f;

                splitter.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startRightWidth = right.resolvedStyle.width;
                    splitter.CaptureMouse();
                    evt.StopPropagation();
                });

                splitter.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    if (!dragging) return;

                    var mainWidth = main.resolvedStyle.width;
                    var leftWidth = left.resolvedStyle.width;
                    var dx = evt.mousePosition.x - startX;

                    var maxRight = mainWidth - leftWidth - minCenterWidth - splitterWidth - splitterWidth;
                    var nextRight = Mathf.Clamp(startRightWidth - dx, minRightWidth, maxRight);
                    right.style.width = nextRight;
                    evt.StopPropagation();
                });

                splitter.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    if (!dragging) return;
                    dragging = false;
                    splitter.ReleaseMouse();
                    evt.StopPropagation();
                });
            }

            var splitterLC = CreateSplitter();
            var splitterCR = CreateSplitter();
            AttachLeftSplitterDrag(splitterLC);
            AttachRightSplitterDrag(splitterCR);

            _entityListRoot = new VisualElement { style = { flexGrow = 1 } };
            _entityFiltersContainer = new VisualElement { style = { flexGrow = 0 } };
            _entityGroupsContainer = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };

            _entityHeaderContainer = new VisualElement { style = { flexGrow = 0, flexDirection = FlexDirection.Row } };
            _entityHeaderContainer.style.paddingLeft = 6;
            _entityHeaderContainer.style.paddingRight = 6;
            _entityHeaderContainer.style.paddingTop = 6;
            _entityHeaderContainer.style.paddingBottom = 4;

            _contextEditorButton = new Button(() => ContextEditorClicked?.Invoke()) { text = "强化/构筑" };
            _contextEditorButton.style.flexGrow = 1;
            _contextEditorButton.style.display = DisplayStyle.None;
            _entityHeaderContainer.Add(_contextEditorButton);

            _entityListRoot.Add(_entityFiltersContainer);
            _entityListRoot.Add(_entityHeaderContainer);
            _entityListRoot.Add(_entityGroupsContainer);
            left.Add(_entityListRoot);

            _forestView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            center.Add(_forestView);

            _relationView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, display = DisplayStyle.None } };
            center.Add(_relationView);

            _detailsView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            right.Add(_detailsView);

            _issuesView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 0, height = 200, borderTopWidth = 1 } };
            right.Add(_issuesView);

            _debugView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 0, height = 160, borderTopWidth = 1 } };
            right.Add(_debugView);

            _detailsRenderer = new ExplainDetailsRenderer(_detailsView, a => ActionInvoked?.Invoke(a));
            _issuesRenderer = new ExplainIssuesRenderer(_issuesView, i => IssueInvoked?.Invoke(i));
            _debugRenderer = new ExplainDebugRenderer(_debugView);
            var nodeRowFactory = new ExplainNodeRowFactory(
                (n, dbl) => NodeInvoked?.Invoke(n, dbl),
                (n, menu) => NodeContextMenuPopulateRequested?.Invoke(n, menu));
            _forestRenderer = new ExplainForestRenderer(_forestView, nodeRowFactory, (d, expand) => DiscoveryToggleRequested?.Invoke(d, expand));
            _relationRenderer = new ExplainRelationRenderer(_relationView, nodeId =>
            {
                if (!string.IsNullOrEmpty(nodeId) && TryFocusNode(nodeId, out var _))
                {
                    SetSelectedNodeId(nodeId);
                }
            }, key => RelationEntityInvoked?.Invoke(key));

            main.Add(left);
            main.Add(splitterLC);
            main.Add(center);
            main.Add(splitterCR);
            main.Add(right);

            _root.Add(main);
        }

        public void SetRelationMode(bool enabled)
        {
            if (_forestView != null) _forestView.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
            if (_relationView != null) _relationView.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetRelationModeWithoutNotify(bool enabled)
        {
            if (_relationToggle != null) _relationToggle.SetValueWithoutNotify(enabled);
            SetRelationMode(enabled);
        }
    }
}
