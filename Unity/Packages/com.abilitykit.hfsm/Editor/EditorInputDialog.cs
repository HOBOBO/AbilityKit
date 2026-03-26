using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Popup window for text input dialog.
    /// </summary>
    public class InputDialogueWindow : EditorWindow
    {
        private string _title;
        private string _message;
        private string _inputValue;
        private System.Action<string> _onConfirm;
        private Vector2 _windowSize = new Vector2(300, 120);

        public static void Show(string title, string message, string defaultValue, System.Action<string> onConfirm)
        {
            var window = CreateInstance<InputDialogueWindow>();
            window._title = title;
            window._message = message;
            window._inputValue = defaultValue;
            window._onConfirm = onConfirm;
            window.titleContent = new GUIContent(title);
            window.minSize = window.maxSize = window._windowSize;
            window.position = new Rect(
                (Screen.currentResolution.width - window._windowSize.x) / 2,
                (Screen.currentResolution.height - window._windowSize.y) / 2,
                window._windowSize.x,
                window._windowSize.y
            );
            window.ShowPopup();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(_message, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            GUI.SetNextControlName("InputField");
            _inputValue = EditorGUILayout.TextField(_inputValue);

            EditorGUI.FocusTextInControl("InputField");

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                _onConfirm?.Invoke(_inputValue);
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnLostFocus()
        {
            Close();
        }
    }

    /// <summary>
    /// Simple input dialog helper for editor use.
    /// </summary>
    public static class EditorInputDialog
    {
        /// <summary>
        /// Shows a modal dialog with a text input field.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog message.</param>
        /// <param name="defaultValue">Default input value.</param>
        /// <param name="onConfirm">Callback when confirmed with the entered value.</param>
        public static void Show(string title, string message, string defaultValue, System.Action<string> onConfirm)
        {
            InputDialogueWindow.Show(title, message, defaultValue, onConfirm);
        }

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        public static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, message, "Yes", "No");
        }

        /// <summary>
        /// Shows a message dialog.
        /// </summary>
        public static void ShowMessage(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
