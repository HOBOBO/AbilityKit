using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    [CustomEditor(typeof(RuntimeObservationSample))]
    public sealed class RuntimeObservationSampleInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sample = (RuntimeObservationSample)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Start / Recreate Runtime")) sample.StartRuntime();
                if (GUILayout.Button("Step One Deterministic Tick")) sample.StepOnce();
                if (GUILayout.Button("Restart Tree")) sample.RestartTree();
                if (GUILayout.Button("Stop Runtime")) sample.StopRuntime();
            }

            if (GUILayout.Button("Open Runtime Observation"))
            {
                EditorWindow
                    .GetWindow<AbilityKit.BehaviorTree.Editor.DebugObservationWindow>()
                    .Show();
            }

            EditorGUILayout.HelpBox(
                "Play Mode 中修改 Agent Decision Inputs。运行实例会以 Complete Runtime Observation 注册到调试窗口。",
                MessageType.Info);
        }
    }
}
