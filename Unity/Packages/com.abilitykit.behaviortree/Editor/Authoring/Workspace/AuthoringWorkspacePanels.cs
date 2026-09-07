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
        private readonly VisualElement _content = new();
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
            AddNodeList("Orphans", model.OrphanNodeIds);
            AddTextList("Subtrees", model.SubtreeReferences);
            AddSearchHits(model.Search);

            if (!model.ClipboardAvailable)
                AddMuted("Clipboard API: " + _presenter.Clipboard.Status);
        }

        private void BuildShell()
        {
            _root.style.paddingLeft = 8f;
            _root.style.paddingRight = 8f;
            _root.style.paddingTop = 6f;
            _root.style.paddingBottom = 6f;
            _root.style.borderBottomWidth = 1f;
            _root.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);

            _visibleToggle = new Toggle("Overview")
            {
                value = _state.GetPanelVisible(PanelId, true),
                tooltip = "Show tree overview and layout controls",
            };
            _visibleToggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _visibleToggle.RegisterValueChangedCallback(evt =>
            {
                _state.SetPanelVisible(PanelId, evt.newValue);
                _state.SetFoldoutExpanded(FoldoutId, evt.newValue);
                _content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _root.Add(_visibleToggle);
            _root.Add(_content);
        }

        private void AddMetricRow(AuthoringOverviewModel model)
        {
            AddMuted(
                $"{model.NodeCount} nodes  {model.EdgeCount} edges  {model.BlackboardKeyCount} keys  {model.DiagnosticErrorCount} errors");
            AddMuted($"{model.GroupCount} groups  {model.NoteCount} notes");
        }

        private void AddRootRow(AuthoringOverviewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RootNodeId))
            {
                AddMuted("Root: missing");
                return;
            }

            _content.Add(NodeButton(
                "Root: " + LabelFor(model.RootDisplayName, model.RootNodeId),
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
                text = "Layout",
                tooltip = "Run automatic layout",
            };
            layout.style.height = 22f;
            layout.style.marginRight = 4f;
            row.Add(layout);

            var layoutLocked = new Button(_layoutSelectionLocked)
            {
                text = "Keep Selected",
                tooltip = "Run layout while keeping selected nodes fixed",
            };
            layoutLocked.style.height = 22f;
            row.Add(layoutLocked);
            _content.Add(row);

            var lockSelection = new Toggle("Lock selected during panel layout")
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
            if (nodeIds.Count > max) AddMuted("+" + (nodeIds.Count - max) + " more");
        }

        private void AddTextList(string title, System.Collections.Generic.IReadOnlyList<string> values)
        {
            if (values.Count == 0) return;
            AddSectionTitle(title);
            var max = Math.Min(8, values.Count);
            for (var i = 0; i < max; i++) AddMuted(values[i]);
            if (values.Count > max) AddMuted("+" + (values.Count - max) + " more");
        }

        private void AddSearchHits(AuthoringSearchResult search)
        {
            if (search.Hits.Count == 0) return;
            AddSectionTitle(string.IsNullOrWhiteSpace(search.Query) ? "Nodes" : "Search");
            foreach (var hit in search.Hits)
            {
                var suffix = hit.IsRoot ? "  root" : hit.IsOrphan ? "  orphan" : string.Empty;
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
