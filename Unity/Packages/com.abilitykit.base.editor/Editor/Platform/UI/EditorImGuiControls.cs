#if UNITY_EDITOR
using System;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using AbilityKit.Editor.Platform.Synchronization;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Platform.UI
{
    public static class EditorImGuiControls
    {
        public static void DrawCommandToolbar(
            EditorCommandRegistry registry,
            IEditorLocalization localization,
            EditorCommandContext context = null,
            Predicate<EditorCommand> filter = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (localization == null) throw new ArgumentNullException(nameof(localization));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach (var command in registry.Commands)
            {
                if (filter != null && !filter(command)) continue;
                var previousEnabled = GUI.enabled;
                GUI.enabled = command.CanExecute(context);
                var content = new GUIContent(
                    localization.Get(command.LabelKey),
                    string.IsNullOrEmpty(command.TooltipKey) ? string.Empty : localization.Get(command.TooltipKey));
                var wasChecked = command.IsChecked(context);
                var pressed = GUILayout.Toggle(wasChecked, content, EditorStyles.toolbarButton);
                GUI.enabled = previousEnabled;
                if (pressed != wasChecked) command.TryExecute(context);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawSearch(EditorSearchState state, GUIContent label = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (label != null) GUILayout.Label(label, EditorStyles.miniLabel);
            state.Text = GUILayout.TextField(state.Text, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField);
            if (!state.IsEmpty && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22f))) state.Clear();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawTabs(
            EditorTabState state,
            IEditorLocalization localization,
            EditorCommandRegistry commands = null,
            EditorCommandContext context = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (localization == null) throw new ArgumentNullException(nameof(localization));

            state.EnsureValidSelection();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach (var tab in state.Tabs)
            {
                if (!(tab.IsVisible?.Invoke() ?? true)) continue;
                var previousEnabled = GUI.enabled;
                GUI.enabled = tab.IsEnabled?.Invoke() ?? true;
                var content = new GUIContent(
                    localization.Get(tab.LabelKey),
                    string.IsNullOrEmpty(tab.TooltipKey) ? string.Empty : localization.Get(tab.TooltipKey));
                var selected = string.Equals(tab.Id, state.SelectedId, StringComparison.Ordinal);
                if (GUILayout.Toggle(selected, content, EditorStyles.toolbarButton) != selected && state.Select(tab.Id))
                {
                    if (!string.IsNullOrEmpty(tab.CommandId)) commands?.Execute(tab.CommandId, context);
                }
                GUI.enabled = previousEnabled;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawEmptyState(
            IEditorLocalization localization,
            string titleKey,
            string messageKey = null,
            params GUILayoutOption[] options)
        {
            if (localization == null) throw new ArgumentNullException(nameof(localization));
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, options ?? Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            GUILayout.Label(localization.Get(titleKey), EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(messageKey))
            {
                GUILayout.Label(localization.Get(messageKey), EditorStyles.wordWrappedMiniLabel);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        public static void DrawStatusBadge(EditorStatusBadgeModel model, IEditorLocalization localization)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (localization == null) throw new ArgumentNullException(nameof(localization));
            var label = localization.Get(model.TextKey);
            if (model.Count.HasValue) label += " " + model.Count.Value;
            var tooltip = string.IsNullOrEmpty(model.TooltipKey) ? string.Empty : localization.Get(model.TooltipKey);
            var previous = GUI.color;
            GUI.color = StatusColor(model.Kind);
            GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.miniButton);
            GUI.color = previous;
        }

        public static void DrawDiagnostics(
            EditorDiagnosticCollection diagnostics,
            IEditorLocalization localization,
            EditorSearchState search,
            EditorDiagnosticSeverity minimumSeverity,
            ref Vector2 scroll)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (localization == null) throw new ArgumentNullException(nameof(localization));
            if (search == null) throw new ArgumentNullException(nameof(search));

            DrawSearch(search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var visible = diagnostics.Filter(minimumSeverity, search.Text);
            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox(localization.Get("abilitykit.editor.diagnostics.empty.message"), MessageType.Info);
            }
            else
            {
                foreach (var diagnostic in visible) DrawDiagnostic(diagnostic, localization);
            }
            EditorGUILayout.EndScrollView();
        }

        public static Rect DrawSplitter(Rect available, EditorSplitterState state, bool horizontal, ref bool dragging)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var divider = horizontal
                ? new Rect(available.x + state.Position - 2f, available.y, 4f, available.height)
                : new Rect(available.x, available.y + state.Position - 2f, available.width, 4f);
            EditorGUIUtility.AddCursorRect(divider, horizontal ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.25f));

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && divider.Contains(evt.mousePosition))
            {
                dragging = true;
                evt.Use();
            }
            else if (dragging && evt.type == EventType.MouseDrag)
            {
                state.Position = horizontal ? evt.mousePosition.x - available.x : evt.mousePosition.y - available.y;
                evt.Use();
            }
            else if (dragging && evt.type == EventType.MouseUp)
            {
                dragging = false;
                evt.Use();
            }

            return divider;
        }

        public static void DrawSourceSyncCard(EditorSourceSyncCardModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(model.Title, EditorStyles.boldLabel);
            var previousColor = GUI.color;
            GUI.color = SourceSyncColor(model.Inspection.State);
            EditorGUILayout.LabelField(model.StateLabel, EditorStyles.miniBoldLabel);
            GUI.color = previousColor;

            var messageType = model.Inspection.State switch
            {
                EditorSourceSyncState.Conflict => MessageType.Error,
                EditorSourceSyncState.SourceMissing => MessageType.Error,
                EditorSourceSyncState.InvalidSource => MessageType.Error,
                EditorSourceSyncState.Untracked => MessageType.Warning,
                EditorSourceSyncState.LocalChanged => MessageType.Warning,
                EditorSourceSyncState.SourceChanged => MessageType.Warning,
                _ => MessageType.Info
            };
            EditorGUILayout.HelpBox(model.StatusMessage, messageType);
            EditorGUILayout.LabelField(
                model.PathLabel,
                model.HasSourcePath ? model.SourcePath : model.UnboundLabel,
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!model.CanImport))
            {
                if (GUILayout.Button(model.ImportLabel, EditorStyles.miniButtonLeft)) model.Import?.Invoke();
            }
            using (new EditorGUI.DisabledScope(!model.CanExport))
            {
                if (GUILayout.Button(model.ExportLabel, EditorStyles.miniButtonRight)) model.Export?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!model.CanCopyPath))
            {
                if (GUILayout.Button(model.CopyPathLabel, EditorStyles.miniButtonLeft)) model.CopyPath?.Invoke();
            }
            using (new EditorGUI.DisabledScope(!model.CanRevealPath))
            {
                if (GUILayout.Button(model.RevealLabel, EditorStyles.miniButtonRight)) model.RevealPath?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawDiagnostic(EditorDiagnostic diagnostic, IEditorLocalization localization)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(diagnostic.Severity + "  " + diagnostic.Code, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(diagnostic.Message, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(diagnostic.Path)) EditorGUILayout.SelectableLabel(diagnostic.Path, EditorStyles.miniLabel, GUILayout.Height(16f));
            if (diagnostic.CanLocate || diagnostic.CanFix)
            {
                EditorGUILayout.BeginHorizontal();
                if (diagnostic.CanLocate && GUILayout.Button(localization.Get("abilitykit.editor.diagnostics.locate"), GUILayout.Width(80f)))
                {
                    diagnostic.Locate?.Invoke();
                    if (diagnostic.Target != null)
                    {
                        Selection.activeObject = diagnostic.Target;
                        EditorGUIUtility.PingObject(diagnostic.Target);
                    }
                }
                if (diagnostic.CanFix && GUILayout.Button(localization.Get("abilitykit.editor.diagnostics.fix"), GUILayout.Width(80f))) diagnostic.Fix();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private static Color SourceSyncColor(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.InSync => new Color(0.45f, 0.85f, 0.5f),
                EditorSourceSyncState.LocalChanged => new Color(1f, 0.75f, 0.3f),
                EditorSourceSyncState.SourceChanged => new Color(1f, 0.75f, 0.3f),
                EditorSourceSyncState.Conflict => new Color(1f, 0.45f, 0.4f),
                EditorSourceSyncState.SourceMissing => new Color(1f, 0.45f, 0.4f),
                EditorSourceSyncState.InvalidSource => new Color(1f, 0.45f, 0.4f),
                _ => Color.white
            };
        }

        private static Color StatusColor(EditorStatusKind kind)
        {
            switch (kind)
            {
                case EditorStatusKind.Success: return new Color(0.45f, 0.85f, 0.5f);
                case EditorStatusKind.Info: return new Color(0.45f, 0.7f, 1f);
                case EditorStatusKind.Warning: return new Color(1f, 0.75f, 0.3f);
                case EditorStatusKind.Error: return new Color(1f, 0.45f, 0.4f);
                default: return Color.white;
            }
        }
    }
}
#endif
