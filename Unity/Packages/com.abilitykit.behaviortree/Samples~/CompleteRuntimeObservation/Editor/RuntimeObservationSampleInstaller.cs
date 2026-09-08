using System;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    /// <summary>把示例 authoring JSON 安装为可由 Graph Editor 打开的项目资产。</summary>
    public static class RuntimeObservationSampleInstaller
    {
        private const string MenuRoot = "AbilityKit/Behavior Tree/Samples/Complete Runtime Observation/";
        private const string JsonFileName = "complete_runtime_observation.json";
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

        [MenuItem(MenuRoot + "Create Sample GameObject")]
        public static void CreateSampleGameObject()
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

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (textAsset == null)
            {
                EditorUtility.DisplayDialog("BehaviorTree Sample", "无法加载示例 authoring JSON。", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            var go = new GameObject("RuntimeObservationSample");
            var sample = go.AddComponent<RuntimeObservationSample>();
            var serialized = new SerializedObject(sample);
            serialized.FindProperty("_authoringJson").objectReferenceValue = textAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog(
                "BehaviorTree Sample",
                "已创建示例 GameObject 并绑定 authoring JSON。\n进入 Play Mode 后右键该组件 Start Runtime，再打开 Window > AbilityKit > Behavior Tree Observation 观察。",
                "OK");
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

        internal static string ToAbsolutePath(string assetPath)
            => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));

        internal static void EnsureAssetDirectory(string assetPath)
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
