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

        [MenuItem("Tools/AbilityKit/Framework/Ability/触发器示例/创建功能展示模块")]
        public static void CreateFeatureShowcaseModule()
        {
            var sourcePath = ResolveProjectPath(SampleSourcePath);
            if (!File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog(
                    "触发器编辑示例",
                    "未找到示例 Source JSON：\n" + sourcePath,
                    "确定");
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
                    "触发器编辑示例",
                    "导入示例 Source JSON 失败：\n" + result.Message,
                    "确定");
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
