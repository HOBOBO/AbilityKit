using System;
using System.Linq;
using AbilityKit.HFSM.Unity.Migration;
using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor.Diagnostics
{
    public sealed class HfsmDiagnosticsPanel
    {
        private HfsmEditorContext _context;
        private HfsmNextDiagnosticSnapshot _snapshot;
        private Vector2 _scrollPosition;
        private bool _needsRefresh = true;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private string _failure = string.Empty;
        private int _graphDirtyCount = -1;
        private int _catalogDirtyCount = -1;

        public HfsmNextDiagnosticSnapshot Snapshot => _snapshot;

        public void Initialize(HfsmEditorContext context)
        {
            if (_context != null)
            {
                _context.OnContextChanged -= Invalidate;
            }

            _context = context;
            if (_context != null)
            {
                _context.OnContextChanged += Invalidate;
            }
            Invalidate();
        }

        public void Dispose()
        {
            if (_context == null) return;
            _context.OnContextChanged -= Invalidate;
            _context = null;
        }

        public HfsmNextDiagnosticSnapshot Refresh()
        {
            _needsRefresh = false;
            _snapshot = null;
            _failure = string.Empty;
            if (_context?.GraphAsset == null) return null;

            _graphDirtyCount = EditorUtility.GetDirtyCount(_context.GraphAsset);
            var configuredCatalog = HfsmEditorBindingCatalog.ConfiguredAsset;
            _catalogDirtyCount = configuredCatalog == null
                ? -1
                : EditorUtility.GetDirtyCount(configuredCatalog);

            try
            {
                _snapshot = HfsmNextDiagnostics.Analyze(_context.GraphAsset);
            }
            catch (Exception exception)
            {
                _failure = exception.Message;
            }
            return _snapshot;
        }

        public void OnGUI()
        {
            DetectSerializedChanges();
            if (_needsRefresh && Event.current.type == EventType.Layout)
                Refresh();

            DrawToolbar();
            if (_context?.GraphAsset == null)
            {
                EditorGUILayout.HelpBox("No graph loaded.", MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(_failure))
            {
                EditorGUILayout.HelpBox(_failure, MessageType.Error);
                return;
            }

            if (_snapshot == null) return;
            DrawSummary();
            DrawIssues();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Next Diagnostics", EditorStyles.boldLabel, GUILayout.Width(95));

            var configured = HfsmEditorBindingCatalog.ConfiguredAsset;
            EditorGUI.BeginChangeCheck();
            var nextCatalog = EditorGUILayout.ObjectField(
                configured,
                typeof(HfsmBindingCatalogAsset),
                false,
                GUILayout.MinWidth(80)) as HfsmBindingCatalogAsset;
            if (EditorGUI.EndChangeCheck())
            {
                HfsmEditorBindingCatalog.SetConfiguredAsset(nextCatalog);
                Invalidate();
            }

            GUILayout.FlexibleSpace();
            _showErrors = GUILayout.Toggle(_showErrors, "Errors", EditorStyles.toolbarButton, GUILayout.Width(45));
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", EditorStyles.toolbarButton, GUILayout.Width(60));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(52)))
                Refresh();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal();
            var status = _snapshot.IsExportReady ? "Ready" : "Blocked";
            GUILayout.Label(status, EditorStyles.boldLabel, GUILayout.Width(55));
            GUILayout.Label($"Errors {_snapshot.ErrorCount}", GUILayout.Width(65));
            GUILayout.Label($"Warnings {_snapshot.WarningCount}", GUILayout.Width(85));
            GUILayout.Label("Catalog: " + _snapshot.CatalogSource, GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_snapshot.DefinitionHash))
                EditorGUILayout.SelectableLabel(
                    "Hash: " + _snapshot.DefinitionHash,
                    EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void DrawIssues()
        {
            var issues = _snapshot.Issues.Where(issue =>
                    (_showErrors && issue.Severity == HfsmLegacyImportSeverity.Error) ||
                    (_showWarnings && issue.Severity == HfsmLegacyImportSeverity.Warning))
                .ToArray();
            if (issues.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    _snapshot.IsExportReady ? "Next runtime definition is export-ready." : "No diagnostics match the active filters.",
                    _snapshot.IsExportReady ? MessageType.Info : MessageType.None);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var issue in issues)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(
                    issue.Severity == HfsmLegacyImportSeverity.Error ? "Error" : "Warning",
                    EditorStyles.boldLabel,
                    GUILayout.Width(55));
                GUILayout.Label(issue.Code, GUILayout.Width(90));
                EditorGUILayout.SelectableLabel(
                    issue.Message + "  " + issue.Path,
                    EditorStyles.wordWrappedMiniLabel,
                    GUILayout.MinHeight(30));

                var target = HfsmNextDiagnostics.ResolveTarget(issue.Path);
                EditorGUI.BeginDisabledGroup(!target.IsValid);
                if (GUILayout.Button("Focus", GUILayout.Width(52), GUILayout.Height(22)))
                    Focus(target);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Focus(HfsmDiagnosticTarget target)
        {
            if (_context == null || !target.IsValid) return;
            if (target.Kind == HfsmDiagnosticTargetKind.Node)
                _context.FocusNode(target.Id);
            else if (target.Kind == HfsmDiagnosticTargetKind.Transition)
                _context.FocusTransition(target.Id);
        }

        private void Invalidate()
        {
            _needsRefresh = true;
        }

        private void DetectSerializedChanges()
        {
            if (_context?.GraphAsset != null &&
                EditorUtility.GetDirtyCount(_context.GraphAsset) != _graphDirtyCount)
                _needsRefresh = true;

            var catalog = HfsmEditorBindingCatalog.ConfiguredAsset;
            var catalogDirtyCount = catalog == null ? -1 : EditorUtility.GetDirtyCount(catalog);
            if (catalogDirtyCount == _catalogDirtyCount) return;
            HfsmEditorBindingCatalog.Reset();
            _needsRefresh = true;
        }
    }
}
