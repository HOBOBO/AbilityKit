using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class UnityEditorContainer
    {
        private Dictionary<Object, UnityEditor.Editor> _editors = new Dictionary<Object, UnityEditor.Editor>();

        public void OnInspectorGUI(Object target)
        {
            if (target == null) return;
            UnityEditor.Editor editor = GetEditor(target);
            editor.OnInspectorGUI();
        }

        private UnityEditor.Editor GetEditor(Object target)
        {
            if (_editors == null) _editors = new Dictionary<Object, UnityEditor.Editor>();
            if (_editors.TryGetValue(target, out UnityEditor.Editor editor))
            {
                if (target != editor.target) editor = ResetEditor(target);
                return editor;
            }
            UnityEditor.Editor newEditor = UnityEditor.Editor.CreateEditor(target);
            _editors.Add(target, newEditor);
            return newEditor;
        }

        private UnityEditor.Editor ResetEditor(Object target)
        {
            if (_editors == null) return null;
            if (_editors.TryGetValue(target, out UnityEditor.Editor editor)) Object.DestroyImmediate(editor);
            UnityEditor.Editor newEditor = UnityEditor.Editor.CreateEditor(target);
            _editors[target] = newEditor;
            return newEditor;
        }

        public void DisposeTarget(Object target)
        {
            if (_editors == null) return;
            if (_editors.TryGetValue(target, out UnityEditor.Editor editor)) Object.DestroyImmediate(editor);
        }

        public void Dispose()
        {
            if (_editors == null) return;
            foreach (var pair in _editors) Object.DestroyImmediate(pair.Value);
            _editors.Clear();
        }
    }
}