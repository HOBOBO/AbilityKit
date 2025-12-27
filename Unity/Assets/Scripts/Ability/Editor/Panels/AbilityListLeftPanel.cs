using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityListLeftPanel
    {
        public static void Draw(AbilityListWindow window)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(6);

            DrawRootFolder(window);
            GUILayout.Space(4);
            DrawFilter(window);

            GUILayout.Space(6);
            GUILayout.EndVertical();
        }

        private static void DrawRootFolder(AbilityListWindow window)
        {
            EditorGUILayout.LabelField("RootFolder", EditorStyles.miniLabel);
            SirenixEditorGUI.BeginHorizontalToolbar();

            var folder = window.RootFolder;
            var newFolder = GUILayout.TextField(folder ?? string.Empty, GUI.skin.textField);

            if (SirenixEditorGUI.ToolbarButton("Pick"))
            {
                var abs = EditorUtility.OpenFolderPanel("Select Root Folder", Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(abs))
                {
                    abs = abs.Replace("\\", "/");
                    var dataPath = Application.dataPath.Replace("\\", "/");
                    if (abs.StartsWith(dataPath))
                    {
                        newFolder = "Assets" + abs.Substring(dataPath.Length);
                    }
                }
            }

            SirenixEditorGUI.EndHorizontalToolbar();

            if (!string.Equals(newFolder, folder, System.StringComparison.Ordinal))
            {
                window.RootFolder = string.IsNullOrEmpty(newFolder) ? "Assets" : newFolder;
            }
        }

        private static void DrawFilter(AbilityListWindow window)
        {
            EditorGUILayout.LabelField("Filter", EditorStyles.miniLabel);
            SirenixEditorGUI.BeginHorizontalToolbar();

            var filter = window.Filter;
            var newFilter = SirenixEditorGUI.ToolbarSearchField(filter);
            if (SirenixEditorGUI.ToolbarButton("Clear"))
            {
                newFilter = string.Empty;
            }

            SirenixEditorGUI.EndHorizontalToolbar();

            if (!string.Equals(newFilter, filter, System.StringComparison.Ordinal))
            {
                window.Filter = newFilter;
            }
        }
    }
}
