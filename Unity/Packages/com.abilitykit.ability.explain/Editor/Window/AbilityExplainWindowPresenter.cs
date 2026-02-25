using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class AbilityExplainWindowPresenter : IDisposable
    {
        private readonly AbilityExplainWindowView _view;
        private readonly AbilityExplainWindowState _state = new AbilityExplainWindowState();

        private AbilityExplainContextEditorWindow _contextEditorWindow;

        private IExplainEntityListModule _entityListModule;

        private ExplainResolveRequest _lastResolveRequest;
        private ExplainResolveResult _lastResolveResult;
        private ExplainForest _lastForest;
        private bool _relationMode;

        private readonly Dictionary<string, ExplainTreeRoot> _relationExpandedRoots = new Dictionary<string, ExplainTreeRoot>();

        public AbilityExplainWindowPresenter(AbilityExplainWindowView view)
        {
            _view = view;
        }

        private void OnRelationEntityInvoked(PipelineItemKey key)
        {
            if (string.IsNullOrEmpty(key.Type) || string.IsNullOrEmpty(key.Id)) return;

            if (_relationMode)
            {
                if (TryExpandDiscoveredInline(in key)) return;
            }

            if (_relationMode)
            {
                _relationMode = false;
                _view.SetRelationModeWithoutNotify(false);
            }

            var extra = new Dictionary<string, string> { { "type", key.Type }, { "id", key.Id } };
            ExecuteAction(ExplainAction.Navigate("Focus", NavigationTarget.OpenEditor("focus_tree", extra)));
        }

        private bool TryExpandDiscoveredInline(in PipelineItemKey key)
        {
            var request = ExplainExpandRequest.For(key, options: BuildResolveOptions());
            var resolver = AbilityExplainRegistry.GetResolverForExpand(request);
            if (resolver == null) return false;
            if (!resolver.TryExpandDiscoveredRoot(request, out var root) || root == null || root.Root == null) return false;

            _state.SelectedNode = root.Root;
            _view.RenderNodeDetails(root.Root);

            if (_relationMode && _lastForest != null)
            {
                var k = key.ToString();
                if (!string.IsNullOrEmpty(k)) _relationExpandedRoots[k] = root;
                _view.RenderRelation(_lastForest, _relationExpandedRoots);
            }

            return true;
        }

        public void Initialize()
        {
            _view.SearchChanged += OnSearchChanged;
            _view.RefreshClicked += OnRefreshClicked;
            _view.OptionsChanged += OnOptionsChanged;
            _view.RelationModeChanged += OnRelationModeChanged;
            _view.RelationEntityInvoked += OnRelationEntityInvoked;
            _view.EntitySelected += OnEntitySelected;
            _view.ContextEditorClicked += OnContextEditorClicked;
            _view.NodeInvoked += OnNodeInvoked;
            _view.NodeContextMenuPopulateRequested += OnNodeContextMenuPopulateRequested;
            _view.DiscoveryToggleRequested += OnDiscoveryToggleRequested;
            _view.ActionInvoked += OnActionInvoked;
            _view.IssueInvoked += OnIssueInvoked;

            RefreshEntities();
        }

        public void Dispose()
        {
            _view.SearchChanged -= OnSearchChanged;
            _view.RefreshClicked -= OnRefreshClicked;
            _view.OptionsChanged -= OnOptionsChanged;
            _view.RelationModeChanged -= OnRelationModeChanged;
            _view.RelationEntityInvoked -= OnRelationEntityInvoked;
            _view.EntitySelected -= OnEntitySelected;
            _view.ContextEditorClicked -= OnContextEditorClicked;
            _view.NodeInvoked -= OnNodeInvoked;
            _view.NodeContextMenuPopulateRequested -= OnNodeContextMenuPopulateRequested;
            _view.DiscoveryToggleRequested -= OnDiscoveryToggleRequested;
            _view.ActionInvoked -= OnActionInvoked;
            _view.IssueInvoked -= OnIssueInvoked;

            if (_contextEditorWindow != null)
            {
                _contextEditorWindow.Close();
                _contextEditorWindow = null;
            }
        }

        private void OnContextEditorClicked()
        {
            if (!_state.SelectedEntity.HasValue) return;

            var key = _state.SelectedEntity.Value;
            var p = AbilityExplainRegistry.GetContextEditorProvider(in key);
            if (p == null) return;

            if (_contextEditorWindow != null)
            {
                _contextEditorWindow.Close();
                _contextEditorWindow = null;
            }

            var ctx = new ExplainContextEditorContext(
                in key,
                _state.ResolveContext,
                requestResolve: () => RefreshForest(),
                close: () =>
                {
                    if (_contextEditorWindow == null) return;
                    _contextEditorWindow.Close();
                    _contextEditorWindow = null;
                });

            var content = p.BuildEditor(ctx);
            _contextEditorWindow = AbilityExplainContextEditorWindow.Open(p.GetWindowTitle(in key), content);
        }

        private void OnSearchChanged(string _)
        {
            RefreshEntities();
        }

        private void OnRefreshClicked()
        {
            RefreshForest();
        }

        private void OnOptionsChanged()
        {
            RefreshForest();
        }

        private void OnRelationModeChanged(bool enabled)
        {
            _relationMode = enabled;
            _view.SetRelationMode(enabled);

            if (_lastForest != null)
            {
                if (_relationMode)
                {
                    _view.ClearForest();
                    _view.RenderRelation(_lastForest);
                }
                else
                {
                    _view.ClearRelation();
                    _view.RenderForest(_lastForest);
                }
            }
        }

        private void OnEntitySelected(PipelineItemKey key)
        {
            _state.SelectedEntity = key;
            _state.ResolveContext = ExplainResolveContext.For(key);
            RefreshEntities();
            RefreshForest();
        }

        private void OnNodeInvoked(ExplainNode node, bool isDoubleClick)
        {
            _state.SelectedNode = node;
            if (!string.IsNullOrEmpty(node?.NodeId)) _view.SetSelectedNodeId(node.NodeId);
            _view.RenderNodeDetails(node);

            if (!isDoubleClick) return;
            if (node?.Actions == null || node.Actions.Count <= 0) return;
            ExecuteAction(node.Actions[0]);
        }

        private void OnNodeContextMenuPopulateRequested(ExplainNode node, UnityEngine.UIElements.DropdownMenu menu)
        {
            if (node == null || menu == null) return;

            var ctx = new ExplainNodeContextMenuContext(_lastResolveRequest, _lastResolveResult);
            var ps = AbilityExplainRegistry.GetNodeContextMenuProviders(node, ctx);
            if (ps == null || ps.Count <= 0) return;

            for (var i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p == null) continue;
                p.BuildMenu(node, ctx, menu);
            }
        }

        private void OnDiscoveryToggleRequested(ExplainTreeDiscovery discovery, bool expand)
        {
            if (discovery == null) return;

            if (!expand)
            {
                _view.SetDiscoveryCollapsed(discovery);
                return;
            }

            var request = ExplainExpandRequest.For(discovery.Key, options: BuildResolveOptions());
            var resolver = AbilityExplainRegistry.GetResolverForExpand(request);
            if (resolver == null) return;
            if (!resolver.TryExpandDiscoveredRoot(request, out var root) || root == null) return;

            _view.SetDiscoveryExpanded(discovery, root);

            var nested = CollectNestedDiscoveries(root);
            if (nested != null && nested.Count > 0)
            {
                _view.AppendOrUpdateDiscoveries(nested);
            }
        }

        private static List<ExplainTreeDiscovery> CollectNestedDiscoveries(ExplainTreeRoot expandedRoot)
        {
            if (expandedRoot == null || expandedRoot.Root == null) return null;

            var set = new HashSet<string>();
            var list = new List<ExplainTreeDiscovery>();

            void AddKey(string type, string id)
            {
                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) return;

                var policy = AbilityExplainRegistry.GetDiscoveryPolicy();
                if (policy != null && !policy.IsDiscoverable(new PipelineItemKey(type, id))) return;

                var k = $"{type}:{id}";
                if (!set.Add(k)) return;

                list.Add(new ExplainTreeDiscovery
                {
                    Kind = type.ToLowerInvariant(),
                    Key = new PipelineItemKey(type, id),
                    Title = $"{type}: {type}#{id}",
                    RefCount = 1
                });
            }

            void Walk(ExplainNode n)
            {
                if (n == null) return;

                if (n.Source != null && n.Source.Kind == "table_row")
                {
                    AddKey(n.Source.TableName, n.Source.RowId);
                }

                if (n.Actions != null)
                {
                    for (var i = 0; i < n.Actions.Count; i++)
                    {
                        var t = n.Actions[i] != null ? n.Actions[i].NavigateTo : null;
                        if (t != null && t.Kind == "open_table_row")
                        {
                            AddKey(t.TableName, t.RowId);
                        }
                    }
                }

                if (n.Children == null) return;
                for (var i = 0; i < n.Children.Count; i++)
                {
                    Walk(n.Children[i]);
                }
            }

            Walk(expandedRoot.Root);

            var rootKey = expandedRoot.Key;
            if (!string.IsNullOrEmpty(rootKey.Type) && !string.IsNullOrEmpty(rootKey.Id))
            {
                list.RemoveAll(d => d != null && d.Key.Type == rootKey.Type && d.Key.Id == rootKey.Id);
            }

            return list;
        }

        private void OnActionInvoked(ExplainAction action)
        {
            ExecuteAction(action);
        }

        private void OnIssueInvoked(ExplainIssue issue)
        {
            if (issue == null) return;

            if (!string.IsNullOrEmpty(issue.NodeId) && _view.TryFocusNode(issue.NodeId, out var node) && node != null)
            {
                _state.SelectedNode = node;
                _view.RenderNodeDetails(node);
                return;
            }

            var target = issue.NavigateTo ?? TryConvertSourceToTarget(issue.Source);
            if (target == null) return;

            ExecuteAction(ExplainAction.Navigate(issue.Title ?? "Open", target));
        }

        private void ExecuteAction(ExplainAction action)
        {
            if (action == null || action.NavigateTo == null) return;

            if (TryHandleInWindowNavigation(action.NavigateTo)) return;

            var nav = AbilityExplainRegistry.GetNavigator();
            if (nav == null) return;
            if (!nav.CanNavigate(action.NavigateTo)) return;
            nav.Navigate(action.NavigateTo);
        }

        private bool TryHandleInWindowNavigation(NavigationTarget target)
        {
            if (target == null) return false;

            if (target.Kind == "open_editor" && target.EditorId == "focus_tree")
            {
                if (target.Extra == null) return true;
                target.Extra.TryGetValue("type", out var type);
                target.Extra.TryGetValue("id", out var id);

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) return true;

                if (!_view.IncludeDiscovered)
                {
                    _view.SetIncludeDiscoveredWithoutNotify(true);
                    RefreshForest();
                }

                if (_lastResolveResult == null || _lastResolveResult.Forest == null)
                {
                    RefreshForest();
                }

                var k = new PipelineItemKey(type, id);
                _view.TryFocusDiscovery(in k);

                var forest = _lastResolveResult != null ? _lastResolveResult.Forest : null;
                if (forest?.Discovered == null || forest.Discovered.Count <= 0) return true;

                ExplainTreeDiscovery discovery = null;
                for (var i = 0; i < forest.Discovered.Count; i++)
                {
                    var d = forest.Discovered[i];
                    if (d == null) continue;
                    if (d.Key.Type == k.Type && d.Key.Id == k.Id)
                    {
                        discovery = d;
                        break;
                    }
                }

                if (discovery == null) return true;

                var request = ExplainExpandRequest.For(discovery.Key, options: BuildResolveOptions());
                var resolver = AbilityExplainRegistry.GetResolverForExpand(request);
                if (resolver == null) return true;
                if (!resolver.TryExpandDiscoveredRoot(request, out var root) || root == null) return true;

                _view.SetDiscoveryExpanded(discovery, root);

                var nested = CollectNestedDiscoveries(root);
                if (nested != null && nested.Count > 0)
                {
                    _view.AppendOrUpdateDiscoveries(nested);
                }

                if (root.Root != null)
                {
                    _state.SelectedNode = root.Root;

                    var dk = $"{k.Type}:{k.Id}";
                    if (!string.IsNullOrEmpty(root.Root.NodeId))
                    {
                        _view.SetSelectedNodeId($"d:{dk}/{root.Root.NodeId}");
                    }

                    _view.RenderNodeDetails(root.Root);
                }

                return true;
            }

            return false;
        }

        private void RefreshEntities()
        {
            _state.Entities.Clear();

            var provider = AbilityExplainRegistry.GetEntityProvider();
            if (provider != null)
            {
                var q = provider.Query(_view.SearchText);
                if (q != null) _state.Entities.AddRange(q);
            }

            _entityListModule = AbilityExplainRegistry.GetEntityListModule(provider);
            if (_entityListModule != null)
            {
                var filters = _entityListModule.BuildFilters(new ExplainEntityListModuleContext(RefreshEntities));
                _view.SetEntityListFilters(filters);
            }
            else
            {
                _view.SetEntityListFilters(null);
            }

            var groups = _entityListModule != null
                ? _entityListModule.BuildGroups(provider, _state.Entities)
                : null;

            if (groups == null)
            {
                groups = new List<ExplainEntityListGroup>
                {
                    new ExplainEntityListGroup { Title = "", Items = new List<PipelineItemKey>(_state.Entities) }
                };
            }

            PipelineItemKey? selected = _state.SelectedEntity;
            if (selected.HasValue)
            {
                var found = false;
                for (var gi = 0; gi < groups.Count && !found; gi++)
                {
                    var g = groups[gi];
                    if (g?.Items == null) continue;
                    for (var i = 0; i < g.Items.Count; i++)
                    {
                        var k = g.Items[i];
                        if (k.Type == selected.Value.Type && k.Id == selected.Value.Id)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) selected = null;
            }

            RefreshContextEditorButton(selected);
            _view.RenderEntityGroups(groups, selected);

            if (!selected.HasValue)
            {
                var first = default(PipelineItemKey);
                var has = false;
                for (var gi = 0; gi < groups.Count && !has; gi++)
                {
                    var g = groups[gi];
                    if (g?.Items == null || g.Items.Count <= 0) continue;
                    first = g.Items[0];
                    has = true;
                }

                if (has)
                {
                    _state.SelectedEntity = first;
                    RefreshForest();
                    _view.RenderEntityGroups(groups, _state.SelectedEntity);
                }
                else
                {
                    _state.SelectedEntity = null;
                    RefreshForest();
                }
            }
        }

        private void RefreshForest()
        {
            _view.ClearForest();
            _view.ClearRelation();
            _view.ClearDetails();
            _view.ClearIssues();
            _view.SetForestDiffMap(null);

            if (_state.SelectedEntity == null)
            {
                _view.RenderMissingSetupHint();
                return;
            }

            if (!_state.ResolveContextIsBoundToSelectedEntity)
            {
                _state.ResolveContext = ExplainResolveContext.For(_state.SelectedEntity.Value);
            }

            var resolver = AbilityExplainRegistry.GetResolver();
            if (resolver == null)
            {
                _view.RenderMissingSetupHint();
                return;
            }

            var request = ExplainResolveRequest.For(_state.SelectedEntity.Value, context: _state.ResolveContext, options: BuildResolveOptions());
            if (!resolver.TryResolve(request, out var result) || result == null || result.Forest == null)
            {
                _view.RenderMissingSetupHint();
                return;
            }

            var forestToRender = result.Forest;
            if (IsShowDiffEnabled(_state.ResolveContext))
            {
                var baseCtx = ExplainResolveContext.For(_state.SelectedEntity.Value);
                var baseReq = ExplainResolveRequest.For(_state.SelectedEntity.Value, context: baseCtx, options: BuildResolveOptions());
                if (resolver.TryResolve(baseReq, out var baseResult) && baseResult != null && baseResult.Forest != null)
                {
                    var diffMap = ExplainForestDiff.BuildDiffMap(baseResult.Forest, result.Forest);
                    forestToRender = ExplainForestDiff.BuildForestWithRemoved(baseResult.Forest, result.Forest, diffMap);
                    _view.SetForestDiffMap(diffMap);
                }
            }

            _lastResolveRequest = request;
            _lastResolveResult = result;
            _lastForest = result.Forest;

            _view.SetDetailsContext(new ExplainDetailsContext(request, result));

            if (_relationMode)
            {
                _view.RenderRelation(result.Forest);
            }
            else
            {
                _view.RenderForest(result.Forest);
            }
            _view.RenderIssues(result.Issues);
        }

        private static bool IsShowDiffEnabled(ExplainResolveContext ctx)
        {
            if (ctx?.Values == null) return false;
            return ctx.Values.TryGetValue("ui_show_diff", out var v) && v == "1";
        }

        private void RefreshContextEditorButton(PipelineItemKey? selected)
        {
            if (!selected.HasValue)
            {
                _view.SetContextEditorButton(null, visible: false);
                return;
            }

            var key = selected.Value;
            var p = AbilityExplainRegistry.GetContextEditorProvider(in key);
            if (p == null)
            {
                _view.SetContextEditorButton(null, visible: false);
                return;
            }

            _view.SetContextEditorButton(p.GetButtonText(in key), visible: true);
        }

        private ExplainResolveOptions BuildResolveOptions()
        {
            return new ExplainResolveOptions
            {
                IncludeDiscovered = _view.IncludeDiscovered,
                MaxDepth = _view.MaxDepth
            };
        }

        private static NavigationTarget TryConvertSourceToTarget(ExplainSourceRef source)
        {
            if (source == null) return null;

            switch (source.Kind)
            {
                case "table_row":
                    return NavigationTarget.OpenTableRow(source.TableName, source.RowId, source.FieldPath);
                case "asset":
                    return NavigationTarget.OpenAsset(source.AssetGuid);
                case "file":
                    return NavigationTarget.OpenFile(source.FilePath, source.Line);
                default:
                    return null;
            }
        }

        private sealed class AbilityExplainWindowState
        {
            public readonly List<PipelineItemKey> Entities = new List<PipelineItemKey>();
            public PipelineItemKey? SelectedEntity;
            public ExplainNode SelectedNode;

            public ExplainResolveContext ResolveContext;

            public bool ResolveContextIsBoundToSelectedEntity
            {
                get
                {
                    return SelectedEntity.HasValue
                        && ResolveContext != null
                        && ResolveContext.Key.Type == SelectedEntity.Value.Type
                        && ResolveContext.Key.Id == SelectedEntity.Value.Id;
                }
            }
        }
    }
}
