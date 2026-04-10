using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.GameplayTags.Editor
{
    internal sealed class GameplayTagTextPrompt : EditorWindow
    {
        private string _label;
        private string _text;
        private System.Action<string> _onOk;

        private bool _multiline;
        private Vector2 _scroll;
        private bool _didFocus;

        public static void Open(string title, string label, string defaultText, System.Action<string> onOk)
        {
            Open(title, label, defaultText, multiline: false, onOk: onOk);
        }

        public static void Open(string title, string label, string defaultText, bool multiline, System.Action<string> onOk)
        {
            var w = CreateInstance<GameplayTagTextPrompt>();
            w.titleContent = new UnityEngine.GUIContent(title);
            w._label = label;
            w._text = defaultText ?? string.Empty;
            w._onOk = onOk;
            w._multiline = multiline;
            w.position = multiline
                ? new Rect(Screen.width / 2f, Screen.height / 2f, 520, 260)
                : new Rect(Screen.width / 2f, Screen.height / 2f, 420, 110);
            w.ShowUtility();
            w.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_label ?? string.Empty);

            if (_multiline)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                GUI.SetNextControlName("GameplayTagTextPromptField");
                _text = EditorGUILayout.TextArea(_text ?? string.Empty, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                GUI.SetNextControlName("GameplayTagTextPromptField");
                _text = EditorGUILayout.TextField(_text ?? string.Empty);
            }

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    Close();
                    return;
                }

                if (GUILayout.Button("OK", GUILayout.Width(80)))
                {
                    var cb = _onOk;
                    var value = _text;
                    Close();
                    cb?.Invoke(value);
                }
            }

            if (!_didFocus && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("GameplayTagTextPromptField");
                _didFocus = true;
            }

            if (!_multiline && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                var cb = _onOk;
                var value = _text;
                Close();
                cb?.Invoke(value);
            }

            if (_multiline && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && Event.current.control)
            {
                var cb = _onOk;
                var value = _text;
                Close();
                cb?.Invoke(value);
            }
        }
    }
}
