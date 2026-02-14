using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainForestRenderer
    {
        private const int DiscoveryExpandedIndentPx = 16;
        private static readonly Color SelectedNodeColor = new Color(0.2f, 0.55f, 0.95f, 0.22f);

        private readonly ScrollView _forestView;
        private readonly ExplainNodeRowFactory _nodeRowFactory;
        private readonly Action<ExplainTreeDiscovery, bool> _onToggleDiscovery;

        private Label _discoveredHeader;

        private readonly Dictionary<string, DiscoveryGroupRef> _discoveryGroupIndex = new Dictionary<string, DiscoveryGroupRef>();

        private readonly Dictionary<string, NodeRowRef> _nodeIndex = new Dictionary<string, NodeRowRef>();
        private readonly Dictionary<string, DiscoveryRowRef> _discoveryIndex = new Dictionary<string, DiscoveryRowRef>();

        private string _selectedNodeId;

        public ExplainForestRenderer(
            ScrollView forestView,
            ExplainNodeRowFactory nodeRowFactory,
            Action<ExplainTreeDiscovery, bool> onToggleDiscovery)
        {
            _forestView = forestView;
            _nodeRowFactory = nodeRowFactory;
            _onToggleDiscovery = onToggleDiscovery;
        }

        public void Render(ExplainForest forest)
        {
            if (forest == null) return;

            _nodeIndex.Clear();
            _discoveryIndex.Clear();
            _discoveryGroupIndex.Clear();
            _discoveredHeader = null;
            _selectedNodeId = null;

            foreach (var root in forest.Roots)
            {
                AppendRoot(root);
            }

            if (forest.Discovered != null && forest.Discovered.Count > 0)
            {
                EnsureDiscoveredHeader();

                foreach (var d in forest.Discovered)
                {
                    AppendDiscovery(d);
                }
            }
        }

        public void AppendOrUpdateDiscoveries(List<ExplainTreeDiscovery> discoveries)
        {
            if (discoveries == null || discoveries.Count <= 0) return;

            EnsureDiscoveredHeader();

            foreach (var d in discoveries)
            {
                AppendDiscovery(d);
            }
        }

        public void SetDiscoveryExpanded(ExplainTreeDiscovery discovery, ExplainTreeRoot root)
        {
            if (discovery == null) return;

            var dk = GetDiscoveryKey(discovery);
            if (string.IsNullOrEmpty(dk)) return;
            if (!_discoveryIndex.TryGetValue(dk, out var r)) return;
            if (r.Row == null) return;

            if (r.Container == null)
            {
                r.Container = new VisualElement();
                r.Container.style.marginLeft = DiscoveryExpandedIndentPx;
                r.Container.style.marginTop = 2;
                r.Container.style.marginBottom = 6;

                var parent = r.ParentContainer ?? (VisualElement)_forestView;
                var insertIndex = parent.IndexOf(r.Row);
                if (insertIndex < 0) insertIndex = parent.childCount - 1;
                parent.Insert(insertIndex + 1, r.Container);
            }

            r.Container.style.display = DisplayStyle.Flex;
            r.Container.Clear();

            if (root != null)
            {
                var tmp = new ScrollView(ScrollViewMode.Vertical);
                tmp.style.flexGrow = 0;
                tmp.style.flexShrink = 0;

                var renderer = new ExplainForestRenderer(tmp, _nodeRowFactory, null);
                renderer.AppendRoot(root);

                var snapshot = new List<VisualElement>();
                foreach (var child in tmp.Children())
                {
                    snapshot.Add(child);
                }

                foreach (var child in snapshot)
                {
                    r.Container.Add(child);
                }
            }

            if (r.ToggleButton != null) r.ToggleButton.text = "收起";
            r.IsExpanded = true;
            _discoveryIndex[dk] = r;

            if (!string.IsNullOrEmpty(discovery.Key.Type))
            {
                ResortDiscoveryGroup(discovery.Key.Type);
            }
        }

        public void SetDiscoveryCollapsed(ExplainTreeDiscovery discovery)
        {
            if (discovery == null) return;

            var dk = GetDiscoveryKey(discovery);
            if (string.IsNullOrEmpty(dk)) return;
            if (!_discoveryIndex.TryGetValue(dk, out var r)) return;

            if (r.Container != null)
            {
                r.Container.style.display = DisplayStyle.None;
            }

            if (r.ToggleButton != null) r.ToggleButton.text = "展开";
            r.IsExpanded = false;
            _discoveryIndex[dk] = r;

            if (!string.IsNullOrEmpty(discovery.Key.Type))
            {
                ResortDiscoveryGroup(discovery.Key.Type);
            }
        }

        public void AppendRoot(ExplainTreeRoot root)
        {
            if (root == null) return;

            var title = new Label(root.Title);
            AbilityExplainStyles.ApplyTreeHeader(title);
            _forestView.Add(title);

            if (root.Root != null)
            {
                AppendNodeRecursive(root.Root, indent: 0);
            }
        }

        public bool TryFocusNode(string nodeId, out ExplainNode node)
        {
            node = null;
            if (string.IsNullOrEmpty(nodeId)) return false;

            if (!_nodeIndex.TryGetValue(nodeId, out var r)) return false;
            if (r.Row == null || r.Node == null) return false;

            node = r.Node;
            _forestView.ScrollTo(r.Row);

            SetSelectedNodeId(nodeId);

            var prev = r.Row.style.backgroundColor;
            r.Row.style.backgroundColor = new Color(1f, 0.95f, 0.5f, 0.35f);
            r.Row.schedule.Execute(() => r.Row.style.backgroundColor = prev).StartingIn(800);

            return true;
        }

        public void SetSelectedNodeId(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (_selectedNodeId == nodeId) return;

            if (!string.IsNullOrEmpty(_selectedNodeId) && _nodeIndex.TryGetValue(_selectedNodeId, out var prev) && prev.Row != null)
            {
                prev.Row.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            _selectedNodeId = nodeId;
            if (_nodeIndex.TryGetValue(nodeId, out var cur) && cur.Row != null)
            {
                cur.Row.style.backgroundColor = SelectedNodeColor;
            }
        }

        private void AppendDiscovery(ExplainTreeDiscovery d)
        {
            if (d == null) return;

            var key = GetDiscoveryKey(d);
            if (!string.IsNullOrEmpty(key) && _discoveryIndex.TryGetValue(key, out var existing))
            {
                existing.Discovery = d;
                if (existing.Label != null)
                {
                    existing.Label.text = $"{d.Title} (x{d.RefCount})";
                }
                _discoveryIndex[key] = existing;

                if (!string.IsNullOrEmpty(d.Key.Type))
                {
                    ResortDiscoveryGroup(d.Key.Type);
                }
                return;
            }

            var group = EnsureDiscoveryGroup(d);
            var parent = group.Container;

            var row = new VisualElement();
            AbilityExplainStyles.ApplyDiscoveryRow(row);

            var label = new Label($"{d.Title} (x{d.RefCount})") { style = { flexGrow = 1 } };
            row.Add(label);

            var btn = new Button(() =>
            {
                var dk = GetDiscoveryKey(d);
                if (string.IsNullOrEmpty(dk))
                {
                    _onToggleDiscovery?.Invoke(d, true);
                    return;
                }

                if (_discoveryIndex.TryGetValue(dk, out var r))
                {
                    _onToggleDiscovery?.Invoke(d, !r.IsExpanded);
                }
                else
                {
                    _onToggleDiscovery?.Invoke(d, true);
                }
            })
            {
                text = "展开"
            };
            btn.style.marginLeft = AbilityExplainStyles.Padding;
            row.Add(btn);

            parent.Add(row);

            if (!string.IsNullOrEmpty(key) && !_discoveryIndex.ContainsKey(key))
            {
                _discoveryIndex[key] = new DiscoveryRowRef
                {
                    Discovery = d,
                    Row = row,
                    Label = label,
                    ToggleButton = btn,
                    Container = null,
                    ParentContainer = parent,
                    IsExpanded = false
                };
            }

            if (!string.IsNullOrEmpty(d.Key.Type))
            {
                ResortDiscoveryGroup(d.Key.Type);
            }
        }

        private void EnsureDiscoveredHeader()
        {
            if (_discoveredHeader != null) return;

            _discoveredHeader = new Label("自动发现（默认折叠）");
            AbilityExplainStyles.ApplyTreeHeader(_discoveredHeader);
            _forestView.Add(_discoveredHeader);
        }

        private DiscoveryGroupRef EnsureDiscoveryGroup(ExplainTreeDiscovery d)
        {
            var groupKey = d != null && !string.IsNullOrEmpty(d.Key.Type) ? d.Key.Type : "(Unknown)";
            if (_discoveryGroupIndex.TryGetValue(groupKey, out var existing))
            {
                return existing;
            }

            var container = new Foldout { text = groupKey, value = true };
            container.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.style.marginTop = 4;
            container.style.marginBottom = 2;
            container.style.paddingLeft = AbilityExplainStyles.Padding;

            _forestView.Add(container);

            var g = new DiscoveryGroupRef
            {
                Type = groupKey,
                Container = container
            };

            _discoveryGroupIndex[groupKey] = g;
            return g;
        }

        private void ResortDiscoveryGroup(string type)
        {
            if (string.IsNullOrEmpty(type)) return;
            if (!_discoveryGroupIndex.TryGetValue(type, out var group)) return;
            if (group.Container == null) return;

            var items = new List<DiscoveryRowRef>();

            foreach (var kv in _discoveryIndex)
            {
                var r = kv.Value;
                if (r.Row == null) continue;
                if (r.Discovery.Key.Type != type) continue;
                items.Add(r);
            }

            if (items.Count <= 1) return;

            items.Sort(CompareDiscovery);

            group.Container.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                var r = items[i];
                if (r.Row != null) group.Container.Add(r.Row);
                if (r.Container != null)
                {
                    group.Container.Add(r.Container);
                }

                if (r.Row != null)
                {
                    var dk = GetDiscoveryKey(r.Discovery);
                    if (!string.IsNullOrEmpty(dk) && _discoveryIndex.ContainsKey(dk))
                    {
                        r.ParentContainer = group.Container;
                        _discoveryIndex[dk] = r;
                    }
                }
            }
        }

        private static int CompareDiscovery(DiscoveryRowRef a, DiscoveryRowRef b)
        {
            var ak = a.Discovery.Key;
            var bk = b.Discovery.Key;

            var aId = ak.Id ?? string.Empty;
            var bId = bk.Id ?? string.Empty;

            var aIsNum = long.TryParse(aId, out var aNum);
            var bIsNum = long.TryParse(bId, out var bNum);

            if (aIsNum && bIsNum)
            {
                var c = aNum.CompareTo(bNum);
                if (c != 0) return c;
            }

            if (aIsNum != bIsNum)
            {
                return aIsNum ? -1 : 1;
            }

            var s = string.Compare(aId, bId, StringComparison.Ordinal);
            if (s != 0) return s;

            return string.Compare(a.Discovery.Title, b.Discovery.Title, StringComparison.Ordinal);
        }

        private static string GetDiscoveryKey(ExplainTreeDiscovery d)
        {
            if (d == null) return null;
            return $"{d.Key.Type}:{d.Key.Id}";
        }

        private void AppendNodeRecursive(ExplainNode node, int indent)
        {
            var row = _nodeRowFactory.Create(node, indent);
            _forestView.Add(row);

            if (!string.IsNullOrEmpty(node.NodeId) && !_nodeIndex.ContainsKey(node.NodeId))
            {
                _nodeIndex[node.NodeId] = new NodeRowRef { Node = node, Row = row };
            }

            if (node.Children == null || node.Children.Count <= 0) return;
            foreach (var c in node.Children)
            {
                AppendNodeRecursive(c, indent + 1);
            }
        }

        private struct NodeRowRef
        {
            public ExplainNode Node;
            public VisualElement Row;
        }

        private struct DiscoveryRowRef
        {
            public ExplainTreeDiscovery Discovery;
            public VisualElement Row;
            public Label Label;
            public Button ToggleButton;
            public VisualElement Container;
            public VisualElement ParentContainer;
            public bool IsExpanded;
        }

        private struct DiscoveryGroupRef
        {
            public string Type;
            public Foldout Container;
        }
    }
}
