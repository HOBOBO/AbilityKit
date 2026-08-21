#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Windows
{
    /// <summary>引用搜索结果窗口：列出引用行，点击 Select 定位并 ping 模块资产。</summary>
    internal sealed class TriggerAuthoringReferenceWindow : EditorWindow
    {
        private readonly List<TriggerAuthoringReference> _references = new List<TriggerAuthoringReference>();
        private Vector2 _scroll;
        private string _title;

        public static void Show(List<TriggerAuthoringReference> references, string title)
        {
            var window = GetWindow<TriggerAuthoringReferenceWindow>(utility: true);
            window.titleContent = new GUIContent("Trigger References");
            window._references.Clear();
            if (references != null) window._references.AddRange(references);
            window._title = title ?? "References";
            window.minSize = new Vector2(480f, 200f);
            window.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{_references.Count} reference(s)", EditorStyles.miniLabel);
            GUILayout.Space(4f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_references.Count == 0)
            {
                EditorGUILayout.HelpBox("No references found in this project.", MessageType.Info);
            }
            for (var i = 0; i < _references.Count; i++)
            {
                var reference = _references[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(reference.BuildLabel(), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    Selection.activeObject = reference.Module;
                    EditorGUIUtility.PingObject(reference.Module);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
