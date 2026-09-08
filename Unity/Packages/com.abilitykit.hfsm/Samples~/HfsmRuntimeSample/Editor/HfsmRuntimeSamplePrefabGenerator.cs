using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Samples.HfsmRuntimeSample.Editor
{
    /// <summary>
    /// 在 sample 导入后由 Unity 自身序列化生成 Prefabs/HfsmRuntimeSample.prefab（避免手写 YAML）。
    /// 幂等：已存在则跳过；删除后可自动重新生成。
    /// </summary>
    [InitializeOnLoad]
    internal static class HfsmRuntimeSamplePrefabGenerator
    {
        private const string PrefabName = "HfsmRuntimeSample.prefab";

        static HfsmRuntimeSamplePrefabGenerator()
        {
            EditorApplication.delayCall += GenerateIfMissing;
        }

        private static void GenerateIfMissing()
        {
            var sampleRoot = FindSampleRoot();
            if (string.IsNullOrEmpty(sampleRoot)) return;

            var prefabsDir = sampleRoot + "/Prefabs";
            var prefabPath = prefabsDir + "/" + PrefabName;
            if (File.Exists(prefabPath)) return;

            if (!AssetDatabase.IsValidFolder(prefabsDir))
                AssetDatabase.CreateFolder(sampleRoot, "Prefabs");

            var go = new GameObject("HfsmRuntimeSample");
            go.AddComponent<HfsmRuntimeSample>();
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string FindSampleRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets("hfsm_sample t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith("hfsm_sample.json", System.StringComparison.OrdinalIgnoreCase)) continue;

                var resourcesDir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(resourcesDir)) continue;
                return Path.GetDirectoryName(resourcesDir)?.Replace('\\', '/');
            }
            return string.Empty;
        }
    }
}
