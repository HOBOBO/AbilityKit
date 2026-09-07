using UnityEditor;
using UnityEngine;


namespace AbilityKit.HFSM.Editor
{

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
