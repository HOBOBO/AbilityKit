using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.HFSM.Samples.HfsmRuntimeSample.Editor
{
    /// <summary>
    /// 把随 sample 导入的 Definition JSON 装进一个运行时验证场景：一键创建场景 + GameObject，
    /// 并绑定 Config/hfsm_sample.json。所有产物都落在 Assets/Samples/ 下，可直接删除。
    /// </summary>
    public static class HfsmRuntimeSampleInstaller
    {
        private const string MenuRoot = "AbilityKit/HFSM/Samples/Runtime Sample/";
        private const string JsonFileName = "hfsm_sample.json";

        [MenuItem(MenuRoot + "Create Runtime Verification Scene")]
        public static void CreateSampleScene()
        {
            var textAsset = FindSampleJson();
            if (textAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "HFSM Runtime Sample",
                    $"未找到 {JsonFileName}。请先从 Package Manager 导入 HFSM Runtime Sample。",
                    "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            var go = new GameObject("HfsmRuntimeSample");
            var sample = go.AddComponent<HfsmRuntimeSample>();
            var serialized = new SerializedObject(sample);
            serialized.FindProperty("_definitionJson").objectReferenceValue = textAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog(
                "HFSM Runtime Sample",
                "已创建示例 GameObject 并绑定 hfsm_sample.json。\n进入 Play Mode 后右键该组件 Trigger: go / back 触发状态切换，并打开 Window > AbilityKit > HFSM Runtime Monitor 观察激活路径。",
                "OK");
        }

        private static TextAsset FindSampleJson()
        {
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(JsonFileName)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(JsonFileName, StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) return asset;
            }
            return null;
        }
    }
}
