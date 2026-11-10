using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Editor;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Imports and publishes the authored Jinx W first-slice resources.</summary>
    public static class JinxWSkillSync
    {
        private const string FlowDirectory = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases";
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Publish W")]
        public static void SyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            InvokeTriggerAuthoring("ImportProductionSourcesBatch");
            SyncAuthoredFlows();
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            PublishTriggerPlans();
            CheckTriggerPlans();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[JinxWSkillSync] Published and checked Jinx W first-slice configuration.");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Check W")]
        public static void CheckPublished()
        {
            MobaSkillPipelineExporter.Check();
            CheckTriggerPlans();
            Debug.Log("[JinxWSkillSync] Jinx W Flow and Trigger checks passed.");
        }

        private static void SyncAuthoredFlows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);
            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            var sources = Directory.GetFiles(Resolve(FlowDirectory), "jinx-*-flow.json", SearchOption.TopDirectoryOnly);
            if (sources.Length == 0) throw new InvalidDataException("No Jinx SkillFlow authoring sources were found.");
            foreach (var source in sources.OrderBy(path => path, StringComparer.Ordinal))
            {
                var sourceText = File.ReadAllText(source).Trim();
                if (sourceText.StartsWith("{", StringComparison.Ordinal)) sourceText = "[" + sourceText + "]";
                var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(sourceText, typeof(SkillFlowDTO));
                if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) ||
                    dto.Id < 30020000 || dto.Id >= 30021000)
                    throw new InvalidDataException("Jinx flow source must contain exactly one flow in the Jinx ID range: " + source);
                var matches = flows.Select((flow, index) => new { flow, index })
                    .Where(item => item.flow != null && item.flow.Id == dto.Id).ToArray();
                if (matches.Length > 1)
                    throw new InvalidDataException("SkillFlowCO contains duplicate Jinx flow " + dto.Id + ".");
                var authored = SkillFlowDef.FromDto(dto);
                if (matches.Length == 1) flows[matches[0].index] = authored;
                else flows = flows.Concat(new[] { authored }).ToArray();
            }
            asset.dataList = flows;
            EditorUtility.SetDirty(asset);
        }

        private static void PublishTriggerPlans() => InvokeTriggerAuthoring("PublishRuntimeBatch");
        private static void CheckTriggerPlans() => InvokeTriggerAuthoring("CheckRuntimeBatch");

        private static void InvokeTriggerAuthoring(string methodName)
        {
            var migration = typeof(TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            if (migration == null) throw new MissingMemberException("Trigger Authoring MOBA migration type is unavailable.");
            var method = migration.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(migration.FullName, methodName);
            try { method.Invoke(null, null); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException("Trigger Authoring " + methodName + " failed.", exception.InnerException);
            }
        }

        private static string Resolve(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
