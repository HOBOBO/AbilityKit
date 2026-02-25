using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Explain.Editor
{
    public sealed class AbilityExplainWindow : EditorWindow
    {
        private AbilityExplainWindowPresenter _presenter;

        [MenuItem("Tools/技能解释器")]
        public static void Open()
        {
            var w = GetWindow<AbilityExplainWindow>();
            w.titleContent = new GUIContent("技能解释器");
            w.minSize = new Vector2(1100, 650);
        }

        private void OnEnable()
        {
            var view = new AbilityExplainWindowView(rootVisualElement);
            _presenter = new AbilityExplainWindowPresenter(view);
            _presenter.Initialize();
        }

        private void OnDisable()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
