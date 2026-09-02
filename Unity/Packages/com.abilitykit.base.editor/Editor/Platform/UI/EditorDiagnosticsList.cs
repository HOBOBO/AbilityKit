#if UNITY_EDITOR
using System;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.UI
{
    public sealed class EditorDiagnosticsList : VisualElement, IDisposable
    {
        private readonly EditorDiagnosticCollection _diagnostics;
        private readonly IEditorLocalization _localization;
        private readonly EditorSearchState _search;
        private readonly ToolbarMenu _severityMenu;
        private readonly EditorSearchField _searchField;
        private readonly ScrollView _items;
        private EditorEmptyState _emptyState;
        private EditorDiagnosticSeverity _minimumSeverity;
        private bool _disposed;

        public EditorDiagnosticsList(
            EditorDiagnosticCollection diagnostics,
            IEditorLocalization localization,
            EditorSearchState search = null,
            EditorDiagnosticSeverity minimumSeverity = EditorDiagnosticSeverity.Info)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _search = search ?? new EditorSearchState();
            _minimumSeverity = minimumSeverity;
            style.flexGrow = 1f;

            var toolbar = new Toolbar();
            _severityMenu = new ToolbarMenu();
            AppendSeverityAction(EditorDiagnosticSeverity.Info);
            AppendSeverityAction(EditorDiagnosticSeverity.Warning);
            AppendSeverityAction(EditorDiagnosticSeverity.Error);
            toolbar.Add(_severityMenu);
            _searchField = new EditorSearchField(_search, _localization.Get("abilitykit.editor.search.tooltip"));
            _searchField.style.flexGrow = 1f;
            toolbar.Add(_searchField);
            Add(toolbar);

            _items = new ScrollView { name = "diagnostic-items" };
            _items.style.flexGrow = 1f;
            Add(_items);

            _diagnostics.Changed += Refresh;
            _search.Changed += Refresh;
            _localization.LanguageChanged += Refresh;
            Refresh();
        }

        public EditorDiagnosticSeverity MinimumSeverity
        {
            get => _minimumSeverity;
            set
            {
                if (_minimumSeverity == value) return;
                _minimumSeverity = value;
                Refresh();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _diagnostics.Changed -= Refresh;
            _search.Changed -= Refresh;
            _localization.LanguageChanged -= Refresh;
            _searchField.Dispose();
            _emptyState?.Dispose();
            _emptyState = null;
        }

        private void AppendSeverityAction(EditorDiagnosticSeverity severity)
        {
            _severityMenu.menu.AppendAction(
                severity.ToString(),
                _ => MinimumSeverity = severity,
                _ => _minimumSeverity == severity
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private void Refresh()
        {
            if (_disposed) return;
            _severityMenu.text = _minimumSeverity.ToString();
            _emptyState?.Dispose();
            _emptyState = null;
            _items.Clear();
            var visible = _diagnostics.Filter(_minimumSeverity, _search.Text);
            if (visible.Count == 0)
            {
                _emptyState = new EditorEmptyState(
                    _localization,
                    "abilitykit.editor.diagnostics.empty.title",
                    "abilitykit.editor.diagnostics.empty.message");
                _items.Add(_emptyState);
                return;
            }

            foreach (var diagnostic in visible) _items.Add(CreateRow(diagnostic));
        }

        private VisualElement CreateRow(EditorDiagnostic diagnostic)
        {
            var row = new VisualElement { userData = diagnostic };
            row.AddToClassList("abilitykit-diagnostic-row");
            row.AddToClassList("abilitykit-diagnostic-row--" + diagnostic.Severity.ToString().ToLowerInvariant());

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            var severity = new Label(diagnostic.Severity.ToString());
            severity.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(severity);
            header.Add(new Label(diagnostic.Code));
            if (!string.IsNullOrEmpty(diagnostic.Path))
            {
                var path = new Label(diagnostic.Path);
                path.style.flexGrow = 1f;
                header.Add(path);
            }
            row.Add(header);

            var message = new Label(diagnostic.Message);
            message.style.whiteSpace = WhiteSpace.Normal;
            row.Add(message);

            if (diagnostic.CanLocate || diagnostic.CanFix)
            {
                var actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                if (diagnostic.CanLocate)
                {
                    actions.Add(new Button(() => Locate(diagnostic))
                    {
                        text = _localization.Get("abilitykit.editor.diagnostics.locate")
                    });
                }

                if (diagnostic.CanFix)
                {
                    actions.Add(new Button(diagnostic.Fix)
                    {
                        text = _localization.Get("abilitykit.editor.diagnostics.fix")
                    });
                }
                row.Add(actions);
            }

            return row;
        }

        private static void Locate(EditorDiagnostic diagnostic)
        {
            diagnostic.Locate?.Invoke();
            if (diagnostic.Target == null) return;
            Selection.activeObject = diagnostic.Target;
            EditorGUIUtility.PingObject(diagnostic.Target);
        }
    }
}
#endif
