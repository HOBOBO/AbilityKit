using System;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.HFSM.Migration;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Editor.Diagnostics
{
    public sealed class DiagnosticsPanel
    {
        private EditorContext _context;
        private DiagnosticSnapshot _snapshot;
        private EditorDiagnosticCollection _diagnostics;
        private Vector2 _scrollPosition;
        private bool _needsRefresh = true;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private string _failure = string.Empty;
        private int _graphDirtyCount = -1;
        private int _catalogDirtyCount = -1;

        public DiagnosticSnapshot Snapshot => _snapshot;

        public void Initialize(EditorContext context)
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

        public DiagnosticSnapshot Refresh()
        {
            _needsRefresh = false;
            _snapshot = null;
            _diagnostics = null;
            _failure = string.Empty;
            if (_context?.GraphAsset == null) return null;

            _graphDirtyCount = EditorUtility.GetDirtyCount(_context.GraphAsset);
            var configuredCatalog = EditorBindingCatalog.ConfiguredAsset;
            _catalogDirtyCount = configuredCatalog == null
                ? -1
                : EditorUtility.GetDirtyCount(configuredCatalog);

            try
            {
                _snapshot = Diagnostics.Analyze(_context.GraphAsset);
                _diagnostics = _snapshot.ToPlatformDiagnostics(Focus);
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
                EditorGUILayout.HelpBox("尚未加载状态机图。", MessageType.Info);
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
            GUILayout.Label("Next 诊断", EditorStyles.boldLabel, GUILayout.Width(95));

            var configured = EditorBindingCatalog.ConfiguredAsset;
            EditorGUI.BeginChangeCheck();
            var nextCatalog = EditorGUILayout.ObjectField(
                configured,
                typeof(BindingCatalogAsset),
                false,
                GUILayout.MinWidth(80)) as BindingCatalogAsset;
            if (EditorGUI.EndChangeCheck())
            {
                EditorBindingCatalog.SetConfiguredAsset(nextCatalog);
                Invalidate();
            }

            GUILayout.FlexibleSpace();
            _showErrors = GUILayout.Toggle(_showErrors, "错误", EditorStyles.toolbarButton, GUILayout.Width(45));
            _showWarnings = GUILayout.Toggle(_showWarnings, "警告", EditorStyles.toolbarButton, GUILayout.Width(45));
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(45)))
                Refresh();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal();
            var status = _snapshot.IsExportReady ? "就绪" : "已阻止";
            GUILayout.Label(status, EditorStyles.boldLabel, GUILayout.Width(55));
            GUILayout.Label($"错误 {_diagnostics?.ErrorCount ?? 0}", GUILayout.Width(65));
            GUILayout.Label($"警告 {_diagnostics?.WarningCount ?? 0}", GUILayout.Width(65));
            GUILayout.Label("目录：" + _snapshot.CatalogSource, GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_snapshot.DefinitionHash))
                EditorGUILayout.SelectableLabel(
                    "哈希：" + _snapshot.DefinitionHash,
                    EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void DrawIssues()
        {
            var issues = (_diagnostics?.Items ?? Array.Empty<EditorDiagnostic>())
                .Where(issue =>
                    (_showErrors && issue.Severity == EditorDiagnosticSeverity.Error) ||
                    (_showWarnings && issue.Severity == EditorDiagnosticSeverity.Warning))
                .ToArray();
            if (issues.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    _snapshot.IsExportReady ? "Next Runtime 定义已可导出。" : "没有符合当前筛选条件的诊断项。",
                    _snapshot.IsExportReady ? MessageType.Info : MessageType.None);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var issue in issues)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(
                    issue.Severity == EditorDiagnosticSeverity.Error ? "错误" : "警告",
                    EditorStyles.boldLabel,
                    GUILayout.Width(55));
                GUILayout.Label(issue.Code, GUILayout.Width(90));
                EditorGUILayout.SelectableLabel(
                    issue.Message + "  " + issue.Path,
                    EditorStyles.wordWrappedMiniLabel,
                    GUILayout.MinHeight(30));

                EditorGUI.BeginDisabledGroup(!issue.CanLocate);
                if (GUILayout.Button("定位", GUILayout.Width(52), GUILayout.Height(22)))
                    issue.Locate?.Invoke();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Focus(DiagnosticTarget target)
        {
            if (_context == null || !target.IsValid) return;
            if (target.Kind == DiagnosticTargetKind.Node)
                _context.FocusNode(target.Id);
            else if (target.Kind == DiagnosticTargetKind.Transition)
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

            var catalog = EditorBindingCatalog.ConfiguredAsset;
            var catalogDirtyCount = catalog == null ? -1 : EditorUtility.GetDirtyCount(catalog);
            if (catalogDirtyCount == _catalogDirtyCount) return;
            EditorBindingCatalog.Reset();
            _needsRefresh = true;
        }
    }
}
