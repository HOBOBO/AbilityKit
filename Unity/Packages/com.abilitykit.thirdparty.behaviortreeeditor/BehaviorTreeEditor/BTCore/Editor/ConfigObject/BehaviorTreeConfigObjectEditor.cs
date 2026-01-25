using BTCore.Runtime;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BTCore.Editor
{
    [CustomEditor(typeof(BehaviorTreeConfigObject))]
    public class BehaviorTreeConfigObjectEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            var isChanged = false;
            var behavior = target as BehaviorTreeConfigObject;
            if (behavior != null)
            {
                var source = behavior.GetSource(true);
                GUI.enabled = false;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"), true);
                GUI.enabled = true;

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("Behavior Name", GUILayout.Width(120f));
                behavior.Name = EditorGUILayout.TextField(behavior.Name);
                if (EditorGUI.EndChangeCheck())
                {
                    isChanged = true;
                }

                if (GUILayout.Button("Open"))
                {
                    if (BTEditorWindow.Instance == null) {
                        BTEditorWindow.OpenWindow();
                    }
                    BTEditorWindow.Instance.SelectNewTree(source, behavior);
                }

                GUILayout.EndHorizontal();
                EditorGUI.BeginChangeCheck();
            }
        }
        
        [OnOpenAsset]
        public static bool ClickAction(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is not BehaviorTreeConfigObject behavior)
                return false;
            if (BTEditorWindow.Instance == null) {
                BTEditorWindow.OpenWindow();
            }
            BTEditorWindow.Instance.SelectNewTree(behavior.GetSource(),behavior);
            return true;
        }
    }
}