#if UNITY_EDITOR && ABILITYKIT_PIPELINE_THIRDPARTY_GRAPH

using System;
using System.IO;
using AbilityKit.Core.Generic;
using Emilia.Kit.Editor;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityPipelineDebuggerWindow : EditorWindow
    {
        [MenuItem("Window/AbilityKit/Ability Pipeline Debugger")]
        static void Open()
        {
            GetWindow<AbilityPipelineDebuggerWindow>(utility: false, title: "Ability Pipeline Debugger");
        }

        private int _selectedIndex;

        void OnEnable()
        {
            AbilityPipelineLiveRegistry.Changed += Repaint;
        }

        void OnDisable()
        {
            AbilityPipelineLiveRegistry.Changed -= Repaint;
        }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUILayout.LabelField("Play Mode Only", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);

                var entries = AbilityPipelineLiveRegistry.GetEntries();
                if (entries.Count == 0)
                {
                    EditorGUILayout.HelpBox("No running pipelines registered.", MessageType.Info);
                    return;
                }

                var names = new string[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    names[i] = $"{i}: {entries[i].Name}";
                }

                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, names.Length - 1);
                _selectedIndex = EditorGUILayout.Popup("Running Pipeline", _selectedIndex, names);

                var selected = entries[_selectedIndex];
                var runObj = selected.Run.Target;
                if (runObj == null)
                {
                    EditorGUILayout.HelpBox("Selected run instance is no longer alive.", MessageType.Warning);
                    return;
                }

                var s = selected.LastSnapshot;
                EditorGUILayout.LabelField("State", s.State.ToString());
                EditorGUILayout.LabelField("CurrentPhaseId", s.CurrentPhaseId.ToString());
                EditorGUILayout.LabelField("PhaseIndex", s.PhaseIndex.ToString());
                EditorGUILayout.LabelField("Paused", s.IsPaused ? "Yes" : "No");

                if (GUILayout.Button("Select This Run (for highlight)"))
                {
                    AbilityPipelineLiveRegistry.SelectedRun = runObj;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Graph"))
                    {
                        OpenGraphFor(selected);
                    }

                    if (GUILayout.Button("Rebuild Graph"))
                    {
                        RebuildGraphFor(selected);
                    }
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the pipeline debugger.", MessageType.Info);
            }
        }

        private static string GetAssetPath(int configId)
        {
            var folder = "Assets/DebugPipelines";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return $"{folder}/AbilityPipeline_{configId}.asset";
        }

        private static void OpenGraphFor(AbilityPipelineLiveRegistry.Entry entry)
        {
            var asset = EnsureGraphAsset(entry);
            if (asset == null) return;

            EditorAssetWindowUtility.OpenWindow(typeof(EditorGraphWindow), EditorGraphWindow.GetId(asset), asset);
        }

        private static void RebuildGraphFor(AbilityPipelineLiveRegistry.Entry entry)
        {
            var asset = EnsureGraphAsset(entry, rebuild: true);
            if (asset == null) return;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static AbilityPipelineEditorGraphAsset EnsureGraphAsset(AbilityPipelineLiveRegistry.Entry entry, bool rebuild = false)
        {
            var configObj = entry.Config.Target as IAbilityPipelineConfig;
            if (configObj == null)
            {
                Debug.LogWarning("Pipeline config is not available (GC'd). Cannot build graph.");
                return null;
            }

            var path = GetAssetPath(configObj.ConfigId);
            var asset = AssetDatabase.LoadAssetAtPath<AbilityPipelineEditorGraphAsset>(path);
            if (asset == null)
            {
                asset = EditorGraphAssetUtility.Create<AbilityPipelineEditorGraphAsset>(path);
                rebuild = true;
            }

            if (rebuild)
            {
                asset.RebuildFromConfig(configObj);
                asset.name = $"AbilityPipeline_{configObj.ConfigId}";
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }
    }
}

#endif
