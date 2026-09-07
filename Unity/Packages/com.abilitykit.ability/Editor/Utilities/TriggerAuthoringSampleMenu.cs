#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringSampleMenu
    {
        private const string SampleSourcePath =
            "Packages/com.abilitykit.ability/Samples~/TriggerAuthoring/trigger-editor-feature-showcase.trigger.json";
        private const string OutputFolder = "Assets/AbilityKit/TriggerAuthoringSamples";
        private const string OutputAssetPath = OutputFolder + "/TriggerEditorFeatureShowcase.asset";

        [MenuItem("Tools/AbilityKit/Framework/Ability/Trigger Authoring Samples/Create Feature Showcase Module")]
        public static void CreateFeatureShowcaseModule()
        {
            var sourcePath = ResolveProjectPath(SampleSourcePath);
            if (!File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog(
                    "Trigger Authoring Sample",
                    "Sample source JSON was not found:\n" + sourcePath,
                    "OK");
                return;
            }

            EnsureFolder(OutputFolder);
            var asset = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(OutputAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
                AssetDatabase.CreateAsset(asset, OutputAssetPath);
            }

            var result = TriggerAuthoringSourceSync.Import(asset, sourcePath, force: true);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog(
                    "Trigger Authoring Sample",
                    "Failed to import sample source JSON:\n" + result.Message,
                    "OK");
                return;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[TriggerAuthoringSampleMenu] Created sample TriggerAuthoringModuleAsset: " + OutputAssetPath);
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath ?? string.Empty));
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal)) return;

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
