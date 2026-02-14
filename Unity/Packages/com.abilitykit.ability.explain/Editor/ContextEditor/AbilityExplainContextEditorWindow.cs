using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    public sealed class AbilityExplainContextEditorWindow : EditorWindow
    {
        private VisualElement _contentRoot;

        public static AbilityExplainContextEditorWindow Open(string title, VisualElement content)
        {
            var w = CreateInstance<AbilityExplainContextEditorWindow>();
            w.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? "Context" : title);
            w.minSize = new Vector2(420, 260);
            w.ShowUtility();
            w.SetContent(content);
            w.CenterOnMainWin();
            return w;
        }

        private void OnEnable()
        {
            rootVisualElement.style.flexGrow = 1;
            _contentRoot = new VisualElement { style = { flexGrow = 1 } };
            rootVisualElement.Add(_contentRoot);
        }

        public void SetContent(VisualElement content)
        {
            if (_contentRoot == null) return;
            _contentRoot.Clear();
            if (content != null) _contentRoot.Add(content);
        }

        private void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var size = position.size;
            position = new Rect(
                main.x + (main.width - size.x) * 0.5f,
                main.y + (main.height - size.y) * 0.5f,
                size.x,
                size.y);
        }
    }
}
