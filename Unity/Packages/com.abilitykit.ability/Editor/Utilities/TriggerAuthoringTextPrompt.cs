#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringTextPrompt : EditorWindow
    {
        private string _label;
        private string _text;
        private Action<string> _onConfirm;
        private bool _didFocus;

        public static void Open(string title, string label, string defaultText, Action<string> onConfirm)
        {
            var window = CreateInstance<TriggerAuthoringTextPrompt>();
            window.titleContent = new GUIContent(title);
            window._label = label ?? string.Empty;
            window._text = defaultText ?? string.Empty;
            window._onConfirm = onConfirm;
            window.position = new Rect(Screen.width / 2f, Screen.height / 2f, 460f, 112f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(_label ?? string.Empty);
            GUI.SetNextControlName("TriggerAuthoringTextPromptField");
            _text = EditorGUILayout.TextField(_text ?? string.Empty);
            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            {
                Close();
                return;
            }
            if (GUILayout.Button("OK", GUILayout.Width(80f)))
                Confirm();
            EditorGUILayout.EndHorizontal();

            if (!_didFocus && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("TriggerAuthoringTextPromptField");
                _didFocus = true;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                Confirm();
        }

        private void Confirm()
        {
            var callback = _onConfirm;
            var value = _text;
            Close();
            callback?.Invoke(value);
        }
    }
}
#endif
