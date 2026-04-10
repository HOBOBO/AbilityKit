#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerJsonExporter
    {
        private const string OutputResourcesDir = "ability";
        private const string OutputFileWithoutExt = "ability_triggers";
        private const string OutputPlanFileWithoutExt = "ability_trigger_plans";
        private const string DefaultAbilityConfigFolder = "Assets/Configs/Ability";

        [MenuItem("AbilityKit/Ability/Export Trigger Json")]
        public static void ExportSelectedFolder()
        {
            var folder = AbilityTriggerExportUtils.TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Plan Json")]
        public static void ExportSelectedFolderPlans()
        {
            var folder = AbilityTriggerExportUtils.TryGetSelectedFolderPath();
            ExportPlanFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Json (Configs/Ability)")]
        public static void ExportDefaultFolder()
        {
            ExportFromFolder(DefaultAbilityConfigFolder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Plan Json (Configs/Ability)")]
        public static void ExportDefaultFolderPlans()
        {
            ExportPlanFromFolder(DefaultAbilityConfigFolder);
        }

        public static void ExportFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            ExportLog.Info($"ExportFromFolder: {assetFolder}");

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var dto = LegacyTriggerJsonBuilder.BuildDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                ExportLog.Warning($"No triggers exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = LegacyTriggerJsonBuilder.BuildDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
            }

            ExportLog.Info($"Modules={moduleCount}, ExportedTriggers={exportedTriggerCount}, SkippedDisabled={skippedDisabledCount}, SkippedTriggerId<=0={skippedInvalidIdCount}");

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            ExportLog.Info($"Exported to: {outputPath}");
        }

        public static void ExportPlanFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            ExportLog.Info($"ExportPlanFromFolder: {assetFolder}");

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var dto = TriggerPlanExportPipeline.BuildPlanDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                ExportLog.Warning($"No trigger plans exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = TriggerPlanExportPipeline.BuildPlanDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
            }

            ExportLog.Info($"Plan Modules={moduleCount}, ExportedTriggers={exportedTriggerCount}, SkippedDisabled={skippedDisabledCount}, SkippedTriggerId<=0={skippedInvalidIdCount}");

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputPlanFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            ExportLog.Info($"Exported plans to: {outputPath}");
        }

        internal static string[] FindAbilityModuleGuids(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { assetFolder });
            var primaryCount = guids != null ? guids.Length : 0;
            if (guids != null && guids.Length > 0)
            {
                ExportLog.Info($"FindAssets('t:AbilityModuleSO') found {primaryCount} under '{assetFolder}'.");
                return guids;
            }

            ExportLog.Warning($"FindAssets('t:AbilityModuleSO') found 0 under '{assetFolder}'. Trying fallback scan...");

            // Fallback: some Unity setups may fail to resolve t:AbilityModuleSO queries for types defined in packages.
            // Scan ScriptableObjects and filter by main asset type.
            var soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { assetFolder });
            var soCount = soGuids != null ? soGuids.Length : 0;
            if (soGuids == null || soGuids.Length == 0)
            {
                ExportLog.Warning($"No ScriptableObject assets found under '{assetFolder}'. primaryCount={primaryCount}");
                return Array.Empty<string>();
            }

            var list = new List<string>();
            for (int i = 0; i < soGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(soGuids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                Type t;
                try { t = AssetDatabase.GetMainAssetTypeAtPath(path); }
                catch { continue; }
                if (t == typeof(AbilityModuleSO))
                {
                    list.Add(soGuids[i]);
                }
            }

            if (list.Count == 0)
            {
                var examplePath = AssetDatabase.GUIDToAssetPath(soGuids[0]);
                Type exampleType = null;
                try { exampleType = AssetDatabase.GetMainAssetTypeAtPath(examplePath); }
                catch { }

                ExportLog.Warning(
                    $"No AbilityModuleSO assets found under '{assetFolder}'. " +
                    $"primaryCount={primaryCount}, soCount={soCount}, matched=0, example='{examplePath}', exampleType='{exampleType?.FullName ?? "<null>"}'. " +
                    $"This usually means Unity can't resolve AbilityModuleSO type (assembly not loaded / compile errors / domain reload pending)."
                );
                return Array.Empty<string>();
            }

            var exampleFoundPath = AssetDatabase.GUIDToAssetPath(list[0]);
            ExportLog.Warning($"FindAssets('t:AbilityModuleSO') returned 0; fallback scan found {list.Count} AbilityModuleSO (soCount={soCount}). example='{exampleFoundPath}'");
            return list.ToArray();
        }

    }
}
#endif
