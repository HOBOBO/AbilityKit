#if UNITY_EDITOR
using System;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Diagnostics;
using UnityEditor;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// A shared, reusable host for a single <see cref="EditorDiagnosticCollection"/>. Domain
    /// editors open it programmatically to surface validation results through the platform
    /// diagnostics UI instead of modal string dialogs. It is intentionally not registered as
    /// a menu item — the collection to display is always supplied by a caller.
    /// </summary>
    public sealed class EditorDiagnosticsWindow : EditorWindow
    {
        private EditorDiagnosticCollection _diagnostics;
        private EditorDiagnosticsList _list;

        public static void Show(string title, EditorDiagnosticCollection diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var window = GetWindow<EditorDiagnosticsWindow>(true, string.IsNullOrWhiteSpace(title) ? "Diagnostics" : title);
            window._diagnostics = diagnostics;
            window.Refresh();
            window.Show();
        }

        private void CreateGUI()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_diagnostics == null) return;
            _list?.Dispose();
            _list = null;
            rootVisualElement.Clear();
            _list = new EditorDiagnosticsList(_diagnostics, AbilityKitEditorPlatform.Localization);
            _list.style.flexGrow = 1f;
            rootVisualElement.Add(_list);
        }

        private void OnDestroy()
        {
            _list?.Dispose();
            _list = null;
        }
    }
}
#endif
