#if UNITY_EDITOR
using System;
using AbilityKit.Editor.Platform.Export;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// A shared, reusable host for a single <see cref="EditorExportReport"/>. Domain editors
    /// open it programmatically so export results are reported uniformly (Exported / Unchanged
    /// / Skipped / Failed) instead of through ad-hoc modal dialogs.
    /// </summary>
    public sealed class EditorExportReportWindow : EditorWindow
    {
        private EditorExportReport _report;
        private Vector2 _scroll;

        public static void Show(string title, EditorExportReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var window = GetWindow<EditorExportReportWindow>(true, string.IsNullOrWhiteSpace(title) ? "Export Report" : title);
            window._report = report;
            window.Repaint();
            window.Show();
        }

        private void OnGUI()
        {
            if (_report == null) return;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _report.Entries)
            {
                DrawEntry(entry);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawEntry(EditorExportReportEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.Target, EditorStyles.boldLabel);
            var previous = GUI.color;
            GUI.color = StatusColor(entry.Status);
            EditorGUILayout.LabelField(entry.Status.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            GUI.color = previous;
            EditorGUILayout.EndHorizontal();

            foreach (var artifact in entry.Artifacts)
            {
                var label = string.IsNullOrEmpty(artifact.Format) ? artifact.Path : artifact.Path + " (" + artifact.Format + ")";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }

            foreach (var message in entry.Messages)
            {
                if (entry.Status == EditorExportStatus.Failed)
                {
                    EditorGUILayout.HelpBox(message, MessageType.Error);
                }
                else
                {
                    EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static Color StatusColor(EditorExportStatus status)
        {
            switch (status)
            {
                case EditorExportStatus.Failed: return new Color(1f, 0.45f, 0.4f);
                case EditorExportStatus.Skipped: return new Color(1f, 0.75f, 0.3f);
                case EditorExportStatus.Unchanged: return new Color(0.7f, 0.7f, 0.7f);
                default: return new Color(0.45f, 0.85f, 0.5f);
            }
        }
    }
}
#endif
