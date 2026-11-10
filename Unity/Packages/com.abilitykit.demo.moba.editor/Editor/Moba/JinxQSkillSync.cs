using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Publishes the authored Jinx Q and mode-specific basic attack pipelines.</summary>
    public static class JinxQSkillSync
    {
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private static readonly AuthoredFlow[] AuthoredFlows =
        {
            new AuthoredFlow(30020101, "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-q-flow.json"),
            new AuthoredFlow(30020002, "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-powpow-basic-attack-flow.json"),
            new AuthoredFlow(30020003, "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-fishbones-basic-attack-flow.json")
        };

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Publish Q")]
        public static void SyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SyncAuthoredFlows();
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[JinxQSkillSync] Published and checked Jinx Q and mode-specific basic attack flows.");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Check Q")]
        public static void CheckPublished()
        {
            MobaSkillPipelineExporter.Check();
            Debug.Log("[JinxQSkillSync] Jinx Q Switcheroo! published flow check passed.");
        }

        private static void SyncAuthoredFlows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);

            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            foreach (var authoredFlow in AuthoredFlows)
            {
                var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", authoredFlow.SourcePath));
                if (!File.Exists(sourcePath)) throw new InvalidDataException("Jinx flow source is missing: " + sourcePath);

                var sourceText = File.ReadAllText(sourcePath).Trim();
                if (sourceText.StartsWith("{", StringComparison.Ordinal)) sourceText = "[" + sourceText + "]";
                var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(sourceText, typeof(SkillFlowDTO));
                if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) || dto.Id != authoredFlow.FlowId)
                    throw new InvalidDataException("Jinx flow source must contain exactly flow " + authoredFlow.FlowId + ".");

                var matches = flows.Select((flow, index) => new { flow, index })
                    .Where(item => item.flow != null && item.flow.Id == authoredFlow.FlowId)
                    .ToArray();
                if (matches.Length > 1)
                    throw new InvalidDataException("SkillFlowCO contains duplicate Jinx flow " + authoredFlow.FlowId + ".");

                var authored = SkillFlowDef.FromDto(dto);
                if (matches.Length == 1)
                    flows[matches[0].index] = authored;
                else
                    flows = flows.Concat(new[] { authored }).ToArray();
            }

            asset.dataList = flows;
            EditorUtility.SetDirty(asset);
        }

        private readonly struct AuthoredFlow
        {
            public AuthoredFlow(int flowId, string sourcePath)
            {
                FlowId = flowId;
                SourcePath = sourcePath;
            }

            public int FlowId { get; }
            public string SourcePath { get; }
        }
    }
}
