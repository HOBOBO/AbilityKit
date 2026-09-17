using AbilityKit.HFSM.Editor;
using AbilityKit.HFSM.Editor.RuntimeMonitor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase.Editor
{
    [CustomEditor(typeof(HfsmShowcaseAgent))]
    public sealed class HfsmShowcaseAgentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sourceGraph"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_runtimeJson"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sourceActions"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_actionJson"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_animator"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_ticksPerSecond"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_moving"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_blocked"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_health"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hasTarget"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetDistance"));
            serializedObject.ApplyModifiedProperties();

            var agent = (HfsmShowcaseAgent)target;
            EditorGUILayout.LabelField("Frame", agent.Frame.ToString());
            EditorGUILayout.LabelField("Active Path", agent.ActivePath);
            EditorGUILayout.LabelField("Current Action", agent.CurrentAction);
            EditorGUILayout.LabelField("Action Local Frame", agent.CurrentActionLocalFrame.ToString());
            EditorGUILayout.LabelField("Last Action Log", agent.LastActionLog);
            EditorGUILayout.LabelField("Log Frame", agent.LastActionLogFrame.ToString());
            using (new EditorGUI.DisabledScope(agent.SourceGraph == null ||
                agent.SourceActions == null || Application.isPlaying))
            {
                if (GUILayout.Button("Open Graph Editor"))
                    StateMachineEditorWindow.Open(agent.SourceGraph);
                if (GUILayout.Button("Export Current Graph"))
                {
                    try { HfsmShowcaseInstaller.Export(agent.SourceGraph, agent.SourceActions); }
                    catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }
            using (new EditorGUI.DisabledScope(agent.SourceActions == null || Application.isPlaying))
            {
                if (GUILayout.Button("Select Composite Actions"))
                {
                    Selection.activeObject = agent.SourceActions;
                    EditorGUIUtility.PingObject(agent.SourceActions);
                }
                if (GUILayout.Button("Export Composite Actions"))
                {
                    try { HfsmShowcaseInstaller.ExportActions(agent.SourceActions); }
                    catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }
            using (new EditorGUI.DisabledScope(!agent.IsRunning))
            {
                if (GUILayout.Button("Open Runtime Monitor")) RuntimeMonitorWindow.OpenWindow(agent.name);
                if (GUILayout.Button("Step One Tick")) agent.StepOnce();
                if (GUILayout.Button("Save Checkpoint")) agent.SaveCheckpoint();
                if (GUILayout.Button("Restore Checkpoint")) agent.RestoreCheckpoint();
                if (GUILayout.Button("Respawn")) agent.Respawn();
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("Restart")) agent.Restart();
            if (agent.IsRunning) Repaint();
        }
    }
}
