using System;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    /// <summary>把示例 authoring JSON 安装为可由 Graph Editor 打开的项目资产。</summary>
    public static class RuntimeObservationSampleInstaller
    {
        private const string MenuRoot = "AbilityKit/Behavior Tree/Samples/Complete Runtime Observation/";
        private const string JsonFileName = "complete_runtime_observation.authoring.json";
        private const string DefaultAssetPath = "Assets/BehaviorTreeSamples/CompleteRuntimeObservation.asset";

        [MenuItem(MenuRoot + "Create Or Refresh Authoring Asset")]
        public static void CreateOrRefreshAuthoringAsset()
        {
            var jsonPath = FindImportedSampleJsonPath();
            if (jsonPath.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "BehaviorTree Sample",
                    $"未找到 {JsonFileName}。请先从 Package Manager 导入 Complete Runtime Observation sample。",
                    "OK");
                return;
            }

            var json = File.ReadAllText(ToAbsolutePath(jsonPath));
            _ = AuthoringJson.Load(json);

            EnsureAssetDirectory(DefaultAssetPath);
            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(DefaultAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AuthoringAsset>();
                asset.name = "CompleteRuntimeObservation";
                asset.ImportJson(json);
                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            }
            else
            {
                Undo.RecordObject(asset, "Refresh BehaviorTree Sample");
                asset.ImportJson(json);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AuthoringGraphWindow.Open(asset);
        }

        [MenuItem(MenuRoot + "Open Runtime Observation")]
        public static void OpenRuntimeObservation()
        {
            EditorWindow.GetWindow<DebugObservationWindow>().Show();
        }

        internal static string FindImportedSampleJsonPath()
        {
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(JsonFileName)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(JsonFileName, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return "";
        }

        private static string ToAbsolutePath(string assetPath)
            => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));

        private static void EnsureAssetDirectory(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory)) return;

            var current = "Assets";
            var segments = directory.Split('/');
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
