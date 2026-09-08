using System.IO;
using AbilityKit.BehaviorTree.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    /// <summary>
    /// 在 sample 导入后自动生成 AuthoringAsset，让 Graph Editor 与「预览」直接可打开。
    /// 幂等：资产已存在则跳过，删除后可自动重新生成——与 prefab 自动生成同一模式；
    /// 手写 .asset YAML 不可靠，资产一律由编辑器自身序列化产出。
    /// </summary>
    [InitializeOnLoad]
    internal static class RuntimeObservationSampleAssetGenerator
    {
        private const string AssetPath = "Assets/BehaviorTreeSamples/CompleteRuntimeObservation.asset";

        static RuntimeObservationSampleAssetGenerator()
        {
            EditorApplication.delayCall += GenerateIfMissing;
        }

        private static void GenerateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetPath) != null) return;

            var jsonPath = RuntimeObservationSampleInstaller.FindImportedSampleJsonPath();
            if (jsonPath.Length == 0) return;

            var json = File.ReadAllText(RuntimeObservationSampleInstaller.ToAbsolutePath(jsonPath));
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.name = "CompleteRuntimeObservation";
            asset.ImportJson(json);

            RuntimeObservationSampleInstaller.EnsureAssetDirectory(AssetPath);
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
