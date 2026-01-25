//------------------------------------------------------------
//        File:  BehaviorTreeInspector.cs
//       Brief:  BehaviorTreeInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-29
//============================================================

using BTCore.Runtime.Unity;
using UnityEditor;
using UnityEngine;

namespace BTCore.Editor.Inspectors.MonoBehaviours
{
    [CustomEditor(typeof(BehaviorTree))]
    public class BehaviorTreeInspector : UnityEditor.Editor
    {
        private SerializedProperty _btAsset;
        private BehaviorTree _behaviorTree;
        
        private void OnEnable() {
            _btAsset = serializedObject.FindProperty("_btAsset");
            _behaviorTree = (BehaviorTree) target;
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.PropertyField(_btAsset, new GUIContent("BTAsset"));
            // 立即更新变化值
            serializedObject.ApplyModifiedProperties();
            // BTAsset有变化时，若BT窗口已经打开需更新显示
            if (EditorGUI.EndChangeCheck()) {
                if (BTEditorWindow.Instance == null) {
                    return;
                }
                _behaviorTree.CreateBTree();
                BTEditorWindow.Instance.SelectNewTree(_behaviorTree.BTree);
            }
            
            if (GUILayout.Button("Open", GUILayout.Width(50))) {
                if (BTEditorWindow.Instance == null) {
                    BTEditorWindow.OpenWindow();
                }

                if (_behaviorTree.BTree == null) {
                    _behaviorTree.CreateBTree();
                }
                
                BTEditorWindow.Instance.SelectNewTree(_behaviorTree.BTree);
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
}