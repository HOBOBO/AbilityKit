using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class EditorAssetWindowUtility
    {
        private static Dictionary<string, IEditorAssetWindow> _windows = new Dictionary<string, IEditorAssetWindow>();

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            int amount = windows.Length;
            for (int i = 0; i < amount; i++)
            {
                EditorWindow window = windows[i];
                IEditorAssetWindow editorAssetWindow = window as IEditorAssetWindow;
                if (editorAssetWindow == null) continue;
                if (string.IsNullOrEmpty(editorAssetWindow.id)) continue;
                _windows.Add(editorAssetWindow.id, editorAssetWindow);
            }
        }

        public static void Refresh(IEditorAssetWindow window)
        {
            _windows[window.id] = window;
        }

        public static void OpenWindow(Type windowType, string id, object arg = null)
        {
            if (_windows.TryGetValue(id, out IEditorAssetWindow editorAssetWindow))
            {
                if (editorAssetWindow.id == id)
                {
                    EditorWindow editorWindow = editorAssetWindow as EditorWindow;
                    if (editorWindow != null)
                    {
                        editorAssetWindow.OnReOpen(arg);
                        return;
                    }
                }
            }

            EditorWindow createWindow = CreateWindow(windowType);
            IEditorAssetWindow createEditorAssetWindow = createWindow as IEditorAssetWindow;
            createEditorAssetWindow.OnOpen(arg);

            _windows[createEditorAssetWindow.id] = createEditorAssetWindow;
        }

        private static EditorWindow CreateWindow(Type type)
        {
            ScriptableObject instance = ScriptableObject.CreateInstance(type);
            EditorWindow window = instance as EditorWindow;
            window.Show();
            return window;
        }
    }
}