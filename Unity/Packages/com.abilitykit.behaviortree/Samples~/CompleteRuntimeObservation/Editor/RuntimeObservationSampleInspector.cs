using UnityEditor;
using UnityEngine;
using AbilityKit.BehaviorTree.Editor;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    [CustomEditor(typeof(RuntimeObservationSample))]
    public sealed class RuntimeObservationSampleInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_runtimeJson"),
                new GUIContent("运行时 JSON"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_authoringJson"),
                new GUIContent("编辑源 JSON（兼容旧示例）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_authoringAsset"),
                new GUIContent("关联的编辑资产"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_settings"),
                new GUIContent("运行设置"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_inputs"),
                new GUIContent("感知输入"), true);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_outputs"),
                    new GUIContent("行为输出"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_frame"),
                    new GUIContent("逻辑帧"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_treeState"),
                    new GUIContent("树状态"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_agentRenderer"),
                new GUIContent("模型渲染器"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_stateLabel"),
                new GUIContent("场景状态标签"));
            serializedObject.ApplyModifiedProperties();
            var sample = (RuntimeObservationSample)target;

            var authoring = serializedObject.FindProperty("_authoringAsset").objectReferenceValue as AuthoringAsset;
            if (authoring != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("行为树示例", EditorStyles.boldLabel);
                if (GUILayout.Button("打开行为树编辑器")) AuthoringGraphWindow.Open(authoring);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (GUILayout.Button("导出当前行为树供场景运行"))
                    {
                        var report = AuthoringRuntimeExporter.Export(authoring);
                        AssetDatabase.Refresh();
                        if (!report.Success)
                            EditorUtility.DisplayDialog("导出失败", string.Join("\n", report.Messages), "确定");
                        else
                        {
                            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(
                                authoring.ResolveRuntimeExportPath(authoring.LoadDocument().Tree.TreeId));
                            serializedObject.FindProperty("_runtimeJson").objectReferenceValue = json;
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行控制", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("启动 / 重建运行实例")) sample.StartRuntime();
                if (GUILayout.Button("推进一个逻辑帧")) sample.StepOnce();
                if (GUILayout.Button("重新开始行为树")) sample.RestartTree();
                if (GUILayout.Button("停止运行")) sample.StopRuntime();
            }

            if (GUILayout.Button("打开运行时观察"))
            {
                EditorWindow
                    .GetWindow<AbilityKit.BehaviorTree.Editor.DebugObservationWindow>()
                    .Show();
            }

        }
    }
}
