#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Localization;
using AbilityKit.Editor.Platform.Synchronization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.UI
{
    public sealed class EditorCommandToolbar : Toolbar, IDisposable
    {
        private readonly EditorCommandRegistry _registry;
        private readonly IEditorLocalization _localization;
        private readonly Func<EditorCommandContext> _context;
        private readonly Predicate<EditorCommand> _filter;
        private bool _disposed;

        public EditorCommandToolbar(
            EditorCommandRegistry registry,
            IEditorLocalization localization,
            Func<EditorCommandContext> context = null,
            Predicate<EditorCommand> filter = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _context = context;
            _filter = filter;
            name = "abilitykit-command-toolbar";
            _registry.CommandsChanged += Rebuild;
            _localization.LanguageChanged += Rebuild;
            Rebuild();
        }

        public void RefreshState()
        {
            var context = _context?.Invoke();
            foreach (var child in Children())
            {
                if (!(child is Button button) || !(button.userData is EditorCommand command)) continue;
                button.SetEnabled(command.CanExecute(context));
                button.EnableInClassList("abilitykit-command-toolbar__button--checked", command.IsChecked(context));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.CommandsChanged -= Rebuild;
            _localization.LanguageChanged -= Rebuild;
        }

        private void Rebuild()
        {
            if (_disposed) return;
            Clear();
            foreach (var command in _registry.Commands)
            {
                if (_filter != null && !_filter(command)) continue;
                var captured = command;
                var button = new ToolbarButton(() => captured.TryExecute(_context?.Invoke()))
                {
                    text = _localization.Get(captured.LabelKey),
                    tooltip = string.IsNullOrEmpty(captured.TooltipKey)
                        ? string.Empty
                        : _localization.Get(captured.TooltipKey),
                    userData = captured
                };
                button.AddToClassList("abilitykit-command-toolbar__button");
                Add(button);
            }

            RefreshState();
        }
    }

    public sealed class EditorSearchField : VisualElement, IDisposable
    {
        private readonly EditorSearchState _state;
        private readonly ToolbarSearchField _field;
        private bool _synchronizing;
        private bool _disposed;

        public EditorSearchField(EditorSearchState state, string tooltip = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _field = new ToolbarSearchField { value = state.Text, tooltip = tooltip ?? string.Empty };
            _field.RegisterValueChangedCallback(OnFieldChanged);
            _state.Changed += OnStateChanged;
            Add(_field);
        }

        public ToolbarSearchField Field => _field;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _field.UnregisterValueChangedCallback(OnFieldChanged);
            _state.Changed -= OnStateChanged;
        }

        private void OnFieldChanged(ChangeEvent<string> evt)
        {
            if (_synchronizing) return;
            _state.Text = evt.newValue;
        }

        private void OnStateChanged()
        {
            if (_disposed || string.Equals(_field.value, _state.Text, StringComparison.Ordinal)) return;
            _synchronizing = true;
            _field.SetValueWithoutNotify(_state.Text);
            _synchronizing = false;
        }
    }

    public enum EditorSplitterOrientation
    {
        Horizontal,
        Vertical
    }

    public sealed class EditorSplitter : VisualElement, IDisposable
    {
        private const float DividerSize = 4f;
        private readonly EditorSplitterState _state;
        private readonly EditorSplitterOrientation _orientation;
        private readonly VisualElement _firstPane;
        private readonly VisualElement _divider;
        private readonly VisualElement _secondPane;
        private bool _dragging;
        private bool _disposed;

        public EditorSplitter(
            EditorSplitterState state,
            EditorSplitterOrientation orientation = EditorSplitterOrientation.Horizontal)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _orientation = orientation;
            style.flexGrow = 1f;
            style.flexDirection = orientation == EditorSplitterOrientation.Horizontal
                ? FlexDirection.Row
                : FlexDirection.Column;

            _firstPane = new VisualElement { name = "first-pane" };
            _divider = new VisualElement { name = "divider" };
            _secondPane = new VisualElement { name = "second-pane" };
            _firstPane.style.flexShrink = 0f;
            _secondPane.style.flexGrow = 1f;
            _secondPane.style.minWidth = 0f;
            _secondPane.style.minHeight = 0f;
            _divider.style.flexShrink = 0f;
            _divider.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);

            if (orientation == EditorSplitterOrientation.Horizontal)
            {
                _divider.style.width = DividerSize;
            }
            else
            {
                _divider.style.height = DividerSize;
            }

            hierarchy.Add(_firstPane);
            hierarchy.Add(_divider);
            hierarchy.Add(_secondPane);
            _divider.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _divider.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _divider.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _state.Changed += ApplyPosition;
            ApplyPosition();
        }

        public VisualElement FirstPane => _firstPane;
        public VisualElement SecondPane => _secondPane;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _divider.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _divider.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _divider.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _state.Changed -= ApplyPosition;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _dragging = true;
            _divider.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || !_divider.HasPointerCapture(evt.pointerId)) return;
            var local = this.WorldToLocal(evt.position);
            _state.Position = _orientation == EditorSplitterOrientation.Horizontal ? local.x : local.y;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging || evt.button != 0) return;
            _dragging = false;
            if (_divider.HasPointerCapture(evt.pointerId)) _divider.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void ApplyPosition()
        {
            if (_orientation == EditorSplitterOrientation.Horizontal)
            {
                _firstPane.style.width = _state.Position;
            }
            else
            {
                _firstPane.style.height = _state.Position;
            }
        }
    }

    public sealed class EditorTabs : VisualElement, IDisposable
    {
        private readonly EditorTabState _state;
        private readonly IEditorLocalization _localization;
        private readonly EditorCommandRegistry _commands;
        private readonly Func<EditorCommandContext> _context;
        private bool _disposed;

        public EditorTabs(
            EditorTabState state,
            IEditorLocalization localization,
            EditorCommandRegistry commands = null,
            Func<EditorCommandContext> context = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _commands = commands;
            _context = context;
            style.flexDirection = FlexDirection.Row;
            AddToClassList("abilitykit-tabs");
            _state.Changed += Rebuild;
            _localization.LanguageChanged += Rebuild;
            if (_commands != null) _commands.CommandsChanged += Rebuild;
            Rebuild();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _state.Changed -= Rebuild;
            _localization.LanguageChanged -= Rebuild;
            if (_commands != null) _commands.CommandsChanged -= Rebuild;
        }

        public void RefreshState()
        {
            _state.EnsureValidSelection();
            Rebuild();
        }

        private void Rebuild()
        {
            if (_disposed) return;
            Clear();
            foreach (var tab in _state.Tabs)
            {
                if (!(tab.IsVisible?.Invoke() ?? true)) continue;
                var captured = tab;
                var button = new Button(() => Select(captured))
                {
                    text = _localization.Get(tab.LabelKey),
                    tooltip = string.IsNullOrEmpty(tab.TooltipKey) ? string.Empty : _localization.Get(tab.TooltipKey),
                    userData = tab
                };
                button.SetEnabled(tab.IsEnabled?.Invoke() ?? true);
                button.EnableInClassList("abilitykit-tabs__tab--selected",
                    string.Equals(tab.Id, _state.SelectedId, StringComparison.Ordinal));
                button.AddToClassList("abilitykit-tabs__tab");
                Add(button);
            }
        }

        private void Select(EditorTabDescriptor tab)
        {
            if (!_state.Select(tab.Id)) return;
            if (!string.IsNullOrEmpty(tab.CommandId)) _commands?.Execute(tab.CommandId, _context?.Invoke());
        }
    }

    public sealed class EditorEmptyState : VisualElement, IDisposable
    {
        private readonly IEditorLocalization _localization;
        private readonly string _titleKey;
        private readonly string _messageKey;
        private readonly Label _title;
        private readonly Label _message;
        private bool _disposed;

        public EditorEmptyState(IEditorLocalization localization, string titleKey, string messageKey = null)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _titleKey = titleKey ?? string.Empty;
            _messageKey = messageKey ?? string.Empty;
            _title = new Label();
            _message = new Label();
            AddToClassList("abilitykit-empty-state");
            _title.AddToClassList("abilitykit-empty-state__title");
            _message.AddToClassList("abilitykit-empty-state__message");
            Add(_title);
            Add(_message);
            _localization.LanguageChanged += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _localization.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            _title.text = string.IsNullOrEmpty(_titleKey) ? string.Empty : _localization.Get(_titleKey);
            _message.text = string.IsNullOrEmpty(_messageKey) ? string.Empty : _localization.Get(_messageKey);
            _message.style.display = string.IsNullOrEmpty(_message.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>
    /// Domain-neutral UI Toolkit view for source synchronization status and actions.
    /// Refresh, confirmation, validation, and IO remain owned by the domain host.
    /// </summary>
    public sealed class EditorSourceSyncCard : VisualElement
    {
        private readonly EditorSourceSyncCardModel _model;

        public EditorSourceSyncCard(EditorSourceSyncCardModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            name = "abilitykit-source-sync-card";
            AddToClassList("abilitykit-source-sync-card");
            style.paddingLeft = 6f;
            style.paddingRight = 6f;
            style.paddingTop = 6f;
            style.paddingBottom = 6f;
            style.marginTop = 2f;
            style.marginBottom = 2f;
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = new Color(0f, 0f, 0f, 0.25f);
            style.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);
            style.borderLeftColor = new Color(0f, 0f, 0f, 0.25f);
            style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);

            Add(CreateHeader());
            Add(CreateMessage());
            Add(CreatePath());
            Add(CreateActionRow());
            Add(CreatePathActionRow());
        }

        public EditorSourceSyncCardModel Model => _model;

        private VisualElement CreateHeader()
        {
            var row = CreateRow("abilitykit-source-sync-card__header");
            var title = new Label(_model.Title)
            {
                name = "source-sync-title"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;

            var status = new Label(_model.StateLabel)
            {
                name = "source-sync-status"
            };
            status.style.unityFontStyleAndWeight = FontStyle.Bold;
            status.style.color = SourceSyncColor(_model.Inspection.State);
            row.Add(title);
            row.Add(status);
            return row;
        }

        private VisualElement CreateMessage()
        {
            var message = new Label(_model.StatusMessage)
            {
                name = "source-sync-message"
            };
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.marginTop = 4f;
            message.style.marginBottom = 4f;
            message.style.color = SourceSyncColor(_model.Inspection.State);
            return message;
        }

        private VisualElement CreatePath()
        {
            var row = CreateRow("abilitykit-source-sync-card__path");
            row.Add(new Label(_model.PathLabel)
            {
                name = "source-sync-path-label"
            });

            var path = new Label(_model.HasSourcePath ? _model.SourcePath : _model.UnboundLabel)
            {
                name = "source-sync-path"
            };
            path.style.flexGrow = 1f;
            path.style.marginLeft = 6f;
            path.style.whiteSpace = WhiteSpace.Normal;
            row.Add(path);
            return row;
        }

        private VisualElement CreateActionRow()
        {
            var row = CreateRow("abilitykit-source-sync-card__actions");
            row.style.marginTop = 4f;
            row.Add(CreateButton(_model.ImportLabel, "source-sync-import", _model.Import, _model.CanImport));
            row.Add(CreateButton(_model.ExportLabel, "source-sync-export", _model.Export, _model.CanExport));
            return row;
        }

        private VisualElement CreatePathActionRow()
        {
            var row = CreateRow("abilitykit-source-sync-card__path-actions");
            row.style.marginTop = 2f;
            row.Add(CreateButton(_model.CopyPathLabel, "source-sync-copy-path", _model.CopyPath, _model.CanCopyPath));
            row.Add(CreateButton(_model.RevealLabel, "source-sync-reveal-path", _model.RevealPath, _model.CanRevealPath));
            return row;
        }

        private static VisualElement CreateRow(string className)
        {
            var row = new VisualElement();
            row.AddToClassList(className);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static Button CreateButton(
            string text,
            string elementName,
            Action action,
            bool enabled)
        {
            var button = new Button(() => action?.Invoke())
            {
                name = elementName,
                text = text
            };
            button.style.flexGrow = 1f;
            button.SetEnabled(enabled);
            return button;
        }

        private static Color SourceSyncColor(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.InSync => new Color(0.35f, 0.75f, 0.42f),
                EditorSourceSyncState.LocalChanged => new Color(0.9f, 0.62f, 0.15f),
                EditorSourceSyncState.SourceChanged => new Color(0.9f, 0.62f, 0.15f),
                EditorSourceSyncState.Untracked => new Color(0.9f, 0.62f, 0.15f),
                EditorSourceSyncState.Conflict => new Color(0.9f, 0.28f, 0.25f),
                EditorSourceSyncState.SourceMissing => new Color(0.9f, 0.28f, 0.25f),
                EditorSourceSyncState.InvalidSource => new Color(0.9f, 0.28f, 0.25f),
                _ => Color.white
            };
        }
    }

    public sealed class EditorStatusBadge : Label, IDisposable
    {
        private readonly EditorStatusBadgeModel _model;
        private readonly IEditorLocalization _localization;
        private bool _disposed;

        public EditorStatusBadge(EditorStatusBadgeModel model, IEditorLocalization localization)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            AddToClassList("abilitykit-status-badge");
            AddToClassList("abilitykit-status-badge--" + model.Kind.ToString().ToLowerInvariant());
            _localization.LanguageChanged += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _localization.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            var value = _localization.Get(_model.TextKey);
            text = _model.Count.HasValue ? value + " " + _model.Count.Value : value;
            tooltip = string.IsNullOrEmpty(_model.TooltipKey) ? string.Empty : _localization.Get(_model.TooltipKey);
        }
    }
}
#endif
