#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaP1SkillFlowShowcaseSync
    {
        public const int FlowId = 80120001;
        public const string SourceAssetPath = "Assets/AbilityKit/MobaSkillFlowAuthoring/Showcases/moba-p1-skill-economy-flow.json";
        public const string TargetAssetPath = "Assets/AbilityKit/MobaSkillFlowAuthoring/Showcases/MobaP1SkillEconomyShowcase.asset";

        [MenuItem("AbilityKit/Moba/Skill Flow/Sync P1 Economy Showcase")]
        public static void Sync()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceAssetPath));
            var array = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
                File.ReadAllText(sourcePath), typeof(SkillFlowDTO));
            if (array == null || array.Length != 1 || !(array.GetValue(0) is SkillFlowDTO dto) || dto.Id != FlowId)
                throw new InvalidOperationException("P1 skill economy showcase source must contain exactly one reserved flow.");

            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(TargetAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SkillFlowSO>();
                AssetDatabase.CreateAsset(asset, TargetAssetPath);
            }
            asset.dataList = new[] { SkillFlowDef.FromDto(dto) };
            asset.legacyDataList = Array.Empty<SkillFlowDTO>();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void SyncBatch()
        {
            Sync();
        }
    }
}
#endif
