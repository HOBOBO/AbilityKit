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
        private VisualElement _entityListRoot;
        private VisualElement _entityFiltersContainer;
        private ScrollView _entityGroupsContainer;
        private ScrollView _forestView;
        private ScrollView _detailsView;
        private ScrollView _issuesView;
        private ScrollView _debugView;

        private ExplainForestRenderer _forestRenderer;
        private ExplainDetailsRenderer _detailsRenderer;
        private ExplainIssuesRenderer _issuesRenderer;
        private ExplainDebugRenderer _debugRenderer;

        private ExplainDetailsContext _detailsContext;

        public event Action<string> SearchChanged;
        public event Action RefreshClicked;
        public event Action OptionsChanged;
        public event Action<PipelineItemKey> EntitySelected;
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

        public void RenderNodeDetails(ExplainNode node)
        {
            _detailsRenderer?.Render(node, _detailsContext);
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
            _searchField.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));
            toolbar.Add(_searchField);

            _includeDiscoveredToggle = new Toggle("Discovered") { value = true };
            _includeDiscoveredToggle.style.marginLeft = 8;
            _includeDiscoveredToggle.RegisterValueChangedCallback(_ => OptionsChanged?.Invoke());
            toolbar.Add(_includeDiscoveredToggle);

            _maxDepthField = new IntegerField("MaxDepth") { value = 0 };
            _maxDepthField.style.marginLeft = 8;
            _maxDepthField.style.width = 140;
            _maxDepthField.RegisterValueChangedCallback(_ => OptionsChanged?.Invoke());
            toolbar.Add(_maxDepthField);

            var refreshBtn = new Button(() => RefreshClicked?.Invoke()) { text = "刷新" };
            refreshBtn.style.marginLeft = 8;
            toolbar.Add(refreshBtn);

            _root.Add(toolbar);

            var main = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

            var left = new VisualElement { style = { width = 260, flexShrink = 0, borderRightWidth = 1 } };
            var center = new VisualElement { style = { flexGrow = 1, borderRightWidth = 1 } };
            var right = new VisualElement { style = { width = 360, flexShrink = 0 } };

            _entityListRoot = new VisualElement { style = { flexGrow = 1 } };
            _entityFiltersContainer = new VisualElement { style = { flexGrow = 0 } };
            _entityGroupsContainer = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };

            _entityListRoot.Add(_entityFiltersContainer);
            _entityListRoot.Add(_entityGroupsContainer);
            left.Add(_entityListRoot);

            _forestView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            center.Add(_forestView);

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

            main.Add(left);
            main.Add(center);
            main.Add(right);

            _root.Add(main);
        }
    }
}
