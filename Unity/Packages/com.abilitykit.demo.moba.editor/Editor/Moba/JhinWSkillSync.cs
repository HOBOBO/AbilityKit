using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Imports and publishes the authored first-slice Jhin W resources.</summary>
    public static class JhinWSkillSync
    {
        private const string ProjectPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/MobaTriggerAuthoringProject.asset";
        private const string SourcePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/moba-skills.trigger.json";
        private const string ModulePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/ability_moba_skills.Module.asset";
        private const string PassiveSourcePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/moba-passives.trigger.json";
        private const string PassiveModulePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/passive_moba_passives.Module.asset";
        private const string FlowSourceDirectory = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases";
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";

        public static void SyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SyncAuthoredFlows();
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            SyncTriggerSource();
            SyncPassiveTriggerSource();
            PublishTriggerPlans();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JhinWSkillSync] Imported and published Jhin W first-slice configuration.");
        }

        public static void CheckPublished()
        {
            var migrationType = typeof(TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            var check = migrationType?.GetMethod("CheckRuntimeBatch", BindingFlags.Static | BindingFlags.Public);
            if (check == null) throw new MissingMethodException("Trigger Authoring runtime checker is unavailable.");
            check.Invoke(null, null);
            MobaSkillPipelineExporter.Check();
            Debug.Log("[JhinWSkillSync] Trigger and skill pipeline published checks passed.");
        }


        private static void SyncTriggerSource()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectPath);
            var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(ModulePath);
            if (project == null || module == null) throw new InvalidDataException("MOBA Trigger Authoring project or skill module is missing.");
            var options = new TriggerAuthoringModuleSourceImportOptions
            {
                ExpectedModuleId = "ability.moba.skills",
                Force = true,
                ValidateProject = false,
            };
            if (!TriggerAuthoringModuleSourceImportApi.Import(module, project, Resolve(SourcePath), options, out var message))
                throw new InvalidDataException(message);
        }

        private static void SyncPassiveTriggerSource()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectPath);
            var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(PassiveModulePath);
            if (project == null || module == null) throw new InvalidDataException("MOBA Trigger Authoring project or passive module is missing.");
            var options = new TriggerAuthoringModuleSourceImportOptions
            {
                ExpectedModuleId = "passive.moba.passives",
                Force = true,
                ValidateProject = false,
            };
            if (!TriggerAuthoringModuleSourceImportApi.Import(module, project, Resolve(PassiveSourcePath), options, out var message))
                throw new InvalidDataException(message);
        }

        private static void SyncAuthoredFlows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);
            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            var sources = Directory.GetFiles(Resolve(FlowSourceDirectory), "jhin-*-flow.json", SearchOption.TopDirectoryOnly);
            if (sources.Length == 0) throw new InvalidDataException("No Jhin SkillFlow authoring sources were found.");
            foreach (var source in sources.OrderBy(path => path, StringComparer.Ordinal))
            {
                var sourceText = File.ReadAllText(source).Trim();
                if (sourceText.StartsWith("{", StringComparison.Ordinal)) sourceText = "[" + sourceText + "]";
                var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(sourceText, typeof(SkillFlowDTO));
                if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) || dto.Id < 30010000 || dto.Id >= 30011000)
                    throw new InvalidDataException("Jhin flow source must contain exactly one flow in the Jhin ID range: " + source);
                var existing = flows.SingleOrDefault(flow => flow != null && flow.Id == dto.Id);
                if (existing == null) flows = flows.Concat(new[] { SkillFlowDef.FromDto(dto) }).ToArray();
                else flows[Array.IndexOf(flows, existing)] = SkillFlowDef.FromDto(dto, existing);
            }
            asset.dataList = flows;
            EditorUtility.SetDirty(asset);
        }

        private static string Resolve(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

        private static void PublishTriggerPlans()
        {
            var migrationType = typeof(TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            var publish = migrationType?.GetMethod("PublishRuntimeBatch", BindingFlags.Static | BindingFlags.Public);
            if (publish == null) throw new MissingMethodException("Trigger Authoring runtime publisher is unavailable.");
            publish.Invoke(null, null);
        }
    }
}
