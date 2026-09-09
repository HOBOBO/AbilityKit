#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringOverviewPanel
    {
        public const string PanelId = "overview";
        public const string FoldoutId = "overview";
        private readonly AuthoringWorkspacePresenter _presenter;
        private readonly AuthoringWorkspaceState _state;
        private readonly Action<string> _focusNode;
        private readonly Action _layoutAll;
        private readonly Action _layoutSelectionLocked;
        private readonly VisualElement _root = new();
        internal const float MaximumExpandedHeight = 220f;
        internal const string RootElementName = "bt-overview-panel";
        internal const string ContentElementName = "bt-overview-content";

        private readonly ScrollView _content = new(ScrollViewMode.Vertical);
        private Toggle? _visibleToggle;

        public AuthoringOverviewPanel(
            AuthoringWorkspacePresenter presenter,
            AuthoringWorkspaceState state,
            Action<string> focusNode,
            Action layoutAll,
            Action layoutSelectionLocked)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _focusNode = focusNode ?? throw new ArgumentNullException(nameof(focusNode));
            _layoutAll = layoutAll ?? throw new ArgumentNullException(nameof(layoutAll));
            _layoutSelectionLocked = layoutSelectionLocked ?? throw new ArgumentNullException(nameof(layoutSelectionLocked));
            BuildShell();
        }

        public VisualElement Root => _root;

        public void Refresh(string query)
        {
            var model = _presenter.BuildOverview(query);
            _content.Clear();
            _content.style.display = _state.GetPanelVisible(PanelId, true)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            AddMetricRow(model);
            AddRootRow(model);
            AddLayoutRow();
            AddNodeList("未连接节点", model.OrphanNodeIds);
            AddTextList("子树引用", model.SubtreeReferences);
            AddSearchHits(model.Search);

            if (!model.ClipboardAvailable)
                AddMuted("剪贴板: " + _presenter.Clipboard.Status);
        }

        private void BuildShell()
        {
            _root.name = RootElementName;
            _root.style.maxHeight = MaximumExpandedHeight;
            _root.style.flexShrink = 0f;
            _root.style.overflow = Overflow.Hidden;
            _root.style.paddingLeft = 8f;
            _root.style.paddingRight = 8f;
            _root.style.paddingTop = 6f;
            _root.style.paddingBottom = 6f;
            _root.style.borderBottomWidth = 1f;
            _root.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);

            _visibleToggle = new Toggle("概览")
            {
                value = _state.GetPanelVisible(PanelId, true),
                tooltip = "显示树概览与布局操作",
            };
            _visibleToggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _visibleToggle.RegisterValueChangedCallback(evt =>
            {
                _state.SetPanelVisible(PanelId, evt.newValue);
                _state.SetFoldoutExpanded(FoldoutId, evt.newValue);
                _content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _root.Add(_visibleToggle);
            _content.name = ContentElementName;
            _content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _content.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _content.style.flexGrow = 1f;
            _content.style.flexShrink = 1f;
            _content.style.minHeight = 0f;
            _root.Add(_content);
        }

        private void AddMetricRow(AuthoringOverviewModel model)
        {
            AddMuted(
                $"{model.NodeCount} 节点  ·  {model.EdgeCount} 连线  ·  {model.BlackboardKeyCount} 黑板键");
            AddMuted($"{model.GroupCount} 分组  ·  {model.NoteCount} 注释  ·  {model.DiagnosticErrorCount} 错误");
        }

        private void AddRootRow(AuthoringOverviewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RootNodeId))
            {
                AddMuted("根节点缺失");
                return;
            }

            _content.Add(NodeButton(
                "根节点: " + LabelFor(model.RootDisplayName, model.RootNodeId),
                model.RootNodeId));
        }

        private void AddLayoutRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4f } };
            var layout = new Button(() =>
            {
                if (_state.GetFoldoutExpanded("layout.lock-selection", false)) _layoutSelectionLocked();
                else _layoutAll();
            })
            {
                text = "自动布局",
                tooltip = "整理全部节点",
            };
            layout.style.height = 22f;
            layout.style.marginRight = 4f;
            layout.style.flexGrow = 1f;
            row.Add(layout);

            var layoutLocked = new Button(_layoutSelectionLocked)
            {
                text = "保持选中",
                tooltip = "固定选中节点后整理布局",
            };
            layoutLocked.style.height = 22f;
            layoutLocked.style.flexGrow = 1f;
            row.Add(layoutLocked);
            _content.Add(row);

            var lockSelection = new Toggle("布局时固定选中节点")
            {
                value = _state.GetFoldoutExpanded("layout.lock-selection", false),
            };
            lockSelection.RegisterValueChangedCallback(evt =>
                _state.SetFoldoutExpanded("layout.lock-selection", evt.newValue));
            _content.Add(lockSelection);
        }

        private void AddNodeList(string title, System.Collections.Generic.IReadOnlyList<string> nodeIds)
        {
            if (nodeIds.Count == 0) return;
            AddSectionTitle(title);
            var max = Math.Min(8, nodeIds.Count);
            for (var i = 0; i < max; i++) _content.Add(NodeButton(nodeIds[i], nodeIds[i]));
            if (nodeIds.Count > max) AddMuted("另有 " + (nodeIds.Count - max) + " 项");
        }

        private void AddTextList(string title, System.Collections.Generic.IReadOnlyList<string> values)
        {
            if (values.Count == 0) return;
            AddSectionTitle(title);
            var max = Math.Min(8, values.Count);
            for (var i = 0; i < max; i++) AddMuted(values[i]);
            if (values.Count > max) AddMuted("另有 " + (values.Count - max) + " 项");
        }

        private void AddSearchHits(AuthoringSearchResult search)
        {
            // The canvas already shows every node. The overview only needs a result list
            // after the user enters a query; rendering all nodes here consumes the inspector.
            if (string.IsNullOrWhiteSpace(search.Query)) return;
            if (search.Hits.Count == 0) return;
            AddSectionTitle("搜索结果");
            foreach (var hit in search.Hits)
            {
                var suffix = hit.IsRoot ? "  根" : hit.IsOrphan ? "  未连接" : string.Empty;
                _content.Add(NodeButton(LabelFor(hit.DisplayName, hit.NodeId) + suffix, hit.NodeId));
            }
        }

        private Button NodeButton(string text, string nodeId)
        {
            var button = new Button(() => _focusNode(nodeId))
            {
                text = text,
                tooltip = nodeId,
            };
            button.style.height = 22f;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginTop = 1f;
            button.style.marginBottom = 1f;
            return button;
        }

        private void AddSectionTitle(string text)
        {
            _content.Add(new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 6f,
                    marginBottom = 2f,
                },
            });
        }

        private void AddMuted(string text)
        {
            _content.Add(new Label(text)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    opacity = 0.72f,
                    marginTop = 1f,
                    marginBottom = 1f,
                },
            });
        }

        private static string LabelFor(string displayName, string nodeId)
        {
            return string.IsNullOrWhiteSpace(displayName) ? nodeId : displayName + "  (" + nodeId + ")";
        }
    }
}
